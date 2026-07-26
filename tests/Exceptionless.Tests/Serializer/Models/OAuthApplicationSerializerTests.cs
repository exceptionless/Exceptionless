using Exceptionless.Core.Models;
using Exceptionless.Core.Services;
using Foundatio.Serializer;
using Xunit;

namespace Exceptionless.Tests.Serializer.Models;

public sealed class OAuthApplicationSerializerTests : TestWithServices
{
    private readonly ITextSerializer _serializer;

    public OAuthApplicationSerializerTests(ITestOutputHelper output) : base(output)
    {
        _serializer = GetService<ITextSerializer>();
    }

    [Fact]
    public void Deserialize_MissingGrantTypes_DefaultsToLegacyAuthorizationCodeFlow()
    {
        const string json = """
            {
              "id": "650000000000000000000005",
              "client_id": "legacy-oauth-client",
              "name": "Legacy OAuth Client",
              "redirect_uris": ["https://example.com/oauth/callback"],
              "scopes": ["projects:read"],
              "created_by_user_id": "660000000000000000000001",
              "created_utc": "2026-01-01T00:00:00Z",
              "updated_utc": "2026-01-01T00:00:00Z"
            }
            """;

        var application = _serializer.Deserialize<OAuthApplication>(json);

        Assert.NotNull(application);
        Assert.Equal([OAuthGrantTypes.AuthorizationCode, OAuthGrantTypes.RefreshToken], application.GrantTypes);
    }
}
