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

    [Fact]
    public async Task MicrosoftAsync_AuthenticatedLinkWithDifferentEmail_ReplacesLegacyIdentity()
    {
        // Arrange
        const string code = "authenticated-microsoft-user";
        var user = await _userRepository.GetByEmailAddressAsync(SampleDataService.TEST_ORG_USER_EMAIL);
        Assert.NotNull(user);
        user.MarkEmailAddressVerified();
        user.AddOAuthAccount(OAuthAccount.KnownProviders.WindowsLive, "legacy-user", user.EmailAddress);
        await _userRepository.SaveAsync(user, o => o.ImmediateConsistency().Cache());

        // Act
        var result = await SendMicrosoftLoginAsync(code, isAuthenticated: true);

        // Assert
        Assert.NotNull(result);
        Assert.False(String.IsNullOrEmpty(result.Token));
        var updatedUser = await _userRepository.GetByIdAsync(user.Id);
        Assert.NotNull(updatedUser);
        Assert.Equal(SampleDataService.TEST_ORG_USER_EMAIL, updatedUser.EmailAddress);
        Assert.True(updatedUser.IsEmailAddressVerified);
        var account = Assert.Single(updatedUser.OAuthAccounts);
        Assert.Equal("microsoft", account.Provider);
        Assert.Equal(code, account.ProviderUserId);
        Assert.Equal(TestOAuthProviderClient.GetEmailAddress(code), account.Username);
        Assert.Equal(0, _mailer.UserEmailVerificationCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MicrosoftAsync_ExistingModernIdentity_PreservesEmailVerification(bool isEmailVerified)
    {
        // Arrange
        const string code = "existing-microsoft-user";
        var user = CreateUser("existing-microsoft-owner@exceptionless.test");
        if (!isEmailVerified)
            user.ResetVerifyEmailAddressTokenAndExpiration(TimeProvider);
        string? verificationToken = user.VerifyEmailAddressToken;
        var verificationExpiration = user.VerifyEmailAddressTokenExpiration;
        user.AddOAuthAccount(OAuthAccount.KnownProviders.WindowsLive, "legacy-user", user.EmailAddress);
        user.AddOAuthAccount(OAuthAccount.KnownProviders.Microsoft, code, user.EmailAddress);
        await _userRepository.AddAsync(user, o => o.ImmediateConsistency());

        // Act
        var result = await SendMicrosoftLoginAsync(code);

        // Assert
        Assert.NotNull(result);
        var token = await GetService<ITokenRepository>().GetByIdAsync(result.Token);
        Assert.NotNull(token);
        Assert.Equal(user.Id, token.UserId);
        var updatedUser = await _userRepository.GetByIdAsync(user.Id);
        Assert.NotNull(updatedUser);
        Assert.Equal(user.EmailAddress, updatedUser.EmailAddress);
        var account = Assert.Single(updatedUser.OAuthAccounts);
        Assert.Equal("microsoft", account.Provider);
        Assert.Equal(code, account.ProviderUserId);
        Assert.Equal(TestOAuthProviderClient.GetEmailAddress(code), account.Username);
        Assert.Equal(isEmailVerified, updatedUser.IsEmailAddressVerified);
        Assert.Equal(verificationToken, updatedUser.VerifyEmailAddressToken);
        Assert.Equal(verificationExpiration, updatedUser.VerifyEmailAddressTokenExpiration);
        Assert.Equal(0, _mailer.UserEmailVerificationCount);
    }

    [Fact]
    public async Task MicrosoftAsync_ExactEmailMatch_RequiresAuthenticatedLink()
    {
        // Arrange
        const string code = "matching-email-user";
        var user = CreateUser(TestOAuthProviderClient.GetEmailAddress(code));
        user.AddOAuthAccount(OAuthAccount.KnownProviders.WindowsLive, "legacy-user", user.EmailAddress);
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
        Assert.Equal("Sign in to your existing account first, then link Microsoft from your account settings.", problem.Title);
        Assert.Equal(0, _mailer.UserEmailVerificationCount);
    }

    [Fact]
    public async Task MicrosoftAsync_UnmatchedEmail_CreatesUnverifiedUserAndSendsOneVerification()
    {
        // Arrange
        const string code = "new-microsoft-user";
        string emailAddress = TestOAuthProviderClient.GetEmailAddress(code);

        // Act
        var signup = await SendMicrosoftLoginAsync(code);
        await RefreshDataAsync();
        var login = await SendMicrosoftLoginAsync(code);

        // Assert
        Assert.NotNull(signup);
        Assert.False(String.IsNullOrEmpty(signup.Token));
        Assert.NotNull(login);
        Assert.False(String.IsNullOrEmpty(login.Token));
        var microsoftUser = await _userRepository.GetByEmailAddressAsync(emailAddress);
        Assert.NotNull(microsoftUser);
        var token = await GetService<ITokenRepository>().GetByIdAsync(signup.Token);
        Assert.NotNull(token);
        Assert.Equal(microsoftUser.Id, token.UserId);
        Assert.False(microsoftUser.IsEmailAddressVerified);
        Assert.NotNull(microsoftUser.VerifyEmailAddressToken);
        Assert.True(microsoftUser.HasValidVerifyEmailAddressTokenExpiration(TimeProvider));
        var account = Assert.Single(microsoftUser.OAuthAccounts);
        Assert.Equal("microsoft", account.Provider);
        Assert.Equal(code, account.ProviderUserId);
        Assert.Equal(1, _mailer.UserEmailVerificationCount);
    }

    [Fact]
    public async Task MicrosoftAsync_MatchingInvitation_CreatesUserAndJoinsOrganization()
    {
        // Arrange
        const string code = "invited-microsoft-user";
        _authOptions.EnableAccountCreation = false;
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
        var result = await SendMicrosoftLoginAsync(code, inviteToken: invite.Token);

        // Assert
        Assert.NotNull(result);
        Assert.False(String.IsNullOrEmpty(result.Token));
        var user = await _userRepository.GetByEmailAddressAsync(emailAddress);
        Assert.NotNull(user);
        Assert.True(user.IsEmailAddressVerified);
        Assert.Contains(organization.Id, user.OrganizationIds);
        var updatedOrganization = await _organizationRepository.GetByIdAsync(organization.Id);
        Assert.NotNull(updatedOrganization);
        Assert.DoesNotContain(updatedOrganization.Invites, existing => existing.Token == invite.Token);
        Assert.Null(user.VerifyEmailAddressToken);
        Assert.Equal(0, _mailer.UserEmailVerificationCount);
    }

    [Fact]
    public async Task MicrosoftAsync_UnverifiedAuthenticatedUser_CanLinkIdentity()
    {
        // Arrange
        var user = await _userRepository.GetByEmailAddressAsync(SampleDataService.TEST_ORG_USER_EMAIL);
        Assert.NotNull(user);
        user.ResetVerifyEmailAddressTokenAndExpiration(TimeProvider);
        string? verificationToken = user.VerifyEmailAddressToken;
        user.AddOAuthAccount(OAuthAccount.KnownProviders.WindowsLive, "legacy-user", user.EmailAddress);
        await _userRepository.SaveAsync(user, o => o.ImmediateConsistency().Cache());

        // Act
        var result = await SendMicrosoftLoginAsync("unverified-link", isAuthenticated: true);

        // Assert
        Assert.NotNull(result);
        Assert.False(String.IsNullOrEmpty(result.Token));
        var unchangedUser = await _userRepository.GetByIdAsync(user.Id);
        Assert.NotNull(unchangedUser);
        Assert.False(unchangedUser.IsEmailAddressVerified);
        Assert.Equal(verificationToken, unchangedUser.VerifyEmailAddressToken);
        Assert.Contains(unchangedUser.OAuthAccounts, account => account.Provider == "microsoft" && account.ProviderUserId == "unverified-link");
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

    public override ValueTask DisposeAsync()
    {
        _authOptions.EnableAccountCreation = _originalEnableAccountCreation;
        _authOptions.MicrosoftId = _originalMicrosoftId;
        _authOptions.MicrosoftSecret = _originalMicrosoftSecret;
        return base.DisposeAsync();
    }
}
