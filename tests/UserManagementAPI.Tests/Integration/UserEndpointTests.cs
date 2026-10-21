using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UserManagementAPI.Contracts;
using UserManagementAPI.Models;
using Xunit;

namespace UserManagementAPI.Tests.Integration;

public sealed class UserEndpointTests
{
    internal static CreateUserRequest ValidUser(string email = "alice@example.com") => new()
    {
        FirstName = "Alice", LastName = "Martin", Email = email, Department = "IT"
    };

    [Fact]
    public async Task CrudRoundTripUsesDtoLocationAndDocumentedStatuses()
    {
        await using var factory = new ApiFactory();
        using var client = await factory.CreateReadyClientAsync();
        var created = await client.PostAsJsonAsync("/api/users", ValidUser());
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var user = (await created.Content.ReadFromJsonAsync<UserResponse>())!;
        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.EndsWith($"/api/users/{user.Id}", created.Headers.Location!.ToString());
        var found = await client.GetAsync(created.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, found.StatusCode);
        Assert.Equal(user, await found.Content.ReadFromJsonAsync<UserResponse>());
        var json = JsonDocument.Parse(await found.Content.ReadAsStringAsync());
        Assert.Equal(new[] { "department", "email", "firstName", "id", "lastName" },
            json.RootElement.EnumerateObject().Select(p => p.Name).Order().ToArray());

        var update = ValidUser("new@example.com");
        update.FirstName = " Alicia ";
        update.Department = "HR";
        var updated = await client.PutAsJsonAsync($"/api/users/{user.Id}", update);
        Assert.Equal(HttpStatusCode.NoContent, updated.StatusCode);
        Assert.Equal("", await updated.Content.ReadAsStringAsync());
        var saved = await client.GetFromJsonAsync<UserResponse>($"/api/users/{user.Id}");
        Assert.Equal("Alicia", saved!.FirstName);
        Assert.Equal("new@example.com", saved.Email);
        Assert.Equal("HR", saved.Department);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/users/{user.Id}")).StatusCode);
        await AssertProblem(await client.GetAsync($"/api/users/{user.Id}"), HttpStatusCode.NotFound);
        await AssertProblem(await client.DeleteAsync($"/api/users/{user.Id}"), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task EmptyAndOutOfRangePagesReturnEmptyArrays()
    {
        await using var factory = new ApiFactory();
        using var client = await factory.CreateReadyClientAsync();
        foreach (var path in new[] { "/api/users", "/api/users?page=10&pageSize=100" })
        {
            var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Empty((await response.Content.ReadFromJsonAsync<UserResponse[]>())!);
        }
    }

    [Fact]
    public async Task PagingIsBoundedStableAndProjectsOnlyDtoFields()
    {
        await using var factory = new ApiFactory();
        using var client = await factory.CreateReadyClientAsync();
        await factory.WithDbAsync(async db =>
        {
            for (var n = 105; n >= 1; n--)
                db.Users.Add(new User { Id = Guid.Parse($"00000000-0000-0000-0000-{n:D12}"),
                    FirstName = "Same", LastName = "Name", Email = $"user{n}@example.com", Department = "IT" });
            await db.SaveChangesAsync();
        });
        var first = (await client.GetFromJsonAsync<UserResponse[]>("/api/users"))!;
        var second = (await client.GetFromJsonAsync<UserResponse[]>("/api/users?page=2"))!;
        var repeated = (await client.GetFromJsonAsync<UserResponse[]>("/api/users"))!;
        Assert.Equal(25, first.Length);
        Assert.Equal(25, second.Length);
        Assert.Equal(first, repeated);
        Assert.Empty(first.Select(u => u.Id).Intersect(second.Select(u => u.Id)));
        Assert.Equal(Guid.Parse("00000000-0000-0000-0000-000000000001"), first[0].Id);
        Assert.Equal(100, (await client.GetFromJsonAsync<UserResponse[]>("/api/users?pageSize=100"))!.Length);
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("page=-1")]
    [InlineData("page=abc")]
    [InlineData("page=2147483648")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=-1")]
    [InlineData("pageSize=101")]
    [InlineData("pageSize=abc")]
    [InlineData("page=2147483647&pageSize=100")]
    public async Task InvalidPagingReturns400(string query)
    {
        await using var factory = new ApiFactory();
        using var client = await factory.CreateReadyClientAsync();
        await AssertProblem(await client.GetAsync($"/api/users?{query}"), HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("GET", "not-a-guid")]
    [InlineData("PUT", "not-a-guid")]
    [InlineData("DELETE", "not-a-guid")]
    [InlineData("GET", "00000000-0000-0000-0000-000000000000")]
    [InlineData("PUT", "00000000-0000-0000-0000-000000000000")]
    [InlineData("DELETE", "00000000-0000-0000-0000-000000000000")]
    public async Task InvalidIdsConsistentlyReturn400(string method, string id)
    {
        await using var factory = new ApiFactory();
        using var client = await factory.CreateReadyClientAsync();
        using var request = new HttpRequestMessage(new HttpMethod(method), $"/api/users/{id}");
        if (method == "PUT") request.Content = JsonContent.Create(ValidUser());
        await AssertProblem(await client.SendAsync(request), HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task UnknownIdsReturn404(string method)
    {
        await using var factory = new ApiFactory();
        using var client = await factory.CreateReadyClientAsync();
        using var request = new HttpRequestMessage(new HttpMethod(method), $"/api/users/{Guid.NewGuid()}");
        if (method == "PUT") request.Content = JsonContent.Create(ValidUser());
        await AssertProblem(await client.SendAsync(request), HttpStatusCode.NotFound);
    }

    public static IEnumerable<object[]> InvalidBodies()
    {
        foreach (var method in new[] { "POST", "PUT" })
        {
            foreach (var body in new[] { "{}", "null", "{", "[]",
                "{\"firstName\":null,\"lastName\":\"Martin\",\"email\":\"a@b.com\",\"department\":\"IT\"}" })
                yield return new object[] { method, body };
            foreach (var field in new[] { "firstName", "lastName", "email", "department" })
            {
                foreach (var value in new[] { "", "   ", new string('x', field == "email" ? 255 : 101) })
                {
                    var fields = new Dictionary<string, string> { ["firstName"] = "Alice", ["lastName"] = "Martin",
                        ["email"] = "alice@example.com", ["department"] = "IT" };
                    fields[field] = value;
                    yield return new object[] { method, JsonSerializer.Serialize(fields) };
                }
            }
            foreach (var email in new[] { "not-an-email", "álîce@example.com" })
                yield return new object[] { method, JsonSerializer.Serialize(new { firstName = "Alice", lastName = "Martin", email, department = "IT" }) };
        }
    }

    [Theory]
    [MemberData(nameof(InvalidBodies))]
    public async Task InvalidInputReturns400AndDoesNotChangeDatabase(string method, string body)
    {
        await using var factory = new ApiFactory();
        using var client = await factory.CreateReadyClientAsync();
        var created = await client.PostAsJsonAsync("/api/users", ValidUser());
        var original = (await created.Content.ReadFromJsonAsync<UserResponse>())!;
        using var request = new HttpRequestMessage(new HttpMethod(method),
            method == "POST" ? "/api/users" : $"/api/users/{original.Id}")
        { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        await AssertProblem(await client.SendAsync(request), HttpStatusCode.BadRequest);
        var saved = (await client.GetFromJsonAsync<UserResponse[]>("/api/users"))!;
        Assert.Equal(original, Assert.Single(saved));
    }

    [Fact]
    public async Task DuplicateCreateAndUpdateReturn409AndPreserveRecords()
    {
        await using var factory = new ApiFactory();
        using var client = await factory.CreateReadyClientAsync();
        await client.PostAsJsonAsync("/api/users", ValidUser());
        await AssertProblem(await client.PostAsJsonAsync("/api/users", ValidUser(" ALICE@example.com ")), HttpStatusCode.Conflict);
        var other = await client.PostAsJsonAsync("/api/users", ValidUser("bob@example.com"));
        var bob = (await other.Content.ReadFromJsonAsync<UserResponse>())!;
        await AssertProblem(await client.PutAsJsonAsync($"/api/users/{bob.Id}", ValidUser("ALICE@example.com")), HttpStatusCode.Conflict);
        Assert.Equal(bob, await client.GetFromJsonAsync<UserResponse>($"/api/users/{bob.Id}"));
        Assert.Equal(HttpStatusCode.NoContent,
            (await client.PutAsJsonAsync($"/api/users/{bob.Id}", ValidUser("BOB@example.com"))).StatusCode);
        await factory.WithDbAsync(async db => Assert.Equal(2, await db.Users.CountAsync()));
    }

    internal static async Task AssertProblem(HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal((int)expected, problem.RootElement.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(problem.RootElement.GetProperty("title").GetString()));
    }
}
