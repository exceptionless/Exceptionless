using Exceptionless.Core.Authorization;
using Exceptionless.Core.Models;
using Exceptionless.Core.Services;
using Foundatio.Serializer;
using Xunit;

namespace Exceptionless.Tests.Serializer.Models;

public class OAuthTokenSerializerTests : TestWithServices
{
    private readonly ITextSerializer _serializer;

    public OAuthTokenSerializerTests(ITestOutputHelper output) : base(output)
    {
        _serializer = GetService<ITextSerializer>();
    }

    [Fact]
    public void RoundTrip_WithOAuthAccessToken_PreservesValues()
    {
        var token = new OAuthToken
        {
            Id = "650000000000000000000005",
            UserId = "660000000000000000000001",
            ClientId = "test-oauth-client",
            GrantId = "test-grant-id",
            Resource = "http://localhost:7110/api/v2",
            AccessTokenHash = OAuthService.CreateTokenHash("serializer-access-token"),
            RefreshTokenHash = OAuthService.CreateTokenHash("serializer-refresh-token"),
            RefreshExpiresUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            OrganizationIds = ["550000000000000000000001", "550000000000000000000002"],
            Scopes = [AuthorizationRoles.ProjectsRead],
            CreatedBy = "660000000000000000000001",
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };

        string? json = _serializer.SerializeToString(token);
        var result = _serializer.Deserialize<OAuthToken>(json);

        Assert.NotNull(result);
        Assert.Equal("test-oauth-client", result.ClientId);
        Assert.Equal("test-grant-id", result.GrantId);
        Assert.Equal("http://localhost:7110/api/v2", result.Resource);
        Assert.Equal(OAuthService.CreateTokenHash("serializer-access-token"), result.AccessTokenHash);
        Assert.Equal(OAuthService.CreateTokenHash("serializer-refresh-token"), result.RefreshTokenHash);
        Assert.Equal(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), result.RefreshExpiresUtc);
        Assert.Contains("550000000000000000000001", result.OrganizationIds);
        Assert.Contains("550000000000000000000002", result.OrganizationIds);
        Assert.Contains(AuthorizationRoles.ProjectsRead, result.Scopes);
    }
}
