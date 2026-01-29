using System.Text.Json;
using Exceptionless.Core.Models;
using Exceptionless.Core.Serialization;
using Exceptionless.Web.Models;
using Exceptionless.Web.Utility;
using Foundatio.Xunit;
using Xunit;

namespace Exceptionless.Tests.Serializer;

/// <summary>
/// Tests for LowerCaseUnderscoreNamingPolicy and System.Text.Json serialization for the API layer.
/// </summary>
public class LowerCaseUnderscoreNamingPolicyTests : TestWithLoggingBase
{
    private readonly JsonSerializerOptions _jsonSerializerOptions;

    public LowerCaseUnderscoreNamingPolicyTests(ITestOutputHelper output) : base(output)
    {
        _jsonSerializerOptions = new()
        {
            PropertyNamingPolicy = LowerCaseUnderscoreNamingPolicy.Instance,
            Converters = { new DeltaJsonConverterFactory() }
        };
    }

    [Fact]
    public void NamingPolicy_Instance_ReturnsSingleton()
    {
        // Arrange
        var instance1 = LowerCaseUnderscoreNamingPolicy.Instance;

        // Act
        var instance2 = LowerCaseUnderscoreNamingPolicy.Instance;

        // Assert
        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void NamingPolicy_AppOptionsProperties_SerializesCorrectly()
    {
        // Arrange
        var model = new AppOptionsModel
        {
            BaseURL = "https://example.com",
            EnableSSL = true,
            MaximumRetentionDays = 180,
            WebsiteMode = "production"
        };

        // Act
        string json = JsonSerializer.Serialize(model, _jsonSerializerOptions);

        // Assert
        /* language=json */
        const string expected = """{"base_u_r_l":"https://example.com","enable_s_s_l":true,"maximum_retention_days":180,"website_mode":"production"}""";
        Assert.Equal(expected, json);
    }

    [Fact]
    public void NamingPolicy_EnvironmentProperties_SerializesCorrectly()
    {
        // Arrange
        // Properties from event-serialization-input.json
        var model = new EnvironmentModel
        {
            OSName = "Windows 11",
            OSVersion = "10.0.22621",
            IPAddress = "192.168.1.100",
            MachineName = "TEST-MACHINE"
        };

        // Act
        string json = JsonSerializer.Serialize(model, _jsonSerializerOptions);

        // Assert
        /* language=json */
        const string expected = """{"o_s_name":"Windows 11","o_s_version":"10.0.22621","i_p_address":"192.168.1.100","machine_name":"TEST-MACHINE"}""";
        Assert.Equal(expected, json);
    }

    [Fact]
    public void ExternalAuthInfo_Serialize_UsesCamelCasePropertyNames()
    {
        // Arrange
        var authInfo = new ExternalAuthInfo
        {
            ClientId = "test-client",
            Code = "auth-code",
            RedirectUri = "https://example.com/callback",
            InviteToken = "token123"
        };

        // Act
        string json = JsonSerializer.Serialize(authInfo, _jsonSerializerOptions);

        // Assert
        // ExternalAuthInfo uses explicit JsonPropertyName attributes (camelCase)
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
        var authInfo = JsonSerializer.Deserialize<ExternalAuthInfo>(json, _jsonSerializerOptions);

        // Assert
        Assert.NotNull(authInfo);
        Assert.Equal("my-client", authInfo.ClientId);
        Assert.Equal("my-code", authInfo.Code);
        Assert.Equal("https://test.com", authInfo.RedirectUri);
        Assert.Null(authInfo.InviteToken);
    }

    [Fact]
    public void Delta_Deserialize_SnakeCaseJson_SetsPropertyValues()
    {
        // Arrange
        /* language=json */
        const string json = """{"data": "TestValue", "is_active": true}""";

        // Act
        var delta = JsonSerializer.Deserialize<Delta<SimpleModel>>(json, _jsonSerializerOptions);

        // Assert
        Assert.NotNull(delta);
        Assert.True(delta.TryGetPropertyValue("Data", out object? dataValue));
        Assert.Equal("TestValue", dataValue);
        Assert.True(delta.TryGetPropertyValue("IsActive", out object? isActiveValue));
        Assert.True(isActiveValue as bool?);
    }

    [Fact]
    public void Delta_Deserialize_PartialUpdate_OnlyTracksProvidedProperties()
    {
        // Arrange
        /* language=json */
        const string json = """{"is_active": false}""";

        // Act
        var delta = JsonSerializer.Deserialize<Delta<SimpleModel>>(json, _jsonSerializerOptions);

        // Assert
        Assert.NotNull(delta);
        var changedProperties = delta.GetChangedPropertyNames();
        Assert.Single(changedProperties);
        Assert.Contains("IsActive", changedProperties);
    }

    [Fact]
    public void StackStatus_Serialize_UsesStringValue()
    {
        // Arrange
        var stack = new StackStatusModel { Status = StackStatus.Fixed };

        // Act
        string json = JsonSerializer.Serialize(stack, _jsonSerializerOptions);

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
        var model = JsonSerializer.Deserialize<StackStatusModel>(json, _jsonSerializerOptions);

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
