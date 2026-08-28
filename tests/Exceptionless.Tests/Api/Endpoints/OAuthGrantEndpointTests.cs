using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Extensions;
using Exceptionless.Web.Models.OAuth;
using Foundatio.Repositories;
using Foundatio.Repositories.Utility;
using Xunit;

namespace Exceptionless.Tests.Api.Endpoints;

public sealed class OAuthGrantEndpointTests : IntegrationTestsBase
{
    private readonly IOAuthApplicationRepository _oauthApplicationRepository;
    private readonly IOAuthTokenRepository _oauthTokenRepository;
    private readonly IUserRepository _userRepository;

    public OAuthGrantEndpointTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _oauthApplicationRepository = GetService<IOAuthApplicationRepository>();
        _oauthTokenRepository = GetService<IOAuthTokenRepository>();
        _userRepository = GetService<IUserRepository>();
    }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        await GetService<SampleDataService>().CreateDataAsync();
    }

    [Fact]
    public async Task GetOAuthGrantsAsync_WithActiveOAuthTokens_ReturnsGroupedApplications()
    {
        // Arrange
        var user = await _userRepository.GetByEmailAddressAsync(SampleDataService.TEST_ORG_USER_EMAIL);
        Assert.NotNull(user);

        const string clientId = "test-oauth-grant-client";
        await CreateOAuthApplicationAsync(clientId, "Test AI Client");
        string grantId = StringExtensions.GetNewToken();
        await CreateOAuthGrantTokenAsync(user.Id, clientId, "http://localhost:7110/mcp", [AuthorizationRoles.McpRead, AuthorizationRoles.ProjectsRead, AuthorizationRoles.OfflineAccess], grantId: grantId);
        await CreateOAuthGrantTokenAsync(user.Id, clientId, "http://localhost:7110/api/v2", [AuthorizationRoles.ProjectsRead, AuthorizationRoles.StacksRead], grantId: grantId);
        await CreateOAuthGrantTokenAsync(user.Id, "disabled-oauth-grant-client", "http://localhost:7110/mcp", [AuthorizationRoles.McpRead], isDisabled: true);

        // Act
        var grants = await SendRequestAsAsync<IReadOnlyCollection<ViewOAuthGrant>>(r => r
            .AsTestOrganizationUser()
            .AppendPath("users/me/oauth-grants")
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(grants);
        var grant = Assert.Single(grants);
        Assert.Equal(clientId, grant.ClientId);
        Assert.Equal("Test AI Client", grant.ApplicationName);
        Assert.Contains(SampleDataService.TEST_ORG_ID, grant.OrganizationIds);
        Assert.Contains(AuthorizationRoles.McpRead, grant.Scopes);
        Assert.Contains(AuthorizationRoles.StacksRead, grant.Scopes);
        Assert.Equal(2, grant.Resources.Count);
        Assert.Contains(grant.Resources, resource => String.Equals(resource.Resource, "http://localhost:7110/mcp", StringComparison.Ordinal) && resource.Scopes.Contains(AuthorizationRoles.McpRead));
        Assert.Contains(grant.Resources, resource => String.Equals(resource.Resource, "http://localhost:7110/api/v2", StringComparison.Ordinal) && resource.Scopes.Contains(AuthorizationRoles.StacksRead));
    }

    [Fact]
    public async Task GetOAuthGrantsAsync_WhenDisabledTokensExceedPageLimit_ReturnsActiveGrant()
    {
        var user = await _userRepository.GetByEmailAddressAsync(SampleDataService.TEST_ORG_USER_EMAIL);
        Assert.NotNull(user);
        const string clientId = "paged-oauth-grant-client";
        await CreateOAuthApplicationAsync(clientId, "Paged OAuth Grant Client");
        var utcNow = TimeProvider.GetUtcNow().UtcDateTime;
        var disabledTokens = Enumerable.Range(0, 1005)
            .Select(i => CreateOAuthGrantToken(user.Id, $"disabled-paged-client-{i}", "http://localhost:7110/mcp", [AuthorizationRoles.McpRead], utcNow.AddMinutes(1), isDisabled: true))
            .ToArray();
        await _oauthTokenRepository.AddAsync(disabledTokens, o => o.ImmediateConsistency());
        string grantId = StringExtensions.GetNewToken();
        await _oauthTokenRepository.AddAsync(CreateOAuthGrantToken(
            user.Id,
            clientId,
            "http://localhost:7110/mcp",
            [AuthorizationRoles.McpRead, AuthorizationRoles.OfflineAccess],
            utcNow,
            grantId: grantId), o => o.ImmediateConsistency());

        var grants = await SendRequestAsAsync<IReadOnlyCollection<ViewOAuthGrant>>(r => r
            .AsTestOrganizationUser()
            .AppendPath("users/me/oauth-grants")
            .StatusCodeShouldBeOk()
        );

        Assert.NotNull(grants);
        var grant = Assert.Single(grants);
        Assert.Equal(clientId, grant.ClientId);
        Assert.Equal("Paged OAuth Grant Client", grant.ApplicationName);
        Assert.Contains(AuthorizationRoles.McpRead, grant.Scopes);
    }

    [Fact]
    public async Task RevokeOAuthGrantAsync_WithCurrentUserGrant_DisablesAllClientTokens()
    {
        // Arrange
        var user = await _userRepository.GetByEmailAddressAsync(SampleDataService.TEST_ORG_USER_EMAIL);
        Assert.NotNull(user);

        const string clientId = "test-revoke-client";
        await CreateOAuthApplicationAsync(clientId, "Revoked AI Client");
        await CreateOAuthApplicationAsync("unrelated-revoke-client", "Unrelated AI Client");
        var firstToken = await CreateOAuthGrantTokenAsync(user.Id, clientId, "http://localhost:7110/mcp", [AuthorizationRoles.McpRead, AuthorizationRoles.OfflineAccess]);
        var secondToken = await CreateOAuthGrantTokenAsync(user.Id, clientId, "http://localhost:7110/api/v2", [AuthorizationRoles.ProjectsRead, AuthorizationRoles.OfflineAccess]);
        var unrelatedToken = await CreateOAuthGrantTokenAsync(user.Id, "unrelated-revoke-client", "http://localhost:7110/mcp", [AuthorizationRoles.McpRead, AuthorizationRoles.OfflineAccess]);

        // Act
        await SendRequestAsync(r => r
            .Delete()
            .AsTestOrganizationUser()
            .AppendPaths("users", "me", "oauth-grants", firstToken.GrantId!)
            .StatusCodeShouldBeNoContent()
        );

        // Assert
        var revokedFirstToken = await _oauthTokenRepository.GetByIdAsync(firstToken.Id, o => o.ImmediateConsistency());
        var revokedSecondToken = await _oauthTokenRepository.GetByIdAsync(secondToken.Id, o => o.ImmediateConsistency());
        var stillActiveToken = await _oauthTokenRepository.GetByIdAsync(unrelatedToken.Id, o => o.ImmediateConsistency());
        Assert.NotNull(revokedFirstToken);
        Assert.NotNull(revokedSecondToken);
        Assert.NotNull(stillActiveToken);
        Assert.True(revokedFirstToken.IsDisabled);
        Assert.True(revokedSecondToken.IsDisabled);
        Assert.Null(revokedFirstToken.RefreshTokenHash);
        Assert.Null(revokedSecondToken.RefreshTokenHash);
        Assert.False(stillActiveToken.IsDisabled);
        Assert.NotNull(stillActiveToken.RefreshTokenHash);
    }

    [Fact]
    public async Task RevokeOAuthGrantAsync_WhenClientTokensExceedPageLimit_DisablesAllClientTokens()
    {
        var user = await _userRepository.GetByEmailAddressAsync(SampleDataService.TEST_ORG_USER_EMAIL);
        Assert.NotNull(user);
        const string clientId = "paged-revoke-client";
        await CreateOAuthApplicationAsync(clientId, "Paged Revoke Client");
        var utcNow = TimeProvider.GetUtcNow().UtcDateTime;
        var tokens = Enumerable.Range(0, 1005)
            .Select(i => CreateOAuthGrantToken(user.Id, clientId, "http://localhost:7110/mcp", [AuthorizationRoles.McpRead, AuthorizationRoles.OfflineAccess], utcNow))
            .ToList();
        string targetGrantId = StringExtensions.GetNewToken();
        var targetToken = CreateOAuthGrantToken(user.Id, clientId, "http://localhost:7110/api/v2", [AuthorizationRoles.ProjectsRead, AuthorizationRoles.OfflineAccess], utcNow, grantId: targetGrantId);
        tokens.Add(targetToken);
        await _oauthTokenRepository.AddAsync(tokens, o => o.ImmediateConsistency());

        await SendRequestAsync(r => r
            .Delete()
            .AsTestOrganizationUser()
            .AppendPaths("users", "me", "oauth-grants", targetGrantId)
            .StatusCodeShouldBeNoContent()
        );

        var results = await _oauthTokenRepository.GetByUserIdAndClientIdForUpdateAsync(user.Id, clientId, o => o.ImmediateConsistency().SearchAfterPaging().PageLimit(1000));
        int tokenCount = 0;
        do
        {
            foreach (var token in results.Documents)
            {
                tokenCount++;
                Assert.True(token.IsDisabled);
                Assert.Null(token.RefreshTokenHash);
            }
        } while (await results.NextPageAsync());

        Assert.Equal(tokens.Count, tokenCount);
    }

    [Fact]
    public async Task RevokeOAuthGrantAsync_ForAnotherUserGrant_ReturnsNotFound()
    {
        // Arrange
        var freeUser = await _userRepository.GetByEmailAddressAsync(SampleDataService.FREE_USER_EMAIL);
        Assert.NotNull(freeUser);

        const string clientId = "other-user-revoke-client";
        await CreateOAuthApplicationAsync(clientId, "Other User AI Client");
        var token = await CreateOAuthGrantTokenAsync(freeUser.Id, clientId, "http://localhost:7110/mcp", [AuthorizationRoles.McpRead, AuthorizationRoles.OfflineAccess], organizationIds: [SampleDataService.FREE_ORG_ID]);

        // Act
        await SendRequestAsync(r => r
            .Delete()
            .AsTestOrganizationUser()
            .AppendPaths("users", "me", "oauth-grants", token.GrantId!)
            .StatusCodeShouldBeNotFound()
        );

        // Assert
        var storedToken = await _oauthTokenRepository.GetByIdAsync(token.Id, o => o.ImmediateConsistency());
        Assert.NotNull(storedToken);
        Assert.False(storedToken.IsDisabled);
        Assert.NotNull(storedToken.RefreshTokenHash);
    }
    private Task<OAuthApplication> CreateOAuthApplicationAsync(string clientId, string name)
    {
        var utcNow = TimeProvider.GetUtcNow().UtcDateTime;
        var application = new OAuthApplication
        {
            Id = ObjectId.GenerateNewId().ToString(),
            ClientId = clientId,
            Name = name,
            RedirectUris = ["http://localhost/callback"],
            Scopes =
            [
                AuthorizationRoles.McpRead,
                AuthorizationRoles.ProjectsRead,
                AuthorizationRoles.StacksRead,
                AuthorizationRoles.StacksWrite,
                AuthorizationRoles.EventsRead,
                AuthorizationRoles.OfflineAccess
            ],
            CreatedByUserId = OAuthApplication.SystemUserId,
            UpdatedByUserId = OAuthApplication.SystemUserId,
            CreatedUtc = utcNow,
            UpdatedUtc = utcNow
        };

        return _oauthApplicationRepository.AddAsync(application, o => o.ImmediateConsistency());
    }

    private async Task<OAuthToken> CreateOAuthGrantTokenAsync(string userId, string clientId, string resource, string[] scopes, bool isDisabled = false, string[]? organizationIds = null, string? grantId = null)
    {
        var utcNow = TimeProvider.GetUtcNow().UtcDateTime;
        var accessToken = StringExtensions.GetRandomString(OAuthService.OAuthTokenLength);
        var refreshToken = scopes.Contains(AuthorizationRoles.OfflineAccess, StringComparer.Ordinal) ? StringExtensions.GetRandomString(OAuthService.OAuthTokenLength) : null;
        var token = new OAuthToken
        {
            Id = ObjectId.GenerateNewId().ToString(),
            UserId = userId,
            ClientId = clientId,
            GrantId = String.IsNullOrWhiteSpace(grantId) ? StringExtensions.GetNewToken() : grantId,
            Resource = resource,
            AccessTokenHash = OAuthService.CreateTokenHash(accessToken),
            RefreshTokenHash = refreshToken is null ? null : OAuthService.CreateTokenHash(refreshToken),
            Scopes = scopes.ToHashSet(StringComparer.Ordinal),
            OrganizationIds = (organizationIds ?? [SampleDataService.TEST_ORG_ID]).ToHashSet(StringComparer.Ordinal),
            ExpiresUtc = utcNow.AddHours(1),
            RefreshExpiresUtc = refreshToken is not null ? utcNow.AddDays(30) : null,
            IsDisabled = isDisabled,
            CreatedBy = userId,
            CreatedUtc = utcNow,
            UpdatedUtc = utcNow
        };

        await _oauthTokenRepository.AddAsync(token, o => o.ImmediateConsistency());
        return token;
    }

    private static OAuthToken CreateOAuthGrantToken(string userId, string clientId, string resource, string[] scopes, DateTime utcNow, bool isDisabled = false, string[]? organizationIds = null, string? grantId = null)
    {
        var accessToken = StringExtensions.GetRandomString(OAuthService.OAuthTokenLength);
        var refreshToken = scopes.Contains(AuthorizationRoles.OfflineAccess, StringComparer.Ordinal) ? StringExtensions.GetRandomString(OAuthService.OAuthTokenLength) : null;
        return new OAuthToken
        {
            Id = ObjectId.GenerateNewId().ToString(),
            UserId = userId,
            ClientId = clientId,
            GrantId = String.IsNullOrWhiteSpace(grantId) ? StringExtensions.GetNewToken() : grantId,
            Resource = resource,
            AccessTokenHash = OAuthService.CreateTokenHash(accessToken),
            RefreshTokenHash = refreshToken is null ? null : OAuthService.CreateTokenHash(refreshToken),
            Scopes = scopes.ToHashSet(StringComparer.Ordinal),
            OrganizationIds = (organizationIds ?? [SampleDataService.TEST_ORG_ID]).ToHashSet(StringComparer.Ordinal),
            ExpiresUtc = utcNow.AddHours(1),
            RefreshExpiresUtc = refreshToken is not null ? utcNow.AddDays(30) : null,
            IsDisabled = isDisabled,
            CreatedBy = userId,
            CreatedUtc = utcNow,
            UpdatedUtc = utcNow
        };
    }
}
