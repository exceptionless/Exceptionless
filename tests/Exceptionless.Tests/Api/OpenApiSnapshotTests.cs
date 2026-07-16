using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Xunit;

namespace Exceptionless.Tests.Api;

public sealed class OpenApiSnapshotTests : IClassFixture<AppWebHostFactory>
{
    private readonly AppWebHostFactory _factory;

    public OpenApiSnapshotTests(AppWebHostFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetOpenApiJson_Default_MatchesSnapshot()
    {
        // Arrange is handled by the production host in GetOpenApiJsonAsync.

        // Act
        string actualJson = await GetOpenApiJsonAsync();

        // Set UPDATE_SNAPSHOTS=true to regenerate the baseline file.
        if (String.Equals(Environment.GetEnvironmentVariable("UPDATE_SNAPSHOTS"), "true", StringComparison.OrdinalIgnoreCase))
        {
            // Write to the source tree so the change produces a real git diff.
            string sourcePath = Path.GetFullPath(Path.Join(AppContext.BaseDirectory, "..", "..", "..", "Api", "Data", "openapi.json"));
            await File.WriteAllTextAsync(sourcePath, actualJson, TestContext.Current.CancellationToken);

            return;
        }

        // Assert
        await SnapshotTestHelper.AssertMatchesJsonSnapshotAsync("openapi.json", actualJson, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetOpenApiJson_Default_ContainsExpectedRoutesOperationsAndResponses()
    {
        // Arrange
        using var document = await GetOpenApiDocumentAsync();

        // Act
        var paths = document.RootElement.GetProperty("paths");

        // Assert
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
    }

    [Fact]
    public async Task GetOpenApiJson_Default_ContainsExpectedSchemasAndSecuritySchemes()
    {
        // Arrange
        using var document = await GetOpenApiDocumentAsync();

        // Act
        var components = document.RootElement.GetProperty("components");
        var schemas = components.GetProperty("schemas");
        var securitySchemes = components.GetProperty("securitySchemes");

        // Assert
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
    public async Task GetOpenApiJson_MigratedContracts_PreserveControllerMetadata()
    {
        // Arrange
        using var document = await GetOpenApiDocumentAsync();

        // Act
        var paths = document.RootElement.GetProperty("paths");

        // Assert
        AssertArrayResponseSchema(paths, "/api/v2/events", "PersistentEvent");
        AssertArrayResponseSchema(paths, "/api/v2/organizations/{organizationId}/events", "PersistentEvent");
        AssertArrayResponseSchema(paths, "/api/v2/projects/{projectId}/events", "PersistentEvent");
        AssertArrayResponseSchema(paths, "/api/v2/stacks/{stackId}/events", "PersistentEvent");
        AssertArrayResponseSchema(paths, "/api/v2/events/by-ref/{referenceId}", "PersistentEvent");
        AssertArrayResponseSchema(paths, "/api/v2/projects/{projectId}/events/by-ref/{referenceId}", "PersistentEvent");
        AssertArrayResponseSchema(paths, "/api/v2/events/sessions/{sessionId}", "PersistentEvent");
        AssertArrayResponseSchema(paths, "/api/v2/projects/{projectId}/events/sessions/{sessionId}", "PersistentEvent");
        AssertArrayResponseSchema(paths, "/api/v2/events/sessions", "PersistentEvent");
        AssertArrayResponseSchema(paths, "/api/v2/organizations/{organizationId}/events/sessions", "PersistentEvent");
        AssertArrayResponseSchema(paths, "/api/v2/projects/{projectId}/events/sessions", "PersistentEvent");
        AssertArrayResponseSchema(paths, "/api/v2/stacks", "Stack");
        AssertArrayResponseSchema(paths, "/api/v2/organizations/{organizationId}/stacks", "Stack");
        AssertArrayResponseSchema(paths, "/api/v2/projects/{projectId}/stacks", "Stack");
        AssertArrayResponseSchema(paths, "/api/v2/organizations/{organizationId}/tokens", "ViewToken");
        AssertArrayResponseSchema(paths, "/api/v2/projects/{projectId}/tokens", "ViewToken");
        AssertArrayResponseSchema(paths, "/api/v2/projects/{projectId}/webhooks", "WebHook");

        AssertRequiredJsonRequestBody(paths, "/api/v1/error/{id}", "patch", "UpdateEvent");
        AssertRequiredJsonRequestBody(paths, "/api/v2/organizations/{id}", "patch", "UpdateOrganization");
        AssertRequiredJsonRequestBody(paths, "/api/v2/organizations/{id}", "put", "UpdateOrganization");
        AssertRequiredJsonRequestBody(paths, "/api/v2/projects/{id}", "patch", "UpdateProject");
        AssertRequiredJsonRequestBody(paths, "/api/v2/projects/{id}", "put", "UpdateProject");
        AssertRequiredJsonRequestBody(paths, "/api/v2/tokens/{id}", "patch", "UpdateToken");
        AssertRequiredJsonRequestBody(paths, "/api/v2/tokens/{id}", "put", "UpdateToken");
        AssertRequiredJsonRequestBody(paths, "/api/v2/saved-views/{id}", "patch", "UpdateSavedView");
        AssertRequiredJsonRequestBody(paths, "/api/v2/saved-views/{id}", "put", "UpdateSavedView");
        AssertRequiredJsonRequestBody(paths, "/api/v2/users/{id}", "patch", "UpdateUser");
        AssertRequiredJsonRequestBody(paths, "/api/v2/users/{id}", "put", "UpdateUser");

        AssertRequestContentTypes(paths, "/api/v1/error", "post", "application/json", "text/plain");
        AssertRequestContentTypes(paths, "/api/v1/events", "post", "application/json", "text/plain");
        AssertRequestContentTypes(paths, "/api/v1/projects/{projectId}/events", "post", "application/json", "text/plain");
        AssertRequestContentTypes(paths, "/api/v2/events", "post", "application/json", "text/plain");
        AssertRequestContentTypes(paths, "/api/v2/projects/{projectId}/events", "post", "application/json", "text/plain");
        AssertRequestContentTypes(paths, "/api/v2/events/by-ref/{referenceId}/user-description", "post", "application/json", "application/*+json");
        AssertRequestContentTypes(paths, "/api/v2/projects/{projectId}/events/by-ref/{referenceId}/user-description", "post", "application/json", "application/*+json");
        AssertRequestContentTypes(paths, "/api/v2/organizations", "post", "application/json", "application/*+json");
        AssertRequestContentTypes(paths, "/api/v2/organizations/{id}/change-plan", "post", "application/json", "application/*+json", "application/octet-stream", "text/json", "text/plain");
        AssertRequestContentTypes(paths, "/api/v2/organizations/{id}/data/{key}", "post", "application/json", "application/*+json");
        AssertRequestContentTypes(paths, "/api/v2/projects", "post", "application/json", "application/*+json");
        AssertRequestContentTypes(paths, "/api/v2/projects/{id}/config", "post", "application/json", "application/*+json");
        AssertRequestContentTypes(paths, "/api/v2/projects/{id}/data", "post", "application/json", "application/*+json");
        AssertRequestContentTypes(paths, "/api/v2/users/{userId}/projects/{id}/notifications", "put", "application/json", "application/*+json");
        AssertRequestContentTypes(paths, "/api/v2/users/{userId}/projects/{id}/notifications", "post", "application/json", "application/*+json");
        AssertRequestContentTypes(paths, "/api/v2/projects/{id}/{integration}/notifications", "put", "application/json", "application/*+json");
        AssertRequestContentTypes(paths, "/api/v2/projects/{id}/{integration}/notifications", "post", "application/json", "application/*+json");
        AssertRequestContentTypes(paths, "/api/v2/stacks/{id}/add-link", "post", "application/json", "application/*+json");
        AssertRequestContentTypes(paths, "/api/v2/stacks/{id}/remove-link", "post", "application/json", "application/*+json");
        AssertRequestContentTypes(paths, "/api/v2/tokens", "post", "application/json", "application/*+json");
        AssertRequestContentTypes(paths, "/api/v2/projects/{projectId}/tokens", "post", "application/json", "application/*+json");
        AssertRequestContentTypes(paths, "/api/v2/organizations/{organizationId}/tokens", "post", "application/json", "application/*+json");
        AssertRequestContentTypes(paths, "/api/v2/webhooks", "post", "application/json", "application/*+json");
        AssertRequestContentTypes(paths, "/api/v2/organizations/{organizationId}/saved-views", "post", "application/json", "application/*+json");
        AssertRequestContentTypes(paths, "/api/v2/saved-views/predefined", "put", "application/json", "application/*+json", "application/octet-stream", "text/json", "text/plain");
        AssertRequestContentTypes(paths, "/api/v2/oauth/authorize", "post", "application/json", "application/*+json", "application/octet-stream", "text/json", "text/plain");
        AssertRequestContentTypes(paths, "/api/v2/oauth/authorize/consent", "post", "application/json", "application/*+json", "application/octet-stream", "text/json", "text/plain");
        AssertRequestContentTypes(paths, "/api/v2/oauth/register", "post", "application/json", "application/*+json", "application/octet-stream", "text/json", "text/plain");

        AssertOperationTag(paths, "/.well-known/oauth-authorization-server", "OAuth");
        AssertOperationTag(paths, "/.well-known/oauth-protected-resource/mcp", "OAuth");
        AssertOperationTag(paths, "/.well-known/oauth-protected-resource/api/v2", "OAuth");

        AssertOptionalParameter(paths, "/api/v2/organizations/check-name", "get", "name");
        AssertOptionalParameter(paths, "/api/v2/organizations/{organizationId}/projects/check-name", "get", "name");
        AssertOptionalParameter(paths, "/api/v2/projects/check-name", "get", "name");
        AssertOptionalParameter(paths, "/api/v2/projects/{id}/config", "delete", "key");
        AssertOptionalParameter(paths, "/api/v2/projects/{id}/config", "post", "key");
        AssertOptionalParameter(paths, "/api/v2/projects/{id}/data", "delete", "key");
        AssertOptionalParameter(paths, "/api/v2/projects/{id}/data", "post", "key");
        AssertOptionalParameter(paths, "/api/v2/projects/{id}/promotedtabs", "delete", "name");
        AssertOptionalParameter(paths, "/api/v2/projects/{id}/promotedtabs", "post", "name");
        AssertOptionalParameter(paths, "/api/v2/projects/{id}/promotedtabs", "put", "name");
        AssertOptionalParameter(paths, "/api/v2/stacks/{ids}/change-status", "post", "status");
        AssertOptionalParameter(paths, "/api/v2/stacks/{ids}/mark-snoozed", "post", "snoozeUntilUtc");

        var eventById = paths.GetProperty("/api/v2/events/{id}").GetProperty("get");
        Assert.Contains(eventById.GetProperty("parameters").EnumerateArray(), parameter =>
            String.Equals(parameter.GetProperty("name").GetString(), "expected_stack_id", StringComparison.Ordinal));
        AssertResponseCodes(eventById, "200", "400", "404", "426");

        foreach (string path in new[] { "/api/v1/events", "/api/v1/projects/{projectId}/events", "/api/v2/events", "/api/v2/projects/{projectId}/events" })
        {
            var eventPost = paths.GetProperty(path).GetProperty("post");
            Assert.True(eventPost.GetProperty("requestBody").GetProperty("required").GetBoolean());
            AssertResponseCodes(eventPost, "202", "400", "404", "413");
        }
    }

    [Fact]
    public async Task GetOpenApiJson_DeltaNullableComplexProperties_PreserveSchemasAndAnnotations()
    {
        using var document = await GetOpenApiDocumentAsync();
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");

        AssertNullableReference(schemas, "UpdateOrganization", "budget_alert_settings", "OrganizationBudgetAlertSettings");
        AssertNullableReference(schemas, "UpdateProject", "ingest_limit", "ProjectIngestLimit");

        var columnOrderAlternatives = schemas
            .GetProperty("UpdateSavedView")
            .GetProperty("properties")
            .GetProperty("column_order")
            .GetProperty("oneOf")
            .EnumerateArray();
        var columnOrderArray = Assert.Single(columnOrderAlternatives, alternative =>
            alternative.TryGetProperty("type", out var type) && type.GetString() == "array");
        Assert.Equal(50, columnOrderArray.GetProperty("maxItems").GetInt32());
    }

    private async Task<JsonDocument> GetOpenApiDocumentAsync()
    {
        string json = await GetOpenApiJsonAsync();
        return JsonDocument.Parse(json);
    }

    private async Task<string> GetOpenApiJsonAsync()
    {
        await _factory.Server.WaitForReadyAsync();
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync("/docs/v2/openapi.json", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return SnapshotTestHelper.NormalizeJson(json);
    }

    private static void AssertResponseCodes(JsonElement operation, params string[] expectedStatusCodes)
    {
        var responses = operation.GetProperty("responses");
        foreach (string statusCode in expectedStatusCodes)
            Assert.True(responses.TryGetProperty(statusCode, out _), $"Expected response status code '{statusCode}'.");
    }

    private static void AssertArrayResponseSchema(JsonElement paths, string path, string expectedItemSchema)
    {
        var schema = paths.GetProperty(path)
            .GetProperty("get")
            .GetProperty("responses")
            .GetProperty("200")
            .GetProperty("content")
            .GetProperty("application/json")
            .GetProperty("schema");

        Assert.Equal("array", schema.GetProperty("type").GetString());
        Assert.Equal($"#/components/schemas/{expectedItemSchema}", schema.GetProperty("items").GetProperty("$ref").GetString());
    }

    private static void AssertOperationTag(JsonElement paths, string path, string expectedTag)
    {
        var tags = paths.GetProperty(path).GetProperty("get").GetProperty("tags");
        Assert.Equal(expectedTag, Assert.Single(tags.EnumerateArray()).GetString());
    }

    private static void AssertOptionalParameter(JsonElement paths, string path, string method, string parameterName)
    {
        var parameter = Assert.Single(paths.GetProperty(path)
            .GetProperty(method)
            .GetProperty("parameters")
            .EnumerateArray(),
            parameter => String.Equals(parameter.GetProperty("name").GetString(), parameterName, StringComparison.Ordinal));

        Assert.False(parameter.TryGetProperty("required", out var required) && required.GetBoolean());
    }

    private static void AssertRequestContentTypes(JsonElement paths, string path, string method, params string[] expectedContentTypes)
    {
        var content = paths.GetProperty(path)
            .GetProperty(method)
            .GetProperty("requestBody")
            .GetProperty("content");

        Assert.Equal(expectedContentTypes.Order(), content.EnumerateObject().Select(property => property.Name).Order());
    }

    private static void AssertRequiredJsonRequestBody(JsonElement paths, string path, string method, string expectedSchema)
    {
        var requestBody = paths.GetProperty(path).GetProperty(method).GetProperty("requestBody");
        Assert.True(requestBody.GetProperty("required").GetBoolean());

        var content = requestBody.GetProperty("content");
        Assert.Equal(["application/*+json", "application/json"], content.EnumerateObject().Select(property => property.Name).Order());

        foreach (var mediaType in content.EnumerateObject())
            Assert.Equal($"#/components/schemas/{expectedSchema}", mediaType.Value.GetProperty("schema").GetProperty("$ref").GetString());
    }

    private static void AssertNullableReference(JsonElement schemas, string schemaName, string propertyName, string referenceName)
    {
        Assert.True(schemas.TryGetProperty(referenceName, out _), $"Expected referenced schema '{referenceName}'.");

        var propertySchema = schemas
            .GetProperty(schemaName)
            .GetProperty("properties")
            .GetProperty(propertyName);
        var alternatives = propertySchema.GetProperty("oneOf").EnumerateArray().ToArray();

        Assert.Contains(alternatives, alternative => alternative.TryGetProperty("type", out var type) && type.GetString() == "null");
        Assert.Contains(alternatives, alternative =>
            alternative.TryGetProperty("$ref", out var reference) &&
            reference.GetString() == $"#/components/schemas/{referenceName}");
    }
}
