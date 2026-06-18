using System.Text.Json;
using Exceptionless.Core.Models;
using Exceptionless.Web.Models;
using Foundatio.Xunit;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;
using Xunit;

namespace Exceptionless.Tests.Serializer;

/// <summary>
/// Tests for <see cref="JsonNamingPolicy.SnakeCaseLower"/> serialization behavior.
/// </summary>
/// <remarks>
/// The serializer uses the built-in STJ policy. Model properties that need a different
/// legacy wire name should declare it with <see cref="System.Text.Json.Serialization.JsonPropertyNameAttribute"/>.
/// </remarks>
public class SnakeCaseLowerNamingPolicyTests : TestWithLoggingBase
{
    private readonly JsonSerializerOptions _options;

    public SnakeCaseLowerNamingPolicyTests(ITestOutputHelper output) : base(output)
    {
        _options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };
    }

    [Fact]
    public void NamingPolicy_AppOptionsProperties_SerializesCorrectly()
    {
        // Arrange — representative of AppOptions-style properties.
        var model = new AppOptionsModel
        {
            BaseURL = "https://example.com",
            EnableSSL = true,
            MaximumRetentionDays = 180,
            WebsiteMode = "production"
        };

        // Act
        string json = JsonSerializer.Serialize(model, _options);

        // Assert
        /* language=json */
        const string expected = """{"base_url":"https://example.com","enable_ssl":true,"maximum_retention_days":180,"website_mode":"production"}""";
        Assert.Equal(expected, json);
    }

    [Fact]
    public void NamingPolicy_EnvironmentProperties_SerializesCorrectly()
    {
        // Arrange — representative of EnvironmentInfo-style properties on a plain model.
        // The production EnvironmentInfo model uses [JsonPropertyName("o_s_name")] where the
        // legacy wire name intentionally differs from SnakeCaseLower.
        var model = new EnvironmentModel
        {
            OSName = "Windows 11",
            OSVersion = "10.0.22621",
            IPAddress = "192.168.1.100",
            MachineName = "TEST-MACHINE"
        };

        // Act
        string json = JsonSerializer.Serialize(model, _options);

        // Assert — raw model without attribute overrides
        /* language=json */
        const string expected = """{"os_name":"Windows 11","os_version":"10.0.22621","ip_address":"192.168.1.100","machine_name":"TEST-MACHINE"}""";
        Assert.Equal(expected, json);
    }

    [Fact]
    public void NamingPolicy_OSPropertiesWithJsonPropertyNameOverride_PreserveLegacyFieldNames()
    {
        // Arrange — EnvironmentInfo.OSName/OSVersion use [JsonPropertyName("o_s_name")]
        // to preserve the legacy Elasticsearch field name.
        var env = new EnvironmentInfoOverrideModel
        {
            OSName = "Windows 11",
            OSVersion = "10.0.22621"
        };

        // Act
        string json = JsonSerializer.Serialize(env, _options);

        // Assert — attribute wins over naming policy
        /* language=json */
        const string expected = """{"o_s_name":"Windows 11","o_s_version":"10.0.22621"}""";
        Assert.Equal(expected, json);
    }

    [Fact]
    public void NamingPolicy_OAuthAccounts_ProducesClientCompatibleFieldName()
    {
        // Arrange — the Angular client reads vm.user.o_auth_accounts.
        var model = new OAuthAccountsModel
        {
            OAuthAccounts = ["github"]
        };

        // Act
        string json = JsonSerializer.Serialize(model, _options);

        // Assert
        /* language=json */
        const string expected = """{"o_auth_accounts":["github"]}""";
        Assert.Equal(expected, json);
    }

    [Fact]
    public void ExternalAuthInfo_Serialize_UsesCamelCaseJsonPropertyNames()
    {
        // Arrange — ExternalAuthInfo uses explicit [JsonPropertyName] camelCase attributes
        // independent of the naming policy.
        var authInfo = new ExternalAuthInfo
        {
            ClientId = "test-client",
            Code = "auth-code",
            RedirectUri = "https://example.com/callback",
            InviteToken = "token123"
        };

        // Act
        string json = JsonSerializer.Serialize(authInfo, _options);

        // Assert
        /* language=json */
        const string expected = """{"clientId":"test-client","code":"auth-code","redirectUri":"https://example.com/callback","inviteToken":"token123"}""";
        Assert.Equal(expected, json);
    }

    [Fact]
    public void ExternalAuthInfo_Deserialize_ParsesCamelCaseJson()
    {
        // Arrange
        /* language=json */
        const string json = """{"clientId": "my-client", "code": "my-code", "redirectUri": "https://test.com"}""";

        // Act
        var authInfo = JsonSerializer.Deserialize<ExternalAuthInfo>(json, _options);

        // Assert
        Assert.NotNull(authInfo);
        Assert.Equal("my-client", authInfo.ClientId);
        Assert.Equal("my-code", authInfo.Code);
        Assert.Equal("https://test.com", authInfo.RedirectUri);
        Assert.Null(authInfo.InviteToken);
    }

    [Fact]
    public void JsonPatch_Deserialize_SnakeCasePaths_ResolvesCorrectly()
    {
        // Arrange
        /* language=json */
        const string json = """[{"op": "replace", "path": "/data", "value": "TestValue"}, {"op": "replace", "path": "/is_active", "value": true}]""";

        // Act
        var patch = JsonSerializer.Deserialize<JsonPatchDocument<SimpleModel>>(json, _options);

        // Assert
        Assert.NotNull(patch);
        Assert.Equal(2, patch.Operations.Count);
        Assert.Equal("/data", patch.Operations[0].path);
        Assert.Equal("/is_active", patch.Operations[1].path);
    }

    [Fact]
    public void JsonPatch_ApplyTo_SnakeCasePaths_SetsPropertyValues()
    {
        // Arrange
        /* language=json */
        const string json = """[{"op": "replace", "path": "/is_active", "value": false}]""";
        var patch = JsonSerializer.Deserialize<JsonPatchDocument<SimpleModel>>(json, _options)!;
        var model = new SimpleModel { Data = "Original", IsActive = true };

        // Act
        patch.ApplyTo(model, _ => { });

        // Assert
        Assert.False(model.IsActive);
        Assert.Equal("Original", model.Data); // Unchanged property preserved
    }

    [Fact]
    public void StackStatus_Serialize_UsesStringValue()
    {
        // Arrange
        var model = new StackStatusModel { Status = StackStatus.Fixed };

        // Act
        string json = JsonSerializer.Serialize(model, _options);

        // Assert
        /* language=json */
        const string expected = """{"status":"fixed"}""";
        Assert.Equal(expected, json);
    }

    [Fact]
    public void StackStatus_Deserialize_ParsesStringValue()
    {
        // Arrange
        /* language=json */
        const string json = """{"status": "regressed"}""";

        // Act
        var model = JsonSerializer.Deserialize<StackStatusModel>(json, _options);

        // Assert
        Assert.NotNull(model);
        Assert.Equal(StackStatus.Regressed, model.Status);
    }

    private class AppOptionsModel
    {
        public string? BaseURL { get; set; }
        public bool EnableSSL { get; set; }
        public int MaximumRetentionDays { get; set; }
        public string? WebsiteMode { get; set; }
    }

    private class EnvironmentModel
    {
        public string? OSName { get; set; }
        public string? OSVersion { get; set; }
        public string? IPAddress { get; set; }
        public string? MachineName { get; set; }
    }

    /// <summary>
    /// Mirrors the [JsonPropertyName] overrides on EnvironmentInfo.OSName/OSVersion.
    /// </summary>
    private class EnvironmentInfoOverrideModel
    {
        [System.Text.Json.Serialization.JsonPropertyName("o_s_name")]
        public string? OSName { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("o_s_version")]
        public string? OSVersion { get; set; }
    }

    private class OAuthAccountsModel
    {
        public List<string> OAuthAccounts { get; set; } = [];
    }

    private class SimpleModel
    {
        public string? Data { get; set; }
        public bool IsActive { get; set; }
    }

    private class StackStatusModel
    {
        public StackStatus Status { get; set; }
    }
}
