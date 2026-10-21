using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Serilog.Events;
using UserManagementAPI.Contracts;
using UserManagementAPI.Data;
using UserManagementAPI.Services;
using Xunit;

namespace UserManagementAPI.Tests.Integration;

public sealed class SecurityAndLoggingTests
{
    [Theory]
    [InlineData("IT")]
    [InlineData("HR")]
    public async Task BothRolesCanPerformFullCrud(string role)
    {
        await using var factory = new ApiFactory();
        using var client = await factory.CreateReadyClientAsync(role: role);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/users")).StatusCode);
        var created = await client.PostAsJsonAsync("/api/users", UserEndpointTests.ValidUser());
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var user = (await created.Content.ReadFromJsonAsync<UserResponse>())!;
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(created.Headers.Location)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await client.PutAsJsonAsync($"/api/users/{user.Id}", UserEndpointTests.ValidUser())).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/users/{user.Id}")).StatusCode);
    }

    public static IEnumerable<object[]> UnauthorizedRequests()
    {
        foreach (var method in new[] { "LIST", "GET", "POST", "PUT", "DELETE" })
            foreach (var token in new[] { "missing", "malformed", "expired", "future", "wrong-key", "tampered", "wrong-issuer", "wrong-audience", "unsigned", "no-expiration", "wrong-role", "no-role" })
                yield return new object[] { method, token };
    }

    [Theory]
    [MemberData(nameof(UnauthorizedRequests))]
    public async Task EveryRouteRejectsInvalidTokensAndRoles(string method, string tokenKind)
    {
        await using var factory = new ApiFactory();
        using var client = await factory.CreateReadyClientAsync(authenticated: false);
        var token = tokenKind switch
        {
            "missing" => null,
            "malformed" => "not-a-jwt",
            "expired" => factory.CreateToken(expired: true),
            "future" => factory.CreateToken(future: true),
            "wrong-key" => factory.CreateToken(wrongKey: true),
            "tampered" => Tamper(factory.CreateToken()),
            "wrong-issuer" => factory.CreateToken(issuer: "untrusted"),
            "wrong-audience" => factory.CreateToken(audience: "other-api"),
            "unsigned" => factory.CreateToken(unsigned: true),
            "no-expiration" => factory.CreateToken(noExpiration: true),
            "wrong-role" => factory.CreateToken(role: "Finance"),
            "no-role" => factory.CreateToken(role: null),
            _ => throw new InvalidOperationException()
        };
        if (token is not null) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var request = new HttpRequestMessage(new HttpMethod(method == "LIST" ? "GET" : method),
            method is "LIST" or "POST" ? "/api/users" : $"/api/users/{Guid.NewGuid()}");
        if (method is "POST" or "PUT") request.Content = JsonContent.Create(UserEndpointTests.ValidUser());
        var response = await client.SendAsync(request);
        var expected = tokenKind is "wrong-role" or "no-role" ? HttpStatusCode.Forbidden : HttpStatusCode.Unauthorized;
        await UserEndpointTests.AssertProblem(response, expected);
        if (expected == HttpStatusCode.Unauthorized)
            Assert.Contains(response.Headers.WwwAuthenticate, header => header.Scheme == "Bearer");
        await AssertLogged(factory, response, (int)expected);
        if (token is not null) Assert.DoesNotContain(token, AllLogs(factory));
        await factory.WithDbAsync(async db => Assert.Empty(await db.Users.ToListAsync()));
    }

    [Fact]
    public async Task MetadataLogsCoverSuccessAndExpectedErrorsWithoutSensitiveData()
    {
        await using var factory = new ApiFactory();
        using var client = await factory.CreateReadyClientAsync();
        const string marker = "PRIVATE-MARKER-9f78";
        client.DefaultRequestHeaders.Add("X-Correlation-ID", marker);
        var payload = UserEndpointTests.ValidUser("sensitive-person@example.com");
        payload.FirstName = marker;
        var created = await client.PostAsJsonAsync("/api/users", payload);
        await AssertLogged(factory, created, 201);
        var user = (await created.Content.ReadFromJsonAsync<UserResponse>())!;
        await AssertLogged(factory, await client.GetAsync("/api/users?ignored=" + marker), 200);
        await AssertLogged(factory, await client.PostAsJsonAsync("/api/users", payload), 409);
        await AssertLogged(factory, await client.PostAsync("/api/users", new StringContent("{", Encoding.UTF8, "application/json")), 400);
        await AssertLogged(factory, await client.GetAsync("/api/users/" + marker), 400);
        await AssertLogged(factory, await client.GetAsync("/unknown/" + marker), 404);
        await AssertLogged(factory, await client.DeleteAsync($"/api/users/{user.Id}"), 204);
        await AssertLogged(factory, await client.GetAsync($"/api/users/{user.Id}"), 404);
        var logs = AllLogs(factory);
        Assert.DoesNotContain(marker, logs);
        Assert.DoesNotContain(payload.Email, logs);
        Assert.DoesNotContain(client.DefaultRequestHeaders.Authorization!.Parameter!, logs);
        Assert.DoesNotContain(factory.Logs.Events, e => e.Level >= LogEventLevel.Error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FaultsReturnSanitized500AreLoggedAndHostRecovers(bool databaseFailure)
    {
        const string secret = "private-token-and-connection-string";
        var gate = new FaultGate();
        await using var factory = new ApiFactory
        {
            ConfigureServices = services =>
            {
                if (!databaseFailure)
                {
                    services.RemoveAll<IUserService>();
                    services.AddScoped<IUserService>(provider => new FaultOnceService(
                        new UserService(provider.GetRequiredService<AppDbContext>()), gate, secret));
                }
            }
        };
        using var client = await factory.CreateReadyClientAsync();
        if (databaseFailure)
            await factory.WithDbAsync(db => db.Database.ExecuteSqlRawAsync("ALTER TABLE Users RENAME TO UnavailableUsers"));
        var failed = await client.GetAsync("/api/users");
        await UserEndpointTests.AssertProblem(failed, HttpStatusCode.InternalServerError);
        await AssertLogged(factory, failed, 500);
        var body = await failed.Content.ReadAsStringAsync();
        Assert.DoesNotContain(secret, body);
        Assert.DoesNotContain("Exception", body);
        Assert.DoesNotContain("UnavailableUsers", body);
        var logs = AllLogs(factory);
        Assert.DoesNotContain(secret, logs);
        Assert.DoesNotContain("SELECT", logs);
        Assert.DoesNotContain("no such table", logs);
        Assert.Contains(factory.Logs.Events, e => e.Level == LogEventLevel.Error && e.Properties.ContainsKey("ExceptionType"));
        if (databaseFailure)
            await factory.WithDbAsync(db => db.Database.ExecuteSqlRawAsync("ALTER TABLE UnavailableUsers RENAME TO Users"));
        await AssertLogged(factory, await client.GetAsync("/api/users"), 200);
    }

    private static string Tamper(string token)
    {
        var parts = token.Split('.');
        parts[2] = (parts[2][0] == 'a' ? "b" : "a") + parts[2][1..];
        return string.Join('.', parts);
    }

    private static string AllLogs(ApiFactory factory) => string.Join('\n', factory.Logs.Events.Select(e =>
        e.RenderMessage() + string.Join(',', e.Properties.Select(p => $"{p.Key}={p.Value}")) + e.Exception));

    private static async Task AssertLogged(ApiFactory factory, HttpResponseMessage response, int expectedStatus)
    {
        Assert.Equal(expectedStatus, (int)response.StatusCode);
        var correlation = Assert.Single(response.Headers.GetValues("X-Correlation-ID"));
        // TestServer can deliver the body before the outer middleware finishes.
        for (var attempt = 0; attempt < 100 && !factory.Logs.Events.Any(e =>
            e.Properties.ContainsKey("Method") &&
            ((ScalarValue)e.Properties["CorrelationId"]).Value?.ToString() == correlation); attempt++)
            await Task.Delay(10);
        var log = Assert.Single(factory.Logs.Events, e => e.Properties.ContainsKey("Method") &&
            ((ScalarValue)e.Properties["CorrelationId"]).Value?.ToString() == correlation);
        Assert.Equal(expectedStatus, ((ScalarValue)log.Properties["StatusCode"]).Value);
        Assert.Equal(response.RequestMessage!.Method.Method, ((ScalarValue)log.Properties["Method"]).Value);
        Assert.True(Convert.ToDouble(((ScalarValue)log.Properties["ElapsedMs"]).Value) >= 0);
        var requestedPath = response.RequestMessage.RequestUri!.AbsolutePath;
        var expectedPath = requestedPath == "/api/users" ? "api/users" :
            requestedPath.StartsWith("/api/users/") ? "api/users/{id}" : "/unmatched";
        Assert.Equal(expectedPath, ((ScalarValue)log.Properties["Path"]).Value);
    }

    private sealed class FaultGate { public int Calls; }
    private sealed class FaultOnceService(IUserService inner, FaultGate gate, string secret) : IUserService
    {
        public Task<IReadOnlyList<UserResponse>> GetAllAsync(int page = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref gate.Calls) == 1) throw new InvalidOperationException(secret);
            return inner.GetAllAsync(page, pageSize, cancellationToken);
        }
        public Task<UserResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => inner.GetByIdAsync(id, cancellationToken);
        public Task<UserWriteResult> CreateAsync(UserDetails details, CancellationToken cancellationToken = default) => inner.CreateAsync(details, cancellationToken);
        public Task<UserWriteResult> UpdateAsync(Guid id, UserDetails details, CancellationToken cancellationToken = default) => inner.UpdateAsync(id, details, cancellationToken);
        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) => inner.DeleteAsync(id, cancellationToken);
    }
}
