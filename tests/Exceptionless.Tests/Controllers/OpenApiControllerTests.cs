using System.Text.Json;
using Exceptionless.Core.Models.Ingestion;
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

        Assert.True(paths.TryGetProperty("/api/v2/projects/{projectId}/events", out var projectEventsPath));
        Assert.True(projectEventsPath.TryGetProperty("post", out var projectEventsPost));
        Assert.Contains(projectEventsPost.GetProperty("parameters").EnumerateArray(), parameter =>
            parameter.GetProperty("name").GetString() == "X-Exceptionless-Track-Event-Post"
            && parameter.GetProperty("in").GetString() == "header");

        Assert.False(paths.TryGetProperty("/api/v2/projects/{projectId}/events/posts/status", out _));

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
        Assert.False(v3Paths.TryGetProperty("/api/v3/projects/{projectId}/events/processing/status", out _));
        Assert.Equal(2, v3Paths.EnumerateObject().Count());

        var security = v3Document.RootElement.GetProperty("security");
        var requirement = Assert.Single(security.EnumerateArray());
        Assert.True(requirement.TryGetProperty("Bearer", out var scopes));
        Assert.Empty(scopes.EnumerateArray());
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

    [Fact]
    public async Task GetOpenApiJson_InternalIngestionFields_AreExcluded()
    {
        using var document = await GetOpenApiDocumentAsync();
        var persistentEvent = document.RootElement
            .GetProperty("components")
            .GetProperty("schemas")
            .GetProperty("PersistentEvent");
        string[] required = persistentEvent.GetProperty("required")
            .EnumerateArray()
            .Select(value => value.GetString()!)
            .ToArray();
        var properties = persistentEvent.GetProperty("properties");
        Assert.DoesNotContain("is_regression", required);
        Assert.True(properties.GetProperty("is_regression").GetProperty("readOnly").GetBoolean());
        Assert.False(properties.TryGetProperty("ingestion_is_regression_candidate", out _));
        Assert.False(properties.TryGetProperty("ingestion_regression_fixed_in_version", out _));
        Assert.False(properties.TryGetProperty("ingestion_regression_date_fixed", out _));

        var stackProperties = document.RootElement
            .GetProperty("components")
            .GetProperty("schemas")
            .GetProperty("Stack")
            .GetProperty("properties");
        Assert.False(stackProperties.TryGetProperty("ingestion_first_event_id", out _));
        Assert.False(stackProperties.TryGetProperty("ingestion_stack_usage_sequence", out _));

        Assert.Contains("is_first_occurrence", required);
        Assert.Contains("created_utc", required);
    }

    [Fact]
    public async Task GetOpenApiJson_V3ObjectMetadata_IsNullableObjectWithArbitraryProperties()
    {
        using var document = await GetOpenApiDocumentAsync("v3");
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        string[] objectDataSchemas =
        [
            "EventIngestionV3Event",
            "EventIngestionV3User",
            "EventIngestionV3Request",
            "EventIngestionV3Environment"
        ];

        foreach (string schemaName in objectDataSchemas)
        {
            var data = schemas.GetProperty(schemaName).GetProperty("properties").GetProperty("data");
            string[] types = data.GetProperty("type").EnumerateArray().Select(value => value.GetString()!).ToArray();
            Assert.Contains("object", types);
            Assert.Contains("null", types);
            Assert.True(data.TryGetProperty("additionalProperties", out var additionalProperties));
            Assert.Equal(JsonValueKind.Object, additionalProperties.ValueKind);
            Assert.False(data.TryGetProperty("oneOf", out _));
        }

        var postData = schemas.GetProperty("EventIngestionV3Request").GetProperty("properties").GetProperty("post_data");
        Assert.True(postData.TryGetProperty("oneOf", out _));
    }

    [Fact]
    public async Task GetOpenApiJson_V3StringLimits_MatchDurableModels()
    {
        using var document = await GetOpenApiDocumentAsync("v3");
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        var eventProperties = schemas.GetProperty("EventIngestionV3Event").GetProperty("properties");
        var message = eventProperties.GetProperty("message");
        var referenceId = eventProperties.GetProperty("reference_id");
        var tags = eventProperties.GetProperty("tags");
        var title = schemas.GetProperty("EventIngestionV3Stacking").GetProperty("properties").GetProperty("title");

        Assert.Equal(EventIngestionV3Limits.MaximumMessageLength, message.GetProperty("maxLength").GetInt32());
        Assert.Equal(EventIngestionV3Limits.MinimumReferenceIdLength, referenceId.GetProperty("minLength").GetInt32());
        Assert.Equal(EventIngestionV3Limits.MaximumReferenceIdLength, referenceId.GetProperty("maxLength").GetInt32());
        Assert.Equal(EventIngestionV3Limits.MaximumTags, tags.GetProperty("maxItems").GetInt32());
        Assert.Equal(EventIngestionV3Limits.MaximumTagLength, tags.GetProperty("items").GetProperty("maxLength").GetInt32());
        Assert.Equal(EventIngestionV3Limits.MaximumStackTitleLength, title.GetProperty("maxLength").GetInt32());

        var signatureData = schemas.GetProperty("EventIngestionV3Stacking").GetProperty("properties").GetProperty("signature_data");
        Assert.Equal(1, signatureData.GetProperty("minProperties").GetInt32());
        Assert.Equal(EventIngestionV3Limits.MaximumMetadataEntries, signatureData.GetProperty("maxProperties").GetInt32());
        Assert.Equal(
            EventIngestionV3Limits.MaximumMetadataValueLength,
            signatureData.GetProperty("additionalProperties").GetProperty("maxLength").GetInt32());
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
