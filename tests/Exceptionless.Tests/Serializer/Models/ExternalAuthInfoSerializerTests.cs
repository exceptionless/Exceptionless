using Exceptionless.Web.Models;
using Foundatio.Serializer;
using Xunit;

namespace Exceptionless.Tests.Serializer.Models;

/// <summary>
/// Tests ExternalAuthInfo serialization using ITextSerializer.
/// ExternalAuthInfo uses camelCase naming (via JsonObject attribute) instead of snake_case.
/// </summary>
public class ExternalAuthInfoSerializerTests : TestWithServices
{
    private readonly ITextSerializer _serializer;

    public ExternalAuthInfoSerializerTests(ITestOutputHelper output) : base(output)
    {
        _serializer = GetService<ITextSerializer>();
    }

    [Fact]
    public void SerializeToString_CompleteAuthInfo_UsesCamelCaseNaming()
    {
        // Arrange
        var authInfo = new ExternalAuthInfo
        {
            ClientId = "test-client",
            Code = "auth-code",
            RedirectUri = "https://example.com/callback",
            InviteToken = "token123"
        };

        /* language=json */
        const string expectedJson = """{"clientId":"test-client","code":"auth-code","redirectUri":"https://example.com/callback","inviteToken":"token123"}""";

        // Act
        string? json = _serializer.SerializeToString(authInfo);

        // Assert
        Assert.Equal(expectedJson, json);
    }

    [Fact]
    public void SerializeToString_WithoutInviteToken_OmitsNullProperty()
    {
        // Arrange
        var authInfo = new ExternalAuthInfo
        {
            ClientId = "test-client",
            Code = "auth-code",
            RedirectUri = "https://example.com/callback"
        };

        /* language=json */
        const string expectedJson = """{"clientId":"test-client","code":"auth-code","redirectUri":"https://example.com/callback"}""";

        // Act
        string? json = _serializer.SerializeToString(authInfo);

        // Assert
        Assert.Equal(expectedJson, json);
    }

    [Fact]
    public void Deserialize_CamelCaseJson_ParsesAllProperties()
    {
        // Arrange
        /* language=json */
        const string json = """{"clientId":"my-client","code":"my-code","redirectUri":"https://localhost","inviteToken":"invite123"}""";

        // Act
        var authInfo = _serializer.Deserialize<ExternalAuthInfo>(json);

        // Assert
        Assert.NotNull(authInfo);
        Assert.Equal("my-client", authInfo.ClientId);
        Assert.Equal("my-code", authInfo.Code);
        Assert.Equal("https://localhost", authInfo.RedirectUri);
        Assert.Equal("invite123", authInfo.InviteToken);
    }

    [Fact]
    public void Deserialize_WithoutInviteToken_SetsPropertyToNull()
    {
        // Arrange
        /* language=json */
        const string json = """{"clientId":"my-client","code":"my-code","redirectUri":"https://localhost"}""";

        // Act
        var authInfo = _serializer.Deserialize<ExternalAuthInfo>(json);

        // Assert
        Assert.NotNull(authInfo);
        Assert.Equal("my-client", authInfo.ClientId);
        Assert.Equal("my-code", authInfo.Code);
        Assert.Equal("https://localhost", authInfo.RedirectUri);
        Assert.Null(authInfo.InviteToken);
    }

    [Fact]
    public void Deserialize_RoundTrip_PreservesAllProperties()
    {
        // Arrange
        var original = new ExternalAuthInfo
        {
            ClientId = "client-rt",
            Code = "code-rt",
            RedirectUri = "https://app.local/oauth",
            InviteToken = "invite-rt"
        };

        // Act
        string? json = _serializer.SerializeToString(original);
        var deserialized = _serializer.Deserialize<ExternalAuthInfo>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(original.ClientId, deserialized.ClientId);
        Assert.Equal(original.Code, deserialized.Code);
        Assert.Equal(original.RedirectUri, deserialized.RedirectUri);
        Assert.Equal(original.InviteToken, deserialized.InviteToken);
    }

    [Fact]
    public void Deserialize_SpecialCharactersInValues_PreservesData()
    {
        // Arrange
        var original = new ExternalAuthInfo
        {
            ClientId = "client-with-special-chars-!@#$%",
            Code = "code=with+encoded&chars",
            RedirectUri = "https://example.com/callback?state=abc&nonce=123",
            InviteToken = "token/with/slashes"
        };

        // Act
        string? json = _serializer.SerializeToString(original);
        var deserialized = _serializer.Deserialize<ExternalAuthInfo>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(original.ClientId, deserialized.ClientId);
        Assert.Equal(original.Code, deserialized.Code);
        Assert.Equal(original.RedirectUri, deserialized.RedirectUri);
        Assert.Equal(original.InviteToken, deserialized.InviteToken);
    }
}
