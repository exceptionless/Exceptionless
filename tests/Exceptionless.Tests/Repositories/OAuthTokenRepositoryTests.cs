using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services;
using Exceptionless.Tests.Utility;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;
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

    [Fact]
    public async Task RemoveExpiredDisabledAsync_DisabledExpiredTokens_RemovesOnlyExpiredRows()
    {
        var utcNow = DateTime.UtcNow;
        var cutoff = utcNow.Subtract(TimeSpan.FromDays(1));
        var expiredSpentToken = CreateOAuthToken(utcNow.AddDays(-40), isDisabled: true, refreshExpiresUtc: cutoff.AddMinutes(-1));
        var retainedSpentToken = CreateOAuthToken(utcNow.AddDays(-40), isDisabled: true, refreshExpiresUtc: cutoff.AddMinutes(1));
        var activeExpiredToken = CreateOAuthToken(utcNow.AddDays(-40), isDisabled: false, refreshExpiresUtc: cutoff.AddMinutes(-1));
        var expiredClearedRefreshToken = CreateOAuthToken(cutoff.AddMinutes(-1), isDisabled: true, refreshTokenHash: null, refreshExpiresUtc: null);
        var retainedClearedRefreshToken = CreateOAuthToken(cutoff.AddMinutes(1), isDisabled: true, refreshTokenHash: null, refreshExpiresUtc: null);
        var recentClearedExpiredRefreshToken = CreateOAuthToken(cutoff.AddMinutes(1), isDisabled: true, refreshTokenHash: null, refreshExpiresUtc: cutoff.AddMinutes(-1));

        await _repository.AddAsync([
            expiredSpentToken,
            retainedSpentToken,
            activeExpiredToken,
            expiredClearedRefreshToken,
            retainedClearedRefreshToken,
            recentClearedExpiredRefreshToken
        ], o => o.ImmediateConsistency());

        await _repository.PatchAsync(expiredClearedRefreshToken.Id, new PartialPatch(new { updated_utc = cutoff.AddMinutes(-1) }), o => o.ImmediateConsistency());
        await _repository.PatchAsync(retainedClearedRefreshToken.Id, new PartialPatch(new { updated_utc = cutoff.AddMinutes(1) }), o => o.ImmediateConsistency());
        await _repository.PatchAsync(recentClearedExpiredRefreshToken.Id, new PartialPatch(new { updated_utc = cutoff.AddMinutes(1) }), o => o.ImmediateConsistency());

        long removed = await _repository.RemoveExpiredDisabledAsync(cutoff, TestContext.Current.CancellationToken);

        Assert.Equal(2, removed);
        Assert.Null(await _repository.GetByIdAsync(expiredSpentToken.Id, o => o.ImmediateConsistency()));
        Assert.NotNull(await _repository.GetByIdAsync(retainedSpentToken.Id, o => o.ImmediateConsistency()));
        Assert.NotNull(await _repository.GetByIdAsync(activeExpiredToken.Id, o => o.ImmediateConsistency()));
        Assert.NotNull(await _repository.GetByIdAsync(recentClearedExpiredRefreshToken.Id, o => o.ImmediateConsistency()));
        Assert.Null(await _repository.GetByIdAsync(expiredClearedRefreshToken.Id, o => o.ImmediateConsistency()));
        Assert.NotNull(await _repository.GetByIdAsync(retainedClearedRefreshToken.Id, o => o.ImmediateConsistency()));
    }

    private static OAuthToken CreateOAuthToken(
        DateTime utcNow,
        bool isDisabled = false,
        string? refreshTokenHash = "refresh-token-hash",
        DateTime? refreshExpiresUtc = null,
        string? userId = null,
        string? clientId = null,
        string? grantId = null)
    {
        return new OAuthToken
        {
            Id = ObjectId.GenerateNewId().ToString(),
            UserId = userId ?? TestConstants.UserId,
            ClientId = clientId ?? "repository-oauth-client",
            GrantId = grantId ?? StringExtensions.GetNewToken(),
            Resource = "http://localhost:7110/mcp",
            AccessTokenHash = OAuthService.CreateTokenHash(StringExtensions.GetRandomString(OAuthService.OAuthTokenLength)),
            RefreshTokenHash = refreshTokenHash,
            Scopes = [AuthorizationRoles.McpRead, AuthorizationRoles.OfflineAccess],
            OrganizationIds = [TestConstants.OrganizationId],
            ExpiresUtc = utcNow.AddHours(1),
            RefreshExpiresUtc = refreshExpiresUtc ?? utcNow.AddDays(30),
            IsDisabled = isDisabled,
            CreatedBy = userId ?? TestConstants.UserId,
            CreatedUtc = utcNow,
            UpdatedUtc = utcNow
        };
    }
}
