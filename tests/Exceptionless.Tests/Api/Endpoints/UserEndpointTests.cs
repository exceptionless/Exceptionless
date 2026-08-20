using System.Net;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Extensions;
using Exceptionless.Web.Api.Results;
using Exceptionless.Web.Models;
using Exceptionless.Web.Models.OAuth;
using Exceptionless.Web.Utility;
using FluentRest;
using Foundatio.Repositories;
using Foundatio.Repositories.Utility;
using Xunit;

namespace Exceptionless.Tests.Api.Endpoints;

public sealed class UserEndpointTests : IntegrationTestsBase
{
    private readonly IUserRepository _userRepository;
    private readonly IOAuthApplicationRepository _oauthApplicationRepository;
    private readonly IOAuthTokenRepository _oauthTokenRepository;

    public UserEndpointTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _userRepository = GetService<IUserRepository>();
        _oauthApplicationRepository = GetService<IOAuthApplicationRepository>();
        _oauthTokenRepository = GetService<IOAuthTokenRepository>();
    }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        var service = GetService<SampleDataService>();
        await service.CreateDataAsync();
    }

    [Fact]
    public async Task AddAdminRoleAsync_AnonymousUser_ReturnsUnauthorized()
    {
        // Arrange
        var user = await GetTestOrganizationUserAsync();

        // Act
        await SendRequestAsync(r => r
            .Post()
            .AppendPaths("users", user.Id, "admin-role")
            .StatusCodeShouldBeUnauthorized()
        );
    }

    [Fact]
    public async Task AddAdminRoleAsync_AsGlobalAdmin_AddsRole()
    {
        // Arrange
        var user = await GetTestOrganizationUserAsync();

        // Act
        await SendRequestAsync(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPaths("users", user.Id, "admin-role")
            .StatusCodeShouldBeOk()
        );

        // Assert
        var updatedUser = await _userRepository.GetByIdAsync(user.Id);
        Assert.NotNull(updatedUser);
        Assert.Contains(AuthorizationRoles.GlobalAdmin, updatedUser.Roles);
    }

    [Fact]
    public async Task AddAdminRoleAsync_NonAdmin_ReturnsForbidden()
    {
        // Arrange
        var user = await GetTestOrganizationUserAsync();

        // Act
        await SendRequestAsync(r => r
            .Post()
            .AsTestOrganizationUser()
            .AppendPaths("users", user.Id, "admin-role")
            .StatusCodeShouldBeForbidden()
        );

        // Assert - role was not added
        var unchanged = await _userRepository.GetByIdAsync(user.Id);
        Assert.NotNull(unchanged);
        Assert.DoesNotContain(AuthorizationRoles.GlobalAdmin, unchanged.Roles);
    }

    [Fact]
    public async Task DeleteAdminRoleAsync_AsGlobalAdmin_RemovesRole()
    {
        // Arrange
        var currentUser = await SendRequestAsAsync<ViewUser>(r => r
            .AsGlobalAdminUser()
            .AppendPath("users/me")
            .StatusCodeShouldBeOk()
        );
        Assert.NotNull(currentUser);

        // Act
        await SendRequestAsync(r => r
            .Delete()
            .AsGlobalAdminUser()
            .AppendPaths("users", currentUser.Id, "admin-role")
            .StatusCodeShouldBeNoContent()
        );

        // Assert
        var user = await _userRepository.GetByIdAsync(currentUser.Id);
        Assert.NotNull(user);
        Assert.DoesNotContain(AuthorizationRoles.GlobalAdmin, user.Roles);
    }

    [Fact]
    public async Task DeleteAdminRoleAsync_NonAdmin_ReturnsForbidden()
    {
        // Arrange
        var currentUser = await SendRequestAsAsync<ViewUser>(r => r
            .AsGlobalAdminUser()
            .AppendPath("users/me")
            .StatusCodeShouldBeOk()
        );
        Assert.NotNull(currentUser);

        // Act
        await SendRequestAsync(r => r
            .Delete()
            .AsTestOrganizationUser()
            .AppendPaths("users", currentUser.Id, "admin-role")
            .StatusCodeShouldBeForbidden()
        );

        // Assert - role was not removed
        var user = await _userRepository.GetByIdAsync(currentUser.Id);
        Assert.NotNull(user);
        Assert.Contains(AuthorizationRoles.GlobalAdmin, user.Roles);
    }

    [Fact]
    public async Task DeleteAsync_AsGlobalAdmin_ReturnsAccepted()
    {
        // Arrange
        var user = new User
        {
            FullName = "Deletable User",
            EmailAddress = "deletable@exceptionless.test",
            IsEmailAddressVerified = true
        };
        user.Roles.Add(AuthorizationRoles.Client);
        user.Roles.Add(AuthorizationRoles.User);
        user = await _userRepository.AddAsync(user, o => o.ImmediateConsistency());

        // Act
        var response = await SendRequestAsAsync<WorkInProgressResult>(r => r
            .Delete()
            .AsGlobalAdminUser()
            .AppendPaths("users", user.Id)
            .StatusCodeShouldBeAccepted()
        );

        // Assert
        Assert.NotNull(response);
    }

    [Fact]
    public async Task DeleteAsync_NonAdmin_ReturnsForbidden()
    {
        // Arrange
        var user = await GetTestOrganizationUserAsync();

        // Act
        await SendRequestAsync(r => r
            .Delete()
            .AsTestOrganizationUser()
            .AppendPaths("users", user.Id)
            .StatusCodeShouldBeForbidden()
        );

        // Assert - user still exists
        var unchanged = await _userRepository.GetByIdAsync(user.Id);
        Assert.NotNull(unchanged);
    }

    [Fact]
    public async Task DeleteAvatarAsync_WithExistingAvatar_RemovesAvatar()
    {
        // Arrange
        var currentUser = await SendRequestAsAsync<ViewUser>(r => r
            .AsGlobalAdminUser()
            .AppendPath("users/me")
            .StatusCodeShouldBeOk()
        );
        Assert.NotNull(currentUser);
        using var content = CreateProfileImageContent();

        var updatedUser = await SendRequestAsAsync<ViewUser>(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPaths("users", currentUser.Id, "avatar")
            .Content(content)
            .StatusCodeShouldBeOk()
        );
        Assert.NotNull(updatedUser);
        Assert.NotNull(updatedUser.AvatarUrl);

        // Act
        var userWithoutAvatar = await SendRequestAsAsync<ViewUser>(r => r
            .Delete()
            .AsGlobalAdminUser()
            .AppendPaths("users", currentUser.Id, "avatar")
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(userWithoutAvatar);
        Assert.Null(userWithoutAvatar.AvatarUrl);

        var storedUser = await _userRepository.GetByIdAsync(currentUser.Id);
        Assert.NotNull(storedUser);
        Assert.Null(storedUser.AvatarFileName);
    }

    [Fact]
    public Task DeleteCurrentUserAsync_AnonymousUser_ReturnsUnauthorized()
    {
        return SendRequestAsync(r => r
            .Delete()
            .AppendPath("users/me")
            .StatusCodeShouldBeUnauthorized()
        );
    }

    [Fact]
    public async Task DeleteCurrentUserAsync_WithOrganizationMembership_ReturnsBadRequest()
    {
        // Arrange
        var currentUser = await _userRepository.GetByEmailAddressAsync(SampleDataService.TEST_ORG_USER_EMAIL);
        Assert.NotNull(currentUser);
        Assert.NotEmpty(currentUser.OrganizationIds);

        // Act
        await SendRequestAsync(r => r
            .Delete()
            .AsTestOrganizationUser()
            .AppendPath("users/me")
            .StatusCodeShouldBeBadRequest()
        );

        // Assert
        var storedUser = await _userRepository.GetByIdAsync(currentUser.Id);
        Assert.NotNull(storedUser);
        Assert.Contains(SampleDataService.TEST_ORG_ID, storedUser.OrganizationIds);
    }

    [Fact]
    public async Task GetAsync_AnonymousUser_ReturnsUnauthorized()
    {
        // Arrange
        var currentUser = await SendRequestAsAsync<ViewUser>(r => r
            .AsGlobalAdminUser()
            .AppendPath("users/me")
            .StatusCodeShouldBeOk()
        );
        Assert.NotNull(currentUser);

        // Act
        await SendRequestAsync(r => r
            .AppendPaths("users", currentUser.Id)
            .StatusCodeShouldBeUnauthorized()
        );
    }

    [Fact]
    public Task GetAsync_InvalidId_ReturnsNotFound()
    {
        return SendRequestAsync(r => r
            .AsGlobalAdminUser()
            .AppendPaths("users", "000000000000000000000000")
            .StatusCodeShouldBeNotFound()
        );
    }

    [Fact]
    public async Task GetAsync_ValidId_ReturnsUser()
    {
        // Arrange
        var currentUser = await SendRequestAsAsync<ViewUser>(r => r
            .AsGlobalAdminUser()
            .AppendPath("users/me")
            .StatusCodeShouldBeOk()
        );
        Assert.NotNull(currentUser);

        // Act
        var user = await SendRequestAsAsync<ViewUser>(r => r
            .AsGlobalAdminUser()
            .AppendPaths("users", currentUser.Id)
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(user);
        Assert.Equal(currentUser.Id, user.Id);
        Assert.Equal(SampleDataService.TEST_USER_EMAIL, user.EmailAddress);
    }

    [Fact]
    public async Task GetAvatarAsync_WithExistingAvatar_ReturnsImage()
    {
        // Arrange
        var currentUser = await SendRequestAsAsync<ViewUser>(r => r
            .AsGlobalAdminUser()
            .AppendPath("users/me")
            .StatusCodeShouldBeOk()
        );
        Assert.NotNull(currentUser);
        using var content = CreateProfileImageContent();

        var updatedUser = await SendRequestAsAsync<ViewUser>(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPaths("users", currentUser.Id, "avatar")
            .Content(content)
            .StatusCodeShouldBeOk()
        );
        Assert.NotNull(updatedUser);
        Assert.NotNull(updatedUser.AvatarUrl);
        string avatarPath = updatedUser.AvatarUrl.TrimStart('/');

        // Act
        var response = await SendRequestAsync(r => r
            .BaseUri(_server.BaseAddress)
            .AppendPath(avatarPath)
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Headers.CacheControl?.Public);
        Assert.Equal(TimeSpan.FromDays(365), response.Headers.CacheControl?.MaxAge);
    }

    [Fact]
    public Task GetByOrganizationAsync_AnonymousUser_ReturnsUnauthorized()
    {
        return SendRequestAsync(r => r
            .AppendPath($"organizations/{SampleDataService.TEST_ORG_ID}/users")
            .StatusCodeShouldBeUnauthorized()
        );
    }

    [Fact]
    public async Task GetByOrganizationAsync_ValidOrganization_ReturnsUsers()
    {
        // Act
        var users = await SendRequestAsAsync<IReadOnlyCollection<ViewUser>>(r => r
            .AsGlobalAdminUser()
            .AppendPath($"organizations/{SampleDataService.TEST_ORG_ID}/users")
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(users);
        Assert.NotEmpty(users);
    }

    [Fact]
    public Task GetCurrentUserAsync_AnonymousUser_ReturnsUnauthorized()
    {
        return SendRequestAsync(r => r
            .AppendPath("users/me")
            .StatusCodeShouldBeUnauthorized()
        );
    }

    [Fact]
    public async Task GetCurrentUserAsync_AuthenticatedUser_ReturnsCurrentUser()
    {
        // Act
        var user = await SendRequestAsAsync<ViewUser>(r => r
            .AsGlobalAdminUser()
            .AppendPath("users/me")
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(user);
        Assert.Equal(SampleDataService.TEST_USER_EMAIL, user.EmailAddress);
        Assert.NotNull(user.Id);
        Assert.True(user.IsActive);
    }

    [Fact]
    public async Task GetCurrentUserAsync_TestOrganizationUser_ReturnsCurrentUser()
    {
        // Act
        var user = await SendRequestAsAsync<ViewUser>(r => r
            .AsTestOrganizationUser()
            .AppendPath("users/me")
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(user);
        Assert.Equal(SampleDataService.TEST_ORG_USER_EMAIL, user.EmailAddress);
        Assert.True(user.IsActive);
    }

    [Fact]
    public async Task GetCurrentUserAsync_WithAvatar_ReturnsRoutableAvatarUrl()
    {
        // Arrange
        var currentUser = await _userRepository.GetByEmailAddressAsync(SampleDataService.TEST_ORG_USER_EMAIL);
        Assert.NotNull(currentUser);
        currentUser.AvatarFileName = "avatar.png";
        await _userRepository.SaveAsync(currentUser, o => o.ImmediateConsistency().Cache());

        // Act
        var user = await SendRequestAsAsync<ViewUser>(r => r
            .AsTestOrganizationUser()
            .AppendPath("users/me")
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(user);
        Assert.Equal($"/api/v2/users/{currentUser.Id}/avatar/avatar.png", user.AvatarUrl);
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
        Assert.Contains(grant.Resources, r => r.Resource == "http://localhost:7110/mcp" && r.Scopes.Contains(AuthorizationRoles.McpRead));
        Assert.Contains(grant.Resources, r => r.Resource == "http://localhost:7110/api/v2" && r.Scopes.Contains(AuthorizationRoles.StacksRead));
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

    [Fact]
    public async Task UploadAvatarAsync_ImageOverGlobalRequestLimit_ReturnsUpdatedUser()
    {
        // Arrange
        var currentUser = await SendRequestAsAsync<ViewUser>(r => r
            .AsGlobalAdminUser()
            .AppendPath("users/me")
            .StatusCodeShouldBeOk()
        );
        Assert.NotNull(currentUser);
        using var content = CreateProfileImageContent();

        // Act
        var updatedUser = await SendRequestAsAsync<ViewUser>(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPaths("users", currentUser.Id, "avatar")
            .Content(content)
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(updatedUser);
        Assert.Contains($"/users/{currentUser.Id}/avatar/", updatedUser.AvatarUrl);

        var storedUser = await _userRepository.GetByIdAsync(currentUser.Id);
        Assert.NotNull(storedUser);
        Assert.Equal(updatedUser.AvatarUrl?.Split('/').Last(), storedUser.AvatarFileName);
        Assert.DoesNotContain("/", storedUser.AvatarFileName!);
    }

    [Fact]
    public async Task UploadAvatarAsync_NonExistentUser_ReturnsNotFoundBeforeFileValidation()
    {
        // Arrange
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("ignored"), "description");

        // Act
        using var response = await SendRequestAsync(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPaths("users", "000000000000000000000000", "avatar")
            .Content(content)
            .StatusCodeShouldBeNotFound()
        );

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PatchAsync_AnonymousUser_ReturnsUnauthorized()
    {
        // Arrange
        var currentUser = await SendRequestAsAsync<ViewUser>(r => r
            .AsGlobalAdminUser()
            .AppendPath("users/me")
            .StatusCodeShouldBeOk()
        );
        Assert.NotNull(currentUser);

        // Act
        await SendRequestAsync(r => r
            .Patch()
            .AppendPaths("users", currentUser.Id)
            .Content(new { FullName = "Hacker" })
            .StatusCodeShouldBeUnauthorized()
        );

        // Assert - name was not changed
        var user = await _userRepository.GetByIdAsync(currentUser.Id);
        Assert.NotNull(user);
        Assert.NotEqual("Hacker", user.FullName);
    }

    [Fact]
    public async Task PatchAsync_UpdateFullName_ReturnsUpdatedUser()
    {
        // Arrange
        var currentUser = await SendRequestAsAsync<ViewUser>(r => r
            .AsGlobalAdminUser()
            .AppendPath("users/me")
            .StatusCodeShouldBeOk()
        );
        Assert.NotNull(currentUser);

        // Act
        var updatedUser = await SendRequestAsAsync<ViewUser>(r => r
            .Patch()
            .AsGlobalAdminUser()
            .AppendPaths("users", currentUser.Id)
            .Content(new { FullName = "Updated Name" })
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(updatedUser);
        Assert.Equal("Updated Name", updatedUser.FullName);
    }

    [Fact]
    public async Task PatchAsync_UpdateNotifications_ReturnsUpdatedUser()
    {
        // Arrange
        var currentUser = await SendRequestAsAsync<ViewUser>(r => r
            .AsGlobalAdminUser()
            .AppendPath("users/me")
            .StatusCodeShouldBeOk()
        );
        Assert.NotNull(currentUser);

        // Act
        var updatedUser = await SendRequestAsAsync<ViewUser>(r => r
            .Patch()
            .AsGlobalAdminUser()
            .AppendPaths("users", currentUser.Id)
            .Content(new { EmailNotificationsEnabled = false })
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(updatedUser);
        Assert.False(updatedUser.EmailNotificationsEnabled);
    }

    [Fact]
    public Task PatchAsync_WithNonExistentId_ReturnsNotFound()
    {
        return SendRequestAsync(r => r
            .Patch()
            .AsGlobalAdminUser()
            .AppendPaths("users", "000000000000000000000000")
            .Content(new { FullName = "Nobody" })
            .StatusCodeShouldBeNotFound()
        );
    }

    [Fact]
    public async Task PutAsync_UpdateFullName_ReturnsUpdatedUser()
    {
        // Arrange
        var currentUser = await SendRequestAsAsync<ViewUser>(r => r
            .AsGlobalAdminUser()
            .AppendPath("users/me")
            .StatusCodeShouldBeOk()
        );
        Assert.NotNull(currentUser);

        // Act
        var updatedUser = await SendRequestAsAsync<ViewUser>(r => r
            .Put()
            .AsGlobalAdminUser()
            .AppendPaths("users", currentUser.Id)
            .Content(new { FullName = "Put Updated Name" })
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(updatedUser);
        Assert.Equal("Put Updated Name", updatedUser.FullName);
    }

    [Fact]
    public async Task ResendVerificationEmailAsync_AnonymousUser_ReturnsUnauthorized()
    {
        // Arrange
        var currentUser = await SendRequestAsAsync<ViewUser>(r => r
            .AsGlobalAdminUser()
            .AppendPath("users/me")
            .StatusCodeShouldBeOk()
        );
        Assert.NotNull(currentUser);

        // Act
        await SendRequestAsync(r => r
            .AppendPaths("users", currentUser.Id, "resend-verification-email")
            .StatusCodeShouldBeUnauthorized()
        );
    }

    [Fact]
    public async Task ResendVerificationEmailAsync_ValidUser_ReturnsOk()
    {
        // Arrange
        var currentUser = await SendRequestAsAsync<ViewUser>(r => r
            .AsGlobalAdminUser()
            .AppendPath("users/me")
            .StatusCodeShouldBeOk()
        );
        Assert.NotNull(currentUser);

        // Act
        await SendRequestAsync(r => r
            .AsGlobalAdminUser()
            .AppendPaths("users", currentUser.Id, "resend-verification-email")
            .StatusCodeShouldBeOk()
        );
    }

    [Fact]
    public Task UnverifyEmailAddressAsync_AsGlobalAdmin_ReturnsOk()
    {
        return SendRequestAsync(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPath("users/unverify-email-address")
            .Content(SampleDataService.TEST_USER_EMAIL, "text/plain")
            .StatusCodeShouldBeOk()
        );
    }

    [Fact]
    public Task UnverifyEmailAddressAsync_NonAdmin_ReturnsForbidden()
    {
        return SendRequestAsync(r => r
            .Post()
            .AsTestOrganizationUser()
            .AppendPath("users/unverify-email-address")
            .Content(SampleDataService.TEST_USER_EMAIL, "text/plain")
            .StatusCodeShouldBeForbidden()
        );
    }

    [Fact]
    public async Task UnverifyEmailAddressAsync_NonTextBody_ReturnsUnsupportedMediaType()
    {
        // Arrange
        var user = await _userRepository.GetByEmailAddressAsync(SampleDataService.TEST_USER_EMAIL);
        Assert.NotNull(user);
        user.MarkEmailAddressVerified();
        await _userRepository.SaveAsync(user, o => o.ImmediateConsistency());

        // Act
        using var response = await SendRequestAsync(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPath("users/unverify-email-address")
            .Content($"\"{SampleDataService.TEST_USER_EMAIL}\"", "application/json")
            .ExpectedStatus(HttpStatusCode.UnsupportedMediaType)
        );

        // Assert
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        var unchangedUser = await _userRepository.GetByIdAsync(user.Id);
        Assert.NotNull(unchangedUser);
        Assert.True(unchangedUser.IsEmailAddressVerified);
    }

    [Fact]
    public async Task UpdateEmailAddressAsync_AnonymousUser_ReturnsUnauthorized()
    {
        // Arrange
        var currentUser = await SendRequestAsAsync<ViewUser>(r => r
            .AsGlobalAdminUser()
            .AppendPath("users/me")
            .StatusCodeShouldBeOk()
        );
        Assert.NotNull(currentUser);

        // Act
        await SendRequestAsync(r => r
            .Post()
            .AppendPaths("users", currentUser.Id, "email-address", "newemail@exceptionless.test")
            .StatusCodeShouldBeUnauthorized()
        );

        // Assert - email was not changed
        var user = await _userRepository.GetByIdAsync(currentUser.Id);
        Assert.NotNull(user);
        Assert.NotEqual("newemail@exceptionless.test", user.EmailAddress);
    }

    [Fact]
    public async Task UpdateEmailAddressAsync_ValidEmail_ReturnsResult()
    {
        // Arrange
        var currentUser = await SendRequestAsAsync<ViewUser>(r => r
            .AsGlobalAdminUser()
            .AppendPath("users/me")
            .StatusCodeShouldBeOk()
        );
        Assert.NotNull(currentUser);

        // Act
        var result = await SendRequestAsAsync<UpdateEmailAddressResult>(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPaths("users", currentUser.Id, "email-address", "newemail@exceptionless.test")
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task VerifyAsync_ExpiredToken_ReturnsValidationProblem()
    {
        // Arrange
        var user = await _userRepository.GetByEmailAddressAsync(SampleDataService.TEST_ORG_USER_EMAIL);
        Assert.NotNull(user);
        user.ResetVerifyEmailAddressTokenAndExpiration(TimeProvider);
        user.VerifyEmailAddressTokenExpiration = TimeProvider.GetUtcNow().UtcDateTime.AddMinutes(-1);
        await _userRepository.SaveAsync(user, o => o.ImmediateConsistency().Cache());
        string token = Assert.IsType<string>(user.VerifyEmailAddressToken);

        // Act
        await SendRequestAsync(r => r
            .AsGlobalAdminUser()
            .AppendPaths("users", "verify-email-address", token)
            .StatusCodeShouldBeUnprocessableEntity()
        );

        // Assert
        var updatedUser = await _userRepository.GetByIdAsync(user.Id);
        Assert.NotNull(updatedUser);
        Assert.False(updatedUser.IsEmailAddressVerified);
    }

    [Fact]
    public async Task VerifyAsync_InvalidToken_ReturnsNotFound()
    {
        // Arrange
        const string token = "invalidtoken1234567890ab";

        // Act
        var response = await SendRequestAsync(r => r
            .AsGlobalAdminUser()
            .AppendPaths("users", "verify-email-address", token)
            .StatusCodeShouldBeNotFound()
        );

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task VerifyAsync_ValidToken_VerifiesEmailAddress()
    {
        // Arrange
        var user = await _userRepository.GetByEmailAddressAsync(SampleDataService.TEST_ORG_USER_EMAIL);
        Assert.NotNull(user);
        user.ResetVerifyEmailAddressTokenAndExpiration(TimeProvider);
        await _userRepository.SaveAsync(user, o => o.ImmediateConsistency().Cache());
        string token = Assert.IsType<string>(user.VerifyEmailAddressToken);

        // Act
        await SendRequestAsync(r => r
            .AsGlobalAdminUser()
            .AppendPaths("users", "verify-email-address", token)
            .StatusCodeShouldBeOk()
        );

        // Assert
        var updatedUser = await _userRepository.GetByIdAsync(user.Id);
        Assert.NotNull(updatedUser);
        Assert.True(updatedUser.IsEmailAddressVerified);
        Assert.Null(updatedUser.VerifyEmailAddressToken);
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

    private async Task<ViewUser> GetTestOrganizationUserAsync()
    {
        var user = await SendRequestAsAsync<ViewUser>(r => r
            .AsTestOrganizationUser()
            .AppendPath("users/me")
            .StatusCodeShouldBeOk()
        );
        Assert.NotNull(user);
        return user;
    }

    private static MultipartFormDataContent CreateProfileImageContent()
    {
        byte[] bytes = new byte[256 * 1024];
        byte[] pngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        Array.Copy(pngHeader, bytes, pngHeader.Length);
        Assert.True(bytes.Length < ProfileImageStorage.MaxFileSize);

        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new("image/png");

        var content = new MultipartFormDataContent();
        content.Add(fileContent, "file", "avatar.png");
        return content;
    }
}
