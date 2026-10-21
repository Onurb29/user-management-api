using System.Net;
using System.Text.Json;
using Xunit;

namespace UserManagementAPI.Tests.Integration;

public sealed class DocumentationTests
{
    [Fact]
    public async Task DevelopmentOpenApiDescribesEveryRouteAndBearerRequirement()
    {
        await using var factory = new ApiFactory { EnvironmentName = "Development" };
        using var client = await factory.CreateReadyClientAsync(authenticated: false);
        var response = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("bearer", json.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer").GetProperty("scheme").GetString());
        foreach (var (path, method, success) in new[]
        {
            ("/api/users", "get", "200"), ("/api/users", "post", "201"),
            ("/api/users/{id}", "get", "200"), ("/api/users/{id}", "put", "204"), ("/api/users/{id}", "delete", "204")
        })
        {
            var operation = json.GetProperty("paths").GetProperty(path).GetProperty(method);
            Assert.True(operation.GetProperty("security")[0].TryGetProperty("Bearer", out _));
            foreach (var status in new[] { success, "400", "401", "403" })
                Assert.True(operation.GetProperty("responses").TryGetProperty(status, out _));
        }
    }

    [Fact]
    public async Task OpenApiIsNotExposedOutsideDevelopment()
    {
        await using var factory = new ApiFactory();
        using var client = await factory.CreateReadyClientAsync(authenticated: false);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/openapi/v1.json")).StatusCode);
    }
}
