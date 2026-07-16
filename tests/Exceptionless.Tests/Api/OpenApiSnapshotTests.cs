using System.Text.Json;
using Exceptionless.Core.Models.Ingestion;
using Exceptionless.Web.Utility;
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
    public async Task GetOpenApiJson_V3_MatchesSnapshot()
    {
        string actualJson = await GetOpenApiJsonAsync("v3");

        if (String.Equals(Environment.GetEnvironmentVariable("UPDATE_SNAPSHOTS"), "true", StringComparison.OrdinalIgnoreCase))
        {
            string sourcePath = Path.GetFullPath(Path.Join(AppContext.BaseDirectory, "..", "..", "..", "Api", "Data", "openapi-v3.json"));
            await File.WriteAllTextAsync(sourcePath, actualJson, TestContext.Current.CancellationToken);
            return;
        }

        await SnapshotTestHelper.AssertMatchesJsonSnapshotAsync("openapi-v3.json", actualJson, TestContext.Current.CancellationToken);
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

        Assert.True(paths.TryGetProperty("/api/v2/projects/{projectId}/events", out var projectEventsPath));
        Assert.True(projectEventsPath.TryGetProperty("post", out var projectEventsPost));
        Assert.Contains(projectEventsPost.GetProperty("parameters").EnumerateArray(), parameter =>
            parameter.GetProperty("name").GetString() == Headers.TrackEventPost
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
    public async Task GetOpenApiJson_InternalIngestionFields_AreExcluded()
    {
        using var document = await GetOpenApiDocumentAsync();
        var persistentEvent = document.RootElement.GetProperty("components").GetProperty("schemas").GetProperty("PersistentEvent");
        string[] required = persistentEvent.GetProperty("required").EnumerateArray().Select(value => value.GetString()!).ToArray();
        var properties = persistentEvent.GetProperty("properties");
        Assert.DoesNotContain("is_regression", required);
        Assert.True(properties.GetProperty("is_regression").GetProperty("readOnly").GetBoolean());
        Assert.False(properties.TryGetProperty("ingestion_is_regression_candidate", out _));
        Assert.False(properties.TryGetProperty("ingestion_regression_fixed_in_version", out _));
        Assert.False(properties.TryGetProperty("ingestion_regression_date_fixed", out _));

        var stackProperties = document.RootElement.GetProperty("components").GetProperty("schemas").GetProperty("Stack").GetProperty("properties");
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
        string[] objectDataSchemas = ["EventIngestionV3Event", "EventIngestionV3User", "EventIngestionV3Request", "EventIngestionV3Environment"];

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
        Assert.Equal(EventIngestionV3Limits.MaximumMetadataValueLength, signatureData.GetProperty("additionalProperties").GetProperty("maxLength").GetInt32());
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
        AssertRequiredJsonRequestBody(paths, "/api/v2/organizations/{id}", "patch", "NewOrganization");
        AssertRequiredJsonRequestBody(paths, "/api/v2/organizations/{id}", "put", "NewOrganization");
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

    private async Task<JsonDocument> GetOpenApiDocumentAsync(string documentName = "v2")
    {
        string json = await GetOpenApiJsonAsync(documentName);
        return JsonDocument.Parse(json);
    }

    private async Task<string> GetOpenApiJsonAsync(string documentName = "v2")
    {
        await _factory.Server.WaitForReadyAsync();
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync($"/docs/{documentName}/openapi.json", TestContext.Current.CancellationToken);
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
}
