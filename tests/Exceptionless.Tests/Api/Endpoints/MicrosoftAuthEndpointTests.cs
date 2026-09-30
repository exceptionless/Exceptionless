using Exceptionless.Core.Authorization;
using Exceptionless.Core.Configuration;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Extensions;
using Exceptionless.Tests.Mail;
using Exceptionless.Tests.Utility;
using Exceptionless.Web.Models;
using FluentRest;
using Foundatio.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ProblemDetails = Microsoft.AspNetCore.Mvc.ProblemDetails;

namespace Exceptionless.Tests.Api.Endpoints;

public sealed class MicrosoftAuthEndpointTests : IntegrationTestsBase
{
    private readonly AuthOptions _authOptions;
    private readonly bool _originalEnableAccountCreation;
    private readonly string? _originalMicrosoftId;
    private readonly string? _originalMicrosoftSecret;
    private readonly IUserRepository _userRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly CountingMailer _mailer;

    public MicrosoftAuthEndpointTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _authOptions = GetService<AuthOptions>();
        _originalEnableAccountCreation = _authOptions.EnableAccountCreation;
        _originalMicrosoftId = _authOptions.MicrosoftId;
        _originalMicrosoftSecret = _authOptions.MicrosoftSecret;
        _userRepository = GetService<IUserRepository>();
        _organizationRepository = GetService<IOrganizationRepository>();
        _mailer = GetService<CountingMailer>();
    }

    protected override void RegisterServices(IServiceCollection services)
    {
        base.RegisterServices(services);
        services.AddSingleton<CountingMailer>();
        services.ReplaceSingleton<IMailer>(provider => provider.GetRequiredService<CountingMailer>());
    }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        _authOptions.EnableAccountCreation = true;
        _authOptions.MicrosoftId = "microsoft-client-id";
        _authOptions.MicrosoftSecret = "microsoft-client-secret";
        await GetService<SampleDataService>().CreateDataAsync();
        _mailer.Reset();
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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MicrosoftAsync_ExistingModernIdentity_PreservesEmailVerification(bool isEmailVerified)
    {
        // Arrange
        const string code = "existing-microsoft-user";
        var user = CreateUser(TestOAuthProviderClient.GetEmailAddress(code));
        if (!isEmailVerified)
            user.ResetVerifyEmailAddressTokenAndExpiration(TimeProvider);
        string? verificationToken = user.VerifyEmailAddressToken;
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
        Assert.Equal(isEmailVerified, updatedUser.IsEmailAddressVerified);
        Assert.Equal(verificationToken, updatedUser.VerifyEmailAddressToken);
    }

    [Fact]
    public async Task MicrosoftAsync_ExactEmailMatch_RequiresAuthenticatedLinkWithoutChangingUser()
    {
        // Arrange
        const string code = "matching-email-user";
        var user = CreateUser(TestOAuthProviderClient.GetEmailAddress(code));
        user.AddOAuthAccount("WindowsLive", "legacy-user", user.EmailAddress);
        await _userRepository.AddAsync(user, o => o.ImmediateConsistency());

        // Act
        var problem = await SendRequestAsAsync<ProblemDetails>(request => request
            .Post()
            .AppendPaths("auth", "microsoft")
            .Content(new ExternalAuthInfo { ClientId = "microsoft-client-id", Code = code, RedirectUri = "http://localhost" })
            .StatusCodeShouldBeForbidden());

        // Assert
        var updatedUser = await _userRepository.GetByEmailAddressAsync(user.EmailAddress);
        Assert.NotNull(updatedUser);
        Assert.Equal(user.Id, updatedUser.Id);
        var account = Assert.Single(updatedUser.OAuthAccounts);
        Assert.Equal("windowslive", account.Provider);
        Assert.Equal("legacy-user", account.ProviderUserId);
        Assert.NotNull(problem);
        Assert.Contains("link Microsoft", problem.Title);
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
        Assert.False(microsoftUser.IsEmailAddressVerified);
        Assert.False(String.IsNullOrWhiteSpace(microsoftUser.VerifyEmailAddressToken));
        Assert.True(microsoftUser.VerifyEmailAddressTokenExpiration > TimeProvider.GetUtcNow().UtcDateTime);
        Assert.Equal(1, _mailer.UserEmailVerificationCount);
    }

    [Fact]
    public async Task MicrosoftAsync_MatchingInvitation_VerifiesWithoutSendingExpiredEmail()
    {
        // Arrange
        const string code = "invited-microsoft-user";
        string emailAddress = TestOAuthProviderClient.GetEmailAddress(code);
        var organization = (await _organizationRepository.GetAllAsync()).Documents.First();
        var invite = new Invite {
            Token = StringExtensions.GetNewToken(),
            EmailAddress = emailAddress,
            DateAdded = TimeProvider.GetUtcNow().UtcDateTime
        };
        organization.Invites.Add(invite);
        await _organizationRepository.SaveAsync(organization, options => options.ImmediateConsistency());

        // Act
        await SendMicrosoftLoginAsync(code, inviteToken: invite.Token);

        // Assert
        var user = await _userRepository.GetByEmailAddressAsync(emailAddress);
        Assert.NotNull(user);
        Assert.True(user.IsEmailAddressVerified);
        Assert.Null(user.VerifyEmailAddressToken);
        Assert.Contains(organization.Id, user.OrganizationIds);
        Assert.Equal(0, _mailer.UserEmailVerificationCount);
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

    private Task<TokenResult?> SendMicrosoftLoginAsync(string code, bool isAuthenticated = false, string? inviteToken = null)
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
                    InviteToken = inviteToken,
                    RedirectUri = "http://localhost/callback"
                })
                .StatusCodeShouldBeOk();

            if (isAuthenticated)
                request.AsTestOrganizationUser();
        });
    }
}
