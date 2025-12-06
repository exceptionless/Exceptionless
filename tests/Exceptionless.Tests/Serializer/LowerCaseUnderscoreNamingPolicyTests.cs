using System.Text.Json;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Exceptionless.Web.Models;
using Exceptionless.Web.Utility;
using Foundatio.Xunit;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Tests.Serializer;

/// <summary>
/// Tests for LowerCaseUnderscoreNamingPolicy and System.Text.Json serialization for the API layer.
/// </summary>
public class LowerCaseUnderscoreNamingPolicyTests : TestWithLoggingBase
{
    public LowerCaseUnderscoreNamingPolicyTests(ITestOutputHelper output) : base(output) { }

    private static readonly JsonSerializerOptions ApiOptions = new()
    {
        PropertyNamingPolicy = LowerCaseUnderscoreNamingPolicy.Instance,
        Converters = { new DeltaJsonConverterFactory() }
    };

    [Fact]
    public void NamingPolicy_Instance_ReturnsSingleton()
    {
        var instance1 = LowerCaseUnderscoreNamingPolicy.Instance;
        var instance2 = LowerCaseUnderscoreNamingPolicy.Instance;

        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void NamingPolicy_AppOptionsProperties_SerializesCorrectly()
    {
        var model = new AppOptionsModel
        {
            BaseURL = "https://example.com",
            EnableSSL = true,
            MaximumRetentionDays = 180,
            WebsiteMode = "production"
        };

        string json = JsonSerializer.Serialize(model, ApiOptions);

        Assert.Contains("\"base_u_r_l\":\"https://example.com\"", json);
        Assert.Contains("\"enable_s_s_l\":true", json);
        Assert.Contains("\"maximum_retention_days\":180", json);
        Assert.Contains("\"website_mode\":\"production\"", json);
    }

    [Fact]
    public void NamingPolicy_EnvironmentProperties_SerializesCorrectly()
    {
        // Properties from event-serialization-input.json
        var model = new EnvironmentModel
        {
            OSName = "Windows 11",
            OSVersion = "10.0.22621",
            IPAddress = "192.168.1.100",
            MachineName = "TEST-MACHINE"
        };

        string json = JsonSerializer.Serialize(model, ApiOptions);

        Assert.Contains("\"o_s_name\":\"Windows 11\"", json);
        Assert.Contains("\"o_s_version\":\"10.0.22621\"", json);
        Assert.Contains("\"i_p_address\":\"192.168.1.100\"", json);
        Assert.Contains("\"machine_name\":\"TEST-MACHINE\"", json);
    }

    [Fact]
    public void ExternalAuthInfo_Serialize_UsesCamelCasePropertyNames()
    {
        var authInfo = new ExternalAuthInfo
        {
            ClientId = "test-client",
            Code = "auth-code",
            RedirectUri = "https://example.com/callback",
            InviteToken = "token123"
        };

        string json = JsonSerializer.Serialize(authInfo, ApiOptions);

        // ExternalAuthInfo uses explicit JsonPropertyName attributes (camelCase)
        Assert.Contains("\"clientId\":\"test-client\"", json);
        Assert.Contains("\"code\":\"auth-code\"", json);
        Assert.Contains("\"redirectUri\":\"https://example.com/callback\"", json);
        Assert.Contains("\"inviteToken\":\"token123\"", json);
    }

    [Fact]
    public void ExternalAuthInfo_Deserialize_ParsesCamelCaseJson()
    {
        string json = """{"clientId":"my-client","code":"my-code","redirectUri":"https://test.com"}""";

        var authInfo = JsonSerializer.Deserialize<ExternalAuthInfo>(json, ApiOptions);

        Assert.NotNull(authInfo);
        Assert.Equal("my-client", authInfo.ClientId);
        Assert.Equal("my-code", authInfo.Code);
        Assert.Equal("https://test.com", authInfo.RedirectUri);
        Assert.Null(authInfo.InviteToken);
    }

    [Fact]
    public void Delta_Deserialize_SnakeCaseJson_SetsPropertyValues()
    {
        string json = """{"data":"TestValue","is_active":true}""";

        var delta = JsonSerializer.Deserialize<Delta<SimpleModel>>(json, ApiOptions);

        Assert.NotNull(delta);
        Assert.True(delta.TryGetPropertyValue("Data", out object? dataValue));
        Assert.Equal("TestValue", dataValue);
        Assert.True(delta.TryGetPropertyValue("IsActive", out object? isActiveValue));
        Assert.Equal(true, isActiveValue);
    }

    [Fact]
    public void Delta_Deserialize_PartialUpdate_OnlyTracksProvidedProperties()
    {
        string json = """{"is_active":false}""";

        var delta = JsonSerializer.Deserialize<Delta<SimpleModel>>(json, ApiOptions);

        Assert.NotNull(delta);
        var changedProperties = delta.GetChangedPropertyNames();
        Assert.Single(changedProperties);
        Assert.Contains("IsActive", changedProperties);
    }

    [Fact]
    public void StackStatus_Serialize_UsesStringValue()
    {
        var stack = new StackStatusModel { Status = StackStatus.Fixed };

        string json = JsonSerializer.Serialize(stack, ApiOptions);

        Assert.Contains("\"status\":\"fixed\"", json);
    }

    [Fact]
    public void StackStatus_Deserialize_ParsesStringValue()
    {
        string json = """{"status":"regressed"}""";

        var model = JsonSerializer.Deserialize<StackStatusModel>(json, ApiOptions);

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
