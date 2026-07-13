using System.Text.Json;
using Exceptionless.Tests.Extensions;
using Xunit;

namespace Exceptionless.Tests.Controllers;

public class OpenApiControllerTests : IntegrationTestsBase
{
    public OpenApiControllerTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
    }

    [Fact]
    public async Task GetOpenApiJson_Default_ReturnsExpectedBaseline()
    {
        // Arrange
        string baselinePath = Path.Combine(AppContext.BaseDirectory, "Controllers", "Data", "openapi.json");

        // Act
        var response = await SendRequestAsync(r => r
            .BaseUri(_server.BaseAddress)
            .AppendPaths("docs", "v2", "openapi.json")
            .StatusCodeShouldBeOk()
        );

        string actualJson = await response.Content.ReadAsStringAsync(TestCancellationToken);

        // Set UPDATE_SNAPSHOTS=true to regenerate the baseline file.
        if (String.Equals(Environment.GetEnvironmentVariable("UPDATE_SNAPSHOTS"), "true", StringComparison.OrdinalIgnoreCase))
        {
            // Write to the source tree so the change produces a real git diff.
            string sourcePath = Path.GetFullPath(Path.Join(AppContext.BaseDirectory, "..", "..", "..", "Controllers", "Data", "openapi.json"));
            await File.WriteAllTextAsync(sourcePath, actualJson, TestCancellationToken);

            return;
        }

        // Assert
        string expectedJson = NormalizeOpenApiJson(await File.ReadAllTextAsync(baselinePath, TestCancellationToken));
        actualJson = NormalizeOpenApiJson(actualJson);

        Assert.Equal(expectedJson, actualJson);
    }

    [Fact]
    public async Task GetOpenApiJson_V3_ReturnsExpectedBaseline()
    {
        string baselinePath = Path.Combine(AppContext.BaseDirectory, "Controllers", "Data", "openapi-v3.json");
        var response = await SendRequestAsync(r => r
            .BaseUri(_server.BaseAddress)
            .AppendPaths("docs", "v3", "openapi.json")
            .StatusCodeShouldBeOk());
        string actualJson = await response.Content.ReadAsStringAsync(TestCancellationToken);

        if (String.Equals(Environment.GetEnvironmentVariable("UPDATE_SNAPSHOTS"), "true", StringComparison.OrdinalIgnoreCase))
        {
            string sourcePath = Path.GetFullPath(Path.Join(AppContext.BaseDirectory, "..", "..", "..", "Controllers", "Data", "openapi-v3.json"));
            await File.WriteAllTextAsync(sourcePath, actualJson, TestCancellationToken);
            return;
        }

        string expectedJson = NormalizeOpenApiJson(await File.ReadAllTextAsync(baselinePath, TestCancellationToken));
        Assert.Equal(expectedJson, NormalizeOpenApiJson(actualJson));
    }

    [Fact]
    public async Task GetOpenApiJson_ContainsExpectedRoutesOperationsAndResponses()
    {
        using var document = await GetOpenApiDocumentAsync("v2");
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

        Assert.False(paths.TryGetProperty("/api/v3/events", out _));

        using var v3Document = await GetOpenApiDocumentAsync("v3");
        var v3Paths = v3Document.RootElement.GetProperty("paths");
        Assert.True(v3Paths.TryGetProperty("/api/v3/events", out var ingestionPath));
        Assert.True(ingestionPath.TryGetProperty("post", out var ingestionPost));
        Assert.True(ingestionPost.TryGetProperty("requestBody", out var ingestionRequestBody));
        Assert.True(ingestionRequestBody.GetProperty("content").TryGetProperty("application/x-ndjson", out _));
        AssertResponseCodes(ingestionPost, "200", "400", "401", "402", "403", "404", "413", "415", "422", "429", "503");

        Assert.True(v3Paths.TryGetProperty("/api/v3/projects/{projectId}/events", out var projectIngestionPath));
        Assert.True(projectIngestionPath.TryGetProperty("post", out _));
        Assert.Equal(2, v3Paths.EnumerateObject().Count());
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

    private static string NormalizeOpenApiJson(string json)
    {
        return json
            .ReplaceLineEndings("\n")
            .Replace("\\r\\n", "\\n")
            .TrimEnd('\n');
    }

    private async Task<JsonDocument> GetOpenApiDocumentAsync(string documentName = "v2")
    {
        var response = await SendRequestAsync(r => r
            .BaseUri(_server.BaseAddress)
            .AppendPaths("docs", documentName, "openapi.json")
            .StatusCodeShouldBeOk()
        );

        string json = await response.Content.ReadAsStringAsync(TestCancellationToken);
        return JsonDocument.Parse(json);
    }

    private static void AssertResponseCodes(JsonElement operation, params string[] expectedStatusCodes)
    {
        var responses = operation.GetProperty("responses");
        foreach (string statusCode in expectedStatusCodes)
            Assert.True(responses.TryGetProperty(statusCode, out _), $"Expected response status code '{statusCode}'.");
    }
}
