using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Serilog.Core;
using Serilog.Events;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UserManagementAPI.Data;

namespace UserManagementAPI.Tests.Integration;

public class ApiFactory : WebApplicationFactory<Program>
{
    private readonly byte[] _signingKey = RandomNumberGenerator.GetBytes(32);
    public CapturedLogs Logs { get; } = new();
    public Action<IServiceCollection>? ConfigureServices { get; init; }
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"users-http-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:Schemes:Bearer:ValidIssuer"] = "test-issuer",
            ["Authentication:Schemes:Bearer:ValidAudiences:0"] = "test-audience",
            ["Authentication:Schemes:Bearer:SigningKeys:0:Issuer"] = "test-issuer",
            ["Authentication:Schemes:Bearer:SigningKeys:0:Value"] = Convert.ToBase64String(_signingKey)
        }));
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<AppDbContext>();
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite($"Data Source={_databasePath};Pooling=False"));
            services.AddSingleton<ILogEventSink>(Logs);
            ConfigureServices?.Invoke(services);
        });
    }

    public async Task<HttpClient> CreateReadyClientAsync(bool authenticated = true, string role = "IT")
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
        if (authenticated) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(role));
        return client;
    }

    public string CreateToken(string? role = "IT", string issuer = "test-issuer", string audience = "test-audience",
        bool expired = false, bool future = false, bool wrongKey = false, bool unsigned = false, bool noExpiration = false)
    {
        var now = DateTime.UtcNow;
        var claims = new List<Claim> { new("sub", "synthetic-test-user") };
        if (role is not null) claims.Add(new Claim("role", role));
        var credentials = unsigned ? null : new SigningCredentials(
            new SymmetricSecurityKey(wrongKey ? RandomNumberGenerator.GetBytes(32) : _signingKey), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(issuer, audience, claims,
            notBefore: future ? now.AddHours(1) : now.AddHours(-1),
            expires: future ? now.AddHours(2) : expired ? now.AddMinutes(-10) : now.AddHours(1),
            signingCredentials: credentials);
        if (noExpiration) token.Payload.Remove("exp");
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task WithDbAsync(Func<AppDbContext, Task> action)
    {
        using var scope = Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        File.Delete(_databasePath);
        File.Delete(_databasePath + "-wal");
        File.Delete(_databasePath + "-shm");
    }
}

public sealed class CapturedLogs : ILogEventSink
{
    public ConcurrentQueue<LogEvent> Events { get; } = new();
    public void Emit(LogEvent logEvent) => Events.Enqueue(logEvent);
}
