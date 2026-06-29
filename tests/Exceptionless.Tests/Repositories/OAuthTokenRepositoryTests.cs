using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services;
using Exceptionless.Tests.Utility;
using Foundatio.Repositories;
using Foundatio.Repositories.Utility;
using Xunit;

namespace Exceptionless.Tests.Repositories;

public sealed class OAuthTokenRepositoryTests : IntegrationTestsBase
{
    private readonly IOAuthTokenRepository _repository;

    public OAuthTokenRepositoryTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _repository = GetService<IOAuthTokenRepository>();
    }

    [Fact]
    public async Task GetByOAuthTokenHashAsync_ReturnsOAuthAccessToken()
    {
        var utcNow = DateTime.UtcNow;
        string accessToken = StringExtensions.GetRandomString(OAuthService.OAuthTokenLength);
        string refreshToken = StringExtensions.GetRandomString(OAuthService.OAuthTokenLength);
        var token = new OAuthToken
        {
            Id = ObjectId.GenerateNewId().ToString(),
            UserId = TestConstants.UserId,
            ClientId = "repository-oauth-client",
            GrantId = StringExtensions.GetNewToken(),
            Resource = "http://localhost:7110/mcp",
            AccessTokenHash = OAuthService.CreateTokenHash(accessToken),
            RefreshTokenHash = OAuthService.CreateTokenHash(refreshToken),
            Scopes = [AuthorizationRoles.McpRead, AuthorizationRoles.OfflineAccess],
            OrganizationIds = [TestConstants.OrganizationId],
            CreatedBy = TestConstants.UserId,
            CreatedUtc = utcNow,
            UpdatedUtc = utcNow
        };

        await _repository.AddAsync(token, o => o.ImmediateConsistency());

        var accessResults = await _repository.GetByAccessTokenHashAsync(OAuthService.CreateTokenHash(accessToken), o => o.ImmediateConsistency());
        var refreshResults = await _repository.GetByRefreshTokenHashAsync(OAuthService.CreateTokenHash(refreshToken), o => o.ImmediateConsistency());
        var rawRefreshResults = await _repository.GetByRefreshTokenHashAsync(refreshToken, o => o.ImmediateConsistency());

        Assert.Equal(token.Id, Assert.Single(accessResults.Documents).Id);
        Assert.Equal(token.Id, Assert.Single(refreshResults.Documents).Id);
        Assert.Empty(rawRefreshResults.Documents);
    }
}