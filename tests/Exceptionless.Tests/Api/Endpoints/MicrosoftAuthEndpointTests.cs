using Exceptionless.Core.Authorization;
using Exceptionless.Core.Configuration;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Extensions;
using Exceptionless.Tests.Utility;
using Exceptionless.Web.Models;
using FluentRest;
using Foundatio.Repositories;
using Xunit;

namespace Exceptionless.Tests.Api.Endpoints;

public sealed class MicrosoftAuthEndpointTests : IntegrationTestsBase
{
    private readonly AuthOptions _authOptions;
    private readonly bool _originalEnableAccountCreation;
    private readonly string? _originalMicrosoftId;
    private readonly string? _originalMicrosoftSecret;
    private readonly IUserRepository _userRepository;

    public MicrosoftAuthEndpointTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _authOptions = GetService<AuthOptions>();
        _originalEnableAccountCreation = _authOptions.EnableAccountCreation;
        _originalMicrosoftId = _authOptions.MicrosoftId;
        _originalMicrosoftSecret = _authOptions.MicrosoftSecret;
        _userRepository = GetService<IUserRepository>();
    }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        _authOptions.EnableAccountCreation = true;
        _authOptions.MicrosoftId = "microsoft-client-id";
        _authOptions.MicrosoftSecret = "microsoft-client-secret";
        await GetService<SampleDataService>().CreateDataAsync();
    }

    public override ValueTask DisposeAsync()
    {
        _authOptions.EnableAccountCreation = _originalEnableAccountCreation;
        _authOptions.MicrosoftId = _originalMicrosoftId;
        _authOptions.MicrosoftSecret = _originalMicrosoftSecret;
        return base.DisposeAsync();
    }

    [Fact]
    public async Task MicrosoftAsync_AuthenticatedLinkWithDifferentEmail_ReplacesLegacyIdentity()
    {
        // Arrange
        const string code = "authenticated-microsoft-user";
        var user = await _userRepository.GetByEmailAddressAsync(SampleDataService.TEST_ORG_USER_EMAIL);
        Assert.NotNull(user);
        user.AddOAuthAccount("WindowsLive", "legacy-user", user.EmailAddress);
        await _userRepository.SaveAsync(user, o => o.ImmediateConsistency());

        // Act
        await SendMicrosoftLoginAsync(code, isAuthenticated: true);

        // Assert
        var updatedUser = await _userRepository.GetByIdAsync(user.Id);
        Assert.NotNull(updatedUser);
        Assert.Equal(SampleDataService.TEST_ORG_USER_EMAIL, updatedUser.EmailAddress);
        var account = Assert.Single(updatedUser.OAuthAccounts);
        Assert.Equal("microsoft", account.Provider);
        Assert.Equal(code, account.ProviderUserId);
        Assert.Equal(TestOAuthProviderClient.GetEmailAddress(code), account.Username);
    }

    [Fact]
    public async Task MicrosoftAsync_ExistingModernIdentity_RemovesStaleLegacyIdentity()
    {
        // Arrange
        const string code = "existing-microsoft-user";
        var user = CreateUser(TestOAuthProviderClient.GetEmailAddress(code));
        user.AddOAuthAccount("WindowsLive", "legacy-user", user.EmailAddress);
        user.AddOAuthAccount("Microsoft", code, user.EmailAddress);
        await _userRepository.AddAsync(user, o => o.ImmediateConsistency());

        // Act
        await SendMicrosoftLoginAsync(code);

        // Assert
        var updatedUser = await _userRepository.GetByIdAsync(user.Id);
        Assert.NotNull(updatedUser);
        var account = Assert.Single(updatedUser.OAuthAccounts);
        Assert.Equal("microsoft", account.Provider);
        Assert.Equal(code, account.ProviderUserId);
    }

    [Fact]
    public async Task MicrosoftAsync_ExactEmailMatch_ReplacesLegacyIdentityWithoutDuplicateUser()
    {
        // Arrange
        const string code = "matching-email-user";
        var user = CreateUser(TestOAuthProviderClient.GetEmailAddress(code));
        user.AddOAuthAccount("WindowsLive", "legacy-user", user.EmailAddress);
        await _userRepository.AddAsync(user, o => o.ImmediateConsistency());

        // Act
        await SendMicrosoftLoginAsync(code);

        // Assert
        var updatedUser = await _userRepository.GetByEmailAddressAsync(user.EmailAddress);
        Assert.NotNull(updatedUser);
        Assert.Equal(user.Id, updatedUser.Id);
        var account = Assert.Single(updatedUser.OAuthAccounts);
        Assert.Equal("microsoft", account.Provider);
        Assert.Equal(code, account.ProviderUserId);
    }

    [Fact]
    public async Task MicrosoftAsync_UnmatchedEmail_CreatesNewUserWithoutChangingLegacyUser()
    {
        // Arrange
        const string code = "different-email-user";
        var legacyUser = CreateUser("legacy-user@exceptionless.test");
        legacyUser.AddOAuthAccount("WindowsLive", "legacy-user", legacyUser.EmailAddress);
        await _userRepository.AddAsync(legacyUser, o => o.ImmediateConsistency());

        // Act
        await SendMicrosoftLoginAsync(code);

        // Assert
        var unchangedLegacyUser = await _userRepository.GetByIdAsync(legacyUser.Id);
        Assert.NotNull(unchangedLegacyUser);
        var legacyAccount = Assert.Single(unchangedLegacyUser.OAuthAccounts);
        Assert.Equal("windowslive", legacyAccount.Provider);

        var microsoftUser = await _userRepository.GetByEmailAddressAsync(TestOAuthProviderClient.GetEmailAddress(code));
        Assert.NotNull(microsoftUser);
        Assert.NotEqual(legacyUser.Id, microsoftUser.Id);
        var microsoftAccount = Assert.Single(microsoftUser.OAuthAccounts);
        Assert.Equal("microsoft", microsoftAccount.Provider);
        Assert.Equal(code, microsoftAccount.ProviderUserId);
    }

    private static User CreateUser(string emailAddress)
    {
        var user = new User
        {
            EmailAddress = emailAddress,
            FullName = "Microsoft User",
            Roles = new HashSet<string> { AuthorizationRoles.Client, AuthorizationRoles.User }
        };
        user.MarkEmailAddressVerified();
        return user;
    }

    private Task<TokenResult?> SendMicrosoftLoginAsync(string code, bool isAuthenticated = false)
    {
        return SendRequestAsAsync<TokenResult>(request =>
        {
            request
                .Post()
                .AppendPaths("auth", "microsoft")
                .Content(new ExternalAuthInfo
                {
                    ClientId = "microsoft-client-id",
                    Code = code,
                    RedirectUri = "http://localhost/callback"
                })
                .StatusCodeShouldBeOk();

            if (isAuthenticated)
                request.AsTestOrganizationUser();
        });
    }
}
