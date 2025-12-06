using Exceptionless.Core.Models;
using Exceptionless.Web.Models;
using Foundatio.Serializer;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Tests.Serializer;

/// <summary>
/// Tests domain model serialization using ITextSerializer.
/// Note: ITextSerializer currently uses Newtonsoft.Json with our custom settings (snake_case, etc.)
/// </summary>
public class ModelTests : TestWithServices
{
    public ModelTests(ITestOutputHelper output) : base(output) { }

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

        var serializer = GetService<ITextSerializer>();
        string json = serializer.SerializeToString(authInfo);
        Assert.Equal("{\"clientId\":\"test-client\",\"code\":\"auth-code\",\"redirectUri\":\"https://example.com/callback\",\"inviteToken\":\"token123\"}", json);
    }

    [Fact]
    public void ExternalAuthInfo_Deserialize_ParsesCamelCaseJson()
    {
        /* language=json */
        const string json = """{"clientId":"my-client","code":"my-code","redirectUri":"https://localhost"}""";

        var serializer = GetService<ITextSerializer>();
        var authInfo = serializer.Deserialize<ExternalAuthInfo>(json);

        Assert.NotNull(authInfo);
        Assert.Equal("my-client", authInfo.ClientId);
        Assert.Equal("my-code", authInfo.Code);
        Assert.Equal("https://localhost", authInfo.RedirectUri);
        Assert.Null(authInfo.InviteToken);
    }

    [Fact]
    public void StackStatus_Serialize_UsesStringValue()
    {
        var stack = new Stack { Status = StackStatus.Fixed };

        var serializer = GetService<ITextSerializer>();
        string json = serializer.SerializeToString(stack);

        Assert.Contains("\"status\":\"fixed\"", json);
    }

    [Fact]
    public void StackStatus_Deserialize_ParsesStringValue()
    {
        /* language=json */
        const string json = """{"status":"regressed"}""";

        var serializer = GetService<ITextSerializer>();
        var model = serializer.Deserialize<Stack>(json);

        Assert.NotNull(model);
        Assert.Equal(StackStatus.Regressed, model.Status);
    }

    [Fact]
    public void WebHook_Serialize_UsesSnakeCaseProperties()
    {
        var hook = new WebHook
        {
            Id = "test",
            EventTypes = ["NewError"],
            Version = WebHook.KnownVersions.Version2
        };

        var serializer = GetService<ITextSerializer>();
        string json = serializer.SerializeToString(hook);
        Assert.Equal("{\"id\":\"test\",\"event_types\":[\"NewError\"],\"is_enabled\":true,\"version\":\"v2\",\"created_utc\":\"0001-01-01T00:00:00\"}", json);
    }
}
