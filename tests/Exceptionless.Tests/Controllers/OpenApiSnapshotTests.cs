using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Xunit;

namespace Exceptionless.Tests.Controllers;

public sealed class OpenApiSnapshotTests
{
    [Fact]
    public async Task GetOpenApiJson_Default_MatchesSnapshot()
    {
        // Act
        string actualJson = await GetOpenApiJsonAsync();

        // Set UPDATE_SNAPSHOTS=true to regenerate the baseline file.
        if (String.Equals(Environment.GetEnvironmentVariable("UPDATE_SNAPSHOTS"), "true", StringComparison.OrdinalIgnoreCase))
        {
            // Write to the source tree so the change produces a real git diff.
            string sourcePath = Path.GetFullPath(Path.Join(AppContext.BaseDirectory, "..", "..", "..", "Controllers", "Data", "openapi.json"));
            await File.WriteAllTextAsync(sourcePath, actualJson, TestContext.Current.CancellationToken);

            return;
        }

        // Assert
        await SnapshotTestHelper.AssertMatchesJsonSnapshotAsync("openapi.json", actualJson, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetOpenApiJson_ContainsExpectedRoutesOperationsAndResponses()
    {
        using var document = await GetOpenApiDocumentAsync();
        var paths = document.RootElement.GetProperty("paths");

        Assert.True(paths.TryGetProperty("/api/v2/auth/login", out var loginPath));
        Assert.True(loginPath.TryGetProperty("post", out var loginPost));
        Assert.True(loginPost.TryGetProperty("requestBody", out _));
        AssertResponseCodes(loginPost, "200", "401", "422");

        Assert.True(paths.TryGetProperty("/api/v2/auth/logout", out var logoutPath));
        Assert.True(logoutPath.TryGetProperty("get", out var logoutGet));
        AssertResponseCodes(logoutGet, "200", "401", "403");

        Assert.True(paths.TryGetProperty("/api/v2/projects", out var projectsPath));
        Assert.True(projectsPath.TryGetProperty("post", out var projectsPost));
        Assert.True(projectsPost.TryGetProperty("requestBody", out _));
        AssertResponseCodes(projectsPost, "201");

        Assert.True(paths.TryGetProperty("/api/v2/events/by-ref/{referenceId}/user-description", out var userDescriptionPath));
        Assert.True(userDescriptionPath.TryGetProperty("post", out var userDescriptionPost));
        Assert.True(userDescriptionPost.TryGetProperty("requestBody", out _));
        AssertResponseCodes(userDescriptionPost, "202");

        Assert.True(paths.TryGetProperty("/api/v2/events", out var eventsPath));
        Assert.True(eventsPath.TryGetProperty("post", out var eventsPost));
        AssertRequestBodyContent(eventsPost, "application/json", "text/plain");

        Assert.True(paths.TryGetProperty("/api/v2/projects/{projectId}/events", out var projectEventsPath));
        Assert.True(projectEventsPath.TryGetProperty("post", out var projectEventsPost));
        AssertRequestBodyContent(projectEventsPost, "application/json", "text/plain");
    }

    [Fact]
    public async Task GetOpenApiJson_ContainsExpectedSchemasAndSecuritySchemes()
    {
        using var document = await GetOpenApiDocumentAsync();
        var components = document.RootElement.GetProperty("components");
        var schemas = components.GetProperty("schemas");
        var securitySchemes = components.GetProperty("securitySchemes");

        Assert.True(schemas.TryGetProperty("Login", out _));
        Assert.True(schemas.TryGetProperty("Signup", out _));
        Assert.True(schemas.TryGetProperty("NewProject", out _));
        Assert.True(schemas.TryGetProperty("TokenResult", out _));
        Assert.True(schemas.TryGetProperty("ViewOrganization", out _));

        Assert.True(securitySchemes.TryGetProperty("Basic", out var basic));
        Assert.Equal("http", basic.GetProperty("type").GetString());
        Assert.Equal("basic", basic.GetProperty("scheme").GetString());

        Assert.True(securitySchemes.TryGetProperty("Bearer", out var bearer));
        Assert.Equal("http", bearer.GetProperty("type").GetString());
        Assert.Equal("bearer", bearer.GetProperty("scheme").GetString());

        Assert.True(securitySchemes.TryGetProperty("Token", out var token));
        Assert.Equal("apiKey", token.GetProperty("type").GetString());
        Assert.Equal("access_token", token.GetProperty("name").GetString());
    }

    private static async Task<JsonDocument> GetOpenApiDocumentAsync()
    {
        string json = await GetOpenApiJsonAsync();
        return JsonDocument.Parse(json);
    }

    private static async Task<string> GetOpenApiJsonAsync()
    {
        await using var app = MinimalApiTestApp.Create(useTestServer: true, includeOpenApi: true);
        await app.StartAsync(TestContext.Current.CancellationToken);

        var client = app.GetTestClient();
        client.BaseAddress = new Uri("http://localhost");

        using var response = await client.GetAsync("/docs/v2/openapi.json", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return SnapshotTestHelper.NormalizeJson(json);
    }

    private static void AssertResponseCodes(JsonElement operation, params string[] expectedStatusCodes)
    {
        var responses = operation.GetProperty("responses");
        foreach (string statusCode in expectedStatusCodes)
            Assert.True(responses.TryGetProperty(statusCode, out _), $"Expected response status code '{statusCode}'.");
    }

    private static void AssertRequestBodyContent(JsonElement operation, params string[] expectedContentTypes)
    {
        Assert.True(operation.TryGetProperty("requestBody", out var requestBody), "Expected request body.");
        var content = requestBody.GetProperty("content");
        foreach (string contentType in expectedContentTypes)
            Assert.True(content.TryGetProperty(contentType, out _), $"Expected request body content type '{contentType}'.");
    }
}
