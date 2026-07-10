using System.IdentityModel.Tokens.Jwt;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Configuration;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services;
using Exceptionless.Core.Utility;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Tests.Authentication;
using Exceptionless.Tests.Extensions;
using Exceptionless.Tests.Utility;
using Exceptionless.Web.Models;
using FluentRest;
using Foundatio.Queues;
using Foundatio.Repositories;
using Foundatio.Repositories.Utility;
using Microsoft.AspNetCore.Mvc;
using Xunit;
using User = Exceptionless.Core.Models.User;

namespace Exceptionless.Tests.Controllers;

public class AuthControllerTests : IntegrationTestsBase
{
    private readonly AuthOptions _authOptions;
    private readonly IntercomOptions _intercomOptions;
    private readonly IUserRepository _userRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ITokenRepository _tokenRepository;
    private readonly IOAuthTokenRepository _oauthTokenRepository;
    private readonly AuthOptionsState _originalAuthOptions;

    public AuthControllerTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _authOptions = GetService<AuthOptions>();
        _originalAuthOptions = AuthOptionsState.Capture(_authOptions);
        _intercomOptions = GetService<IntercomOptions>();

        _organizationRepository = GetService<IOrganizationRepository>();
        _userRepository = GetService<IUserRepository>();
        _tokenRepository = GetService<ITokenRepository>();
        _oauthTokenRepository = GetService<IOAuthTokenRepository>();
    }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        ConfigureAuthOptions();
        var service = GetService<SampleDataService>();
        await service.CreateDataAsync();
    }

    public override ValueTask DisposeAsync()
    {
        _originalAuthOptions.Apply(_authOptions);
        return base.DisposeAsync();
    }

    private void ConfigureAuthOptions()
    {
        _authOptions.EnableAccountCreation = true;
        _authOptions.EnableActiveDirectoryAuth = false;
        _authOptions.FacebookId = "facebook-client-id";
        _authOptions.FacebookSecret = "facebook-client-secret";
        _authOptions.GitHubId = "github-client-id";
        _authOptions.GitHubSecret = "github-client-secret";
        _authOptions.GoogleId = "google-client-id";
        _authOptions.GoogleSecret = "google-client-secret";
        _authOptions.MicrosoftId = "microsoft-client-id";
        _authOptions.MicrosoftSecret = "microsoft-client-secret";
    }

    [Fact]
    public async Task CannotSignupWithoutPassword()
    {
        var problemDetails = await SendRequestAsAsync<ValidationProblemDetails>(r => r
            .Post()
            .AppendPath("auth/signup")
            .Content(new Signup
            {
                Name = "hello",
                Email = "test@domain.com",
                Password = null!
            })
            .StatusCodeShouldBeUnprocessableEntity()
        );

        Assert.NotNull(problemDetails);
        Assert.Single(problemDetails.Errors);
        Assert.Contains(problemDetails.Errors, error => String.Equals(error.Key, "password"));
    }

    [Theory]
    [InlineData(true, TestDomainLoginProvider.ValidUsername, TestDomainLoginProvider.ValidPassword)]
    [InlineData(true, "test1.2@exceptionless.io", TestDomainLoginProvider.ValidPassword)]
    [InlineData(false, "test1@exceptionless.io", "Password1$")]
    public Task CannotSignupWhenAccountCreationDisabledWithNoTokenAsync(bool enableAdAuth, string email, string password)
    {
        _authOptions.EnableAccountCreation = false;
        _authOptions.EnableActiveDirectoryAuth = enableAdAuth;

        if (enableAdAuth && email == TestDomainLoginProvider.ValidUsername)
        {
            var provider = new TestDomainLoginProvider();
            email = provider.GetEmailAddressFromUsername(email);
        }

        return SendRequestAsync(r => r
            .Post()
            .AppendPath("auth/signup")
            .Content(new Signup
            {
                Name = "Test",
                Email = email,
                Password = password,
                InviteToken = null
            })
            .StatusCodeShouldBeForbidden()
        );
    }

    [Theory]
    [InlineData(true, TestDomainLoginProvider.ValidUsername, TestDomainLoginProvider.ValidPassword)]
    [InlineData(true, "test2.2@exceptionless.io", TestDomainLoginProvider.ValidPassword)]
    [InlineData(false, "test2@exceptionless.io", "Password1$")]
    public Task CannotSignupWhenAccountCreationDisabledWithInvalidTokenAsync(bool enableAdAuth, string email, string password)
    {
        _authOptions.EnableAccountCreation = false;
        _authOptions.EnableActiveDirectoryAuth = enableAdAuth;

        if (enableAdAuth && email == TestDomainLoginProvider.ValidUsername)
        {
            var provider = new TestDomainLoginProvider();
            email = provider.GetEmailAddressFromUsername(email);
        }

        return SendRequestAsync(r => r
            .Post()
            .AppendPath("auth/signup")
            .Content(new Signup
            {
                Name = "Test",
                Email = email,
                Password = password,
                InviteToken = StringExtensions.GetNewToken()
            })
            .StatusCodeShouldBeForbidden()
        );
    }

    [Theory]
    [InlineData(true, TestDomainLoginProvider.ValidUsername, TestDomainLoginProvider.ValidPassword)]
    [InlineData(false, "test3@exceptionless.io", "Password1$")]
    public async Task CanSignupWhenAccountCreationDisabledWithValidTokenAsync(bool enableAdAuth, string email, string password)
    {
        _authOptions.EnableAccountCreation = false;
        _authOptions.EnableActiveDirectoryAuth = enableAdAuth;

        if (enableAdAuth && email == TestDomainLoginProvider.ValidUsername)
        {
            var provider = new TestDomainLoginProvider();
            email = provider.GetEmailAddressFromUsername(email);
        }

        var results = await _organizationRepository.GetAllAsync();
        var organization = results.Documents.First();

        var invite = new Invite
        {
            Token = StringExtensions.GetNewToken(),
            EmailAddress = email.ToLowerInvariant(),
            DateAdded = DateTime.UtcNow
        };
        organization.Invites.Add(invite);
        organization = await _organizationRepository.SaveAsync(organization, o => o.ImmediateConsistency());
        Assert.NotNull(organization.GetInvite(invite.Token));

        var result = await SendRequestAsAsync<TokenResult>(r => r
           .Post()
           .AppendPath("auth/signup")
           .Content(new Signup
           {
               Name = "Test",
               Email = email,
               Password = password,
               InviteToken = invite.Token
           })
           .StatusCodeShouldBeOk()
       );

        Assert.NotNull(result);
        Assert.False(String.IsNullOrEmpty(result.Token));

        var user = await _userRepository.GetByEmailAddressAsync(email);
        Assert.NotNull(user);
        Assert.Equal("Test", user.FullName);
        Assert.Equal(email, user.EmailAddress);
        Assert.NotEqual(password, user.Password);
        Assert.Contains(user.OrganizationIds, o => String.Equals(o, organization.Id));

        // Assert user is verified due to the invite.
        Assert.True(user.IsEmailAddressVerified);
        Assert.Null(user.VerifyEmailAddressToken);
        Assert.Equal(DateTime.MinValue, user.VerifyEmailAddressTokenExpiration);
    }

    [Fact]
    public async Task CanSignupWhenAccountCreationDisabledWithValidTokenAndInvalidAdAccountAsync()
    {
        _authOptions.EnableAccountCreation = false;
        _authOptions.EnableActiveDirectoryAuth = true;

        const string email = "test-user1@exceptionless.io";
        const string password = "invalidAccount1";

        var organizations = await _organizationRepository.GetAllAsync();
        var organization = organizations.Documents.First();
        var invite = new Invite
        {
            Token = StringExtensions.GetNewToken(),
            EmailAddress = email.ToLowerInvariant(),
            DateAdded = DateTime.UtcNow
        };

        organization.Invites.Add(invite);
        await _organizationRepository.SaveAsync(organization, o => o.ImmediateConsistency());
        Assert.NotNull(organization.GetInvite(invite.Token));

        await SendRequestAsync(r => r
           .Post()
           .AppendPath("auth/signup")
           .Content(new Signup
           {
               Name = "Test",
               Email = email,
               Password = password,
               InviteToken = invite.Token
           })
           .StatusCodeShouldBeUnauthorized()
        );
    }

    [Fact]
    public async Task CanSignupWhenAccountCreationEnabledWithNoTokenAsync()
    {
        _authOptions.EnableAccountCreation = true;

        const string email = "test4@exceptionless.io";
        const string password = "Password1$";

        var result = await SendRequestAsAsync<TokenResult>(r => r
           .Post()
           .AppendPath("auth/signup")
           .Content(new Signup
           {
               Name = "Test",
               Email = email,
               Password = password,
               InviteToken = null
           })
           .StatusCodeShouldBeOk()
        );

        Assert.NotNull(result);
        Assert.False(String.IsNullOrEmpty(result.Token));

        var user = await _userRepository.GetByEmailAddressAsync(email);
        Assert.NotNull(user);
        Assert.Equal("Test", user.FullName);
        Assert.Equal(email, user.EmailAddress);
        Assert.NotEqual(password, user.Password);
        Assert.Empty(user.OrganizationIds);

        Assert.False(user.IsEmailAddressVerified);
        Assert.NotNull(user.VerifyEmailAddressToken);
        Assert.NotEqual(DateTime.MinValue, user.VerifyEmailAddressTokenExpiration);
    }

    [Fact]
    public async Task CanSignupWhenAccountCreationEnabledWithNoTokenAndValidAdAccountAsync()
    {
        _authOptions.EnableAccountCreation = true;
        _authOptions.EnableActiveDirectoryAuth = true;

        var provider = new TestDomainLoginProvider();
        string email = provider.GetEmailAddressFromUsername(TestDomainLoginProvider.ValidUsername);

        var result = await SendRequestAsAsync<TokenResult>(r => r
           .Post()
           .AppendPath("auth/signup")
           .Content(new Signup
           {
               Name = "Test",
               Email = email,
               Password = TestDomainLoginProvider.ValidPassword,
               InviteToken = null
           })
           .StatusCodeShouldBeOk()
        );

        Assert.NotNull(result);
        Assert.False(String.IsNullOrEmpty(result.Token));
    }

    [Fact]
    public Task CanSignupWhenAccountCreationEnabledWithNoTokenAndInvalidAdAccountAsync()
    {
        _authOptions.EnableAccountCreation = true;
        _authOptions.EnableActiveDirectoryAuth = true;

        return SendRequestAsync(r => r
           .Post()
           .AppendPath("auth/signup")
           .Content(new Signup
           {
               Name = "Test",
               Email = "testuser2@exceptionless.io",
               Password = "literallydoesntmatter",
               InviteToken = null
           })
           .StatusCodeShouldBeUnauthorized()
        );
    }

    [Fact]
    public async Task CanSignupWhenAccountCreationEnabledWithValidTokenAsync()
    {
        _authOptions.EnableAccountCreation = true;

        var organizations = await _organizationRepository.GetAllAsync();
        var organization = organizations.Documents.First();
        const string email = "test5@exceptionless.io";
        const string name = "Test";
        const string password = "Password1$";

        var invite = new Invite
        {
            Token = StringExtensions.GetNewToken(),
            EmailAddress = email.ToLowerInvariant(),
            DateAdded = DateTime.UtcNow
        };

        organization.Invites.Clear();
        organization.Invites.Add(invite);
        await _organizationRepository.SaveAsync(organization, o => o.ImmediateConsistency());
        Assert.NotNull(organization.GetInvite(invite.Token));

        var result = await SendRequestAsAsync<TokenResult>(r => r
           .Post()
           .AppendPath("auth/signup")
           .Content(new Signup
           {
               Name = name,
               Email = email,
               Password = password,
               InviteToken = invite.Token
           })
           .StatusCodeShouldBeOk()
        );

        Assert.NotNull(result);
        Assert.False(String.IsNullOrEmpty(result.Token));

        await RefreshDataAsync();

        var user = await _userRepository.GetByEmailAddressAsync(email);
        Assert.NotNull(user);
        Assert.Equal("Test", user.FullName);
        Assert.NotEmpty(user.OrganizationIds);
        Assert.NotNull(user.Salt);
        Assert.True(user.IsEmailAddressVerified);
        Assert.Equal(password.ToSaltedHash(user.Salt), user.Password);
        Assert.Contains(organization.Id, user.OrganizationIds);

        organization = await _organizationRepository.GetByIdAsync(organization.Id);
        Assert.NotNull(organization);
        Assert.Empty(organization.Invites);

        var token = await _tokenRepository.GetByIdAsync(result.Token);
        Assert.NotNull(token);
        Assert.Equal(user.Id, token.UserId);
        Assert.Equal(TokenType.Authentication, token.Type);

        var mailQueue = GetService<IQueue<MailMessage>>() as InMemoryQueue<MailMessage>;
        Assert.NotNull(mailQueue);
        Assert.Equal(0, (await mailQueue.GetQueueStatsAsync()).Enqueued);
    }

    [Fact]
    public async Task CanSignupWhenAccountCreationEnabledWithValidTokenAndValidAdAccountAsync()
    {
        _authOptions.EnableAccountCreation = true;
        _authOptions.EnableActiveDirectoryAuth = true;

        var provider = new TestDomainLoginProvider();
        string email = provider.GetEmailAddressFromUsername(TestDomainLoginProvider.ValidUsername);

        var results = await _organizationRepository.GetAllAsync();
        var organization = results.Documents.First();
        var invite = new Invite
        {
            Token = StringExtensions.GetNewToken(),
            EmailAddress = email.ToLowerInvariant(),
            DateAdded = DateTime.UtcNow
        };
        organization.Invites.Add(invite);
        await _organizationRepository.SaveAsync(organization, o => o.ImmediateConsistency());
        Assert.NotNull(organization.GetInvite(invite.Token));

        var result = await SendRequestAsAsync<TokenResult>(r => r
           .Post()
           .AppendPath("auth/signup")
           .Content(new Signup
           {
               Name = "Test",
               Email = email,
               Password = TestDomainLoginProvider.ValidPassword,
               InviteToken = invite.Token
           })
           .StatusCodeShouldBeOk()
        );

        Assert.NotNull(result);
        Assert.False(String.IsNullOrEmpty(result.Token));
    }

    [Fact]
    public async Task CanSignupWhenAccountCreationEnabledWithValidTokenAndInvalidAdAccountAsync()
    {
        _authOptions.EnableAccountCreation = true;
        _authOptions.EnableActiveDirectoryAuth = true;

        string email = "test-user4@exceptionless.io";
        var results = await _organizationRepository.GetAllAsync();
        var organization = results.Documents.First();
        var invite = new Invite
        {
            Token = StringExtensions.GetNewToken(),
            EmailAddress = email.ToLowerInvariant(),
            DateAdded = DateTime.UtcNow
        };
        organization.Invites.Add(invite);
        await _organizationRepository.SaveAsync(organization, o => o.ImmediateConsistency());
        Assert.NotNull(organization.GetInvite(invite.Token));

        await SendRequestAsync(r => r
           .Post()
           .AppendPath("auth/signup")
           .Content(new Signup
           {
               Name = "Test",
               Email = email,
               Password = TestDomainLoginProvider.ValidPassword,
               InviteToken = invite.Token
           })
           .StatusCodeShouldBeUnauthorized()
        );
    }

    [Fact]
    public async Task SignupShouldFailWhenUsingExistingAccountWithNoPasswordOrInvalidPassword()
    {
        const string email = "test6@exceptionless.io";
        const string password = "Test6 password";
        const string salt = "1234567890123456";
        string passwordHash = password.ToSaltedHash(salt);

        var user = new User
        {
            EmailAddress = email,
            Password = passwordHash,
            Salt = salt,
            FullName = "User 6"
        };

        user.MarkEmailAddressVerified();
        await _userRepository.AddAsync(user);

        var problemDetails = await SendRequestAsAsync<ValidationProblemDetails>(r => r
            .Post()
            .AppendPath("auth/signup")
            .Content(new Signup
            {
                Name = "Random Name",
                Email = email,
                Password = null!
            })
            .StatusCodeShouldBeUnprocessableEntity()
        );

        Assert.NotNull(problemDetails);
        Assert.Single(problemDetails.Errors);
        Assert.Contains(problemDetails.Errors, error => String.Equals(error.Key, "password"));

        await SendRequestAsync(r => r
            .Post()
            .AppendPath("auth/signup")
            .Content(new Signup
            {
                Name = "Random Name",
                Email = email,
                Password = "invalidPass"
            })
            .StatusCodeShouldBeUnauthorized()
        );
    }

    [Fact]
    public async Task FacebookAsync_WithConfiguredProvider_ReturnsToken()
    {
        // Arrange
        const string code = "facebook-user";

        // Act
        var result = await SendExternalLoginAsync("facebook", code);

        // Assert
        await AssertExternalLoginAsync(result, "facebook", code);
    }

    [Fact]
    public async Task GitHubAsync_WithConfiguredProvider_ReturnsToken()
    {
        // Arrange
        const string code = "github-user";

        // Act
        var result = await SendExternalLoginAsync("github", code);

        // Assert
        await AssertExternalLoginAsync(result, "github", code);
    }

    [Fact]
    public async Task GoogleAsync_WithConfiguredProvider_ReturnsToken()
    {
        // Arrange
        const string code = "google-user";

        // Act
        var result = await SendExternalLoginAsync("google", code);

        // Assert
        await AssertExternalLoginAsync(result, "google", code);
    }

    [Fact]
    public async Task LiveAsync_WithConfiguredProvider_ReturnsToken()
    {
        // Arrange
        const string code = "live-user";

        // Act
        var result = await SendExternalLoginAsync("live", code);

        // Assert
        await AssertExternalLoginAsync(result, "windowslive", code);
    }

    [Fact]
    public async Task LoginValidAsync()
    {
        _authOptions.EnableActiveDirectoryAuth = false;

        const string email = "test6@exceptionless.io";
        const string password = "Test6 password";
        const string salt = "1234567890123456";
        string passwordHash = password.ToSaltedHash(salt);
        var user = new User
        {
            EmailAddress = email,
            Password = passwordHash,
            Salt = salt,
            FullName = "User 6"
        };

        user.MarkEmailAddressVerified();
        await _userRepository.AddAsync(user);

        var result = await SendRequestAsAsync<TokenResult>(r => r
           .Post()
           .AppendPath("auth/login")
           .Content(new Login
           {
               Email = email,
               Password = password
           })
           .StatusCodeShouldBeOk()
        );

        Assert.NotNull(result);
        Assert.False(String.IsNullOrEmpty(result.Token));
    }

    [Fact]
    public async Task RemoveExternalLoginAsync_WithLinkedAccount_RemovesAccount()
    {
        // Arrange
        const string providerName = "github";
        const string providerUserId = "github-remove-user";
        var user = await _userRepository.GetByEmailAddressAsync(SampleDataService.TEST_ORG_USER_EMAIL);
        Assert.NotNull(user);
        user.AddOAuthAccount(providerName, providerUserId, user.EmailAddress);
        await _userRepository.SaveAsync(user, o => o.ImmediateConsistency().Cache());

        // Act
        var result = await SendRequestAsAsync<TokenResult>(r => r
            .Post()
            .AsTestOrganizationUser()
            .AppendPaths("auth", "unlink", providerName)
            .Content(new ValueFromBody<string>(providerUserId))
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(result);
        Assert.False(String.IsNullOrEmpty(result.Token));
        var updatedUser = await _userRepository.GetByIdAsync(user.Id);
        Assert.NotNull(updatedUser);
        Assert.DoesNotContain(updatedUser.OAuthAccounts, account => account.Provider == providerName && account.ProviderUserId == providerUserId);
    }

    [Fact]
    public Task RemoveExternalLoginAsync_WithoutProviderUserId_ReturnsBadRequest()
    {
        // Arrange
        var providerUserId = new ValueFromBody<string>(String.Empty);

        // Act & Assert
        return SendRequestAsync(r => r
            .Post()
            .AsTestOrganizationUser()
            .AppendPaths("auth", "unlink", "github")
            .Content(providerUserId)
            .StatusCodeShouldBeBadRequest()
        );
    }

    [Fact]
    public async Task LoginInvalidPasswordAsync()
    {
        _authOptions.EnableActiveDirectoryAuth = false;

        const string email = "test7@exceptionless.io";
        const string password = "Test7 password";
        const string salt = "1234567890123456";
        string passwordHash = password.ToSaltedHash(salt);

        var user = new User
        {
            EmailAddress = email,
            Password = passwordHash,
            Salt = salt,
            FullName = "User 7"
        };

        user.MarkEmailAddressVerified();
        await _userRepository.AddAsync(user);

        await SendRequestAsync(r => r
           .Post()
           .AppendPath("auth/login")
           .Content(new Login
           {
               Email = email,
               Password = "This password ain't right"
           })
           .StatusCodeShouldBeUnauthorized()
        );
    }

    [Fact]
    public async Task LoginNoSuchUserAsync()
    {
        _authOptions.EnableActiveDirectoryAuth = false;

        const string email = "test8@exceptionless.io";
        const string password = "Test8 password";
        const string salt = "1234567890123456";
        string passwordHash = password.ToSaltedHash(salt);
        var user = new User
        {
            EmailAddress = email,
            Password = passwordHash,
            Salt = salt,
            FullName = "User 8"
        };

        user.MarkEmailAddressVerified();
        await _userRepository.AddAsync(user);

        await SendRequestAsync(r => r
           .Post()
           .AppendPath("auth/login")
           .Content(new Login
           {
               Email = "Thisguydoesntexist@exceptionless.io",
               Password = "This password ain't right"
           })
           .StatusCodeShouldBeUnauthorized()
        );
    }

    [Fact]
    public async Task LoginValidExistingActiveDirectoryAsync()
    {
        _authOptions.EnableActiveDirectoryAuth = true;

        var provider = new TestDomainLoginProvider();
        string email = provider.GetEmailAddressFromUsername(TestDomainLoginProvider.ValidUsername);
        var user = new User
        {
            EmailAddress = email,
            FullName = "User 6"
        };

        user.MarkEmailAddressVerified();
        await _userRepository.AddAsync(user);

        var result = await SendRequestAsAsync<TokenResult>(r => r
           .Post()
           .AppendPath("auth/login")
           .Content(new Login
           {
               Email = email,
               Password = TestDomainLoginProvider.ValidPassword
           })
           .StatusCodeShouldBeOk()
        );

        Assert.NotNull(result);
        Assert.False(String.IsNullOrEmpty(result.Token));
    }

    [Fact]
    public Task LoginValidNonExistentActiveDirectoryAsync()
    {
        _authOptions.EnableActiveDirectoryAuth = true;

        var provider = new TestDomainLoginProvider();
        string email = provider.GetEmailAddressFromUsername(TestDomainLoginProvider.ValidUsername);

        return SendRequestAsync(r => r
           .Post()
           .AppendPath("auth/login")
           .Content(new Login
           {
               Email = email,
               Password = TestDomainLoginProvider.ValidPassword
           })
           .StatusCodeShouldBeUnauthorized()
        );
    }

    [Fact]
    public async Task LoginInvalidNonExistentActiveDirectoryAsync()
    {
        _authOptions.EnableActiveDirectoryAuth = true;
        var provider = new TestDomainLoginProvider();
        string email = provider.GetEmailAddressFromUsername(TestDomainLoginProvider.ValidUsername);

        await SendRequestAsync(r => r
           .Post()
           .AppendPath("auth/login")
           .Content(new Login
           {
               Email = $"{email}.au",
               Password = "Totallywrongpassword1234"
           })
           .StatusCodeShouldBeUnauthorized()
        );

        // Verify that a user account was not added
        var user = await _userRepository.GetByEmailAddressAsync($"{email}.au");
        Assert.Null(user);
    }

    [Fact]
    public async Task LoginInvalidExistingActiveDirectoryAsync()
    {
        _authOptions.EnableActiveDirectoryAuth = true;

        var provider = new TestDomainLoginProvider();
        string email = provider.GetEmailAddressFromUsername(TestDomainLoginProvider.ValidUsername);
        var user = new User
        {
            EmailAddress = email,
            FullName = "User 6"
        };

        user.MarkEmailAddressVerified();
        await _userRepository.AddAsync(user);

        await SendRequestAsync(r => r
           .Post()
           .AppendPath("auth/login")
           .Content(new Login
           {
               Email = email,
               Password = "Totallywrongpassword1234"
           })
           .StatusCodeShouldBeUnauthorized()
        );
    }

    [Fact]
    public async Task LoginInvalidExistingActiveDirectoryAccountUsingUserNameLoginAsync()
    {
        _authOptions.EnableActiveDirectoryAuth = true;

        var provider = new TestDomainLoginProvider();
        string email = provider.GetEmailAddressFromUsername(TestDomainLoginProvider.ValidUsername);
        var user = new User
        {
            EmailAddress = email,
            FullName = "User 6"
        };

        user.MarkEmailAddressVerified();
        await _userRepository.AddAsync(user);

        await SendRequestAsync(r => r
            .Post()
            .AppendPath("auth/login")
            .Content(new Login
            {
                Email = TestDomainLoginProvider.ValidUsername,
                Password = "Totallywrongpassword1234"
            })
            .StatusCodeShouldBeUnauthorized()
        );
    }

    [Fact]
    public async Task CanChangePasswordAsync()
    {
        const string email = "test6@exceptionless.io";
        const string password = "Test6 password";
        const string salt = "1234567890123456";
        string passwordHash = password.ToSaltedHash(salt);

        var user = new User
        {
            EmailAddress = email,
            Password = passwordHash,
            Salt = salt,
            FullName = "User 6",
            Roles = AuthorizationRoles.AllScopes
        };

        user.MarkEmailAddressVerified();
        await _userRepository.AddAsync(user);

        var result = await SendRequestAsAsync<TokenResult>(r => r
            .Post()
            .AppendPath("auth/login")
            .Content(new Login
            {
                Email = email,
                Password = password,
            })
            .StatusCodeShouldBeOk()
        );

        Assert.NotNull(result);
        Assert.NotEmpty(result.Token);

        var token = await _tokenRepository.GetByIdAsync(result.Token);
        Assert.NotNull(token);

        Assert.NotNull(token.UserId);
        var actualUser = await _userRepository.GetByIdAsync(token.UserId);
        Assert.NotNull(actualUser);
        Assert.Equal(email, actualUser.EmailAddress);
        var utcNow = TimeProvider.GetUtcNow().UtcDateTime;
        var oauthToken = await _oauthTokenRepository.AddAsync(new OAuthToken
        {
            Id = ObjectId.GenerateNewId().ToString(),
            UserId = actualUser.Id,
            ClientId = "test-change-password-client",
            GrantId = StringExtensions.GetNewToken(),
            Resource = "http://localhost:7110/mcp",
            AccessTokenHash = OAuthService.CreateTokenHash("change-password-oauth-access-token"),
            RefreshTokenHash = OAuthService.CreateTokenHash("change-password-oauth-refresh-token"),
            OrganizationIds = [TestConstants.OrganizationId],
            Scopes = [AuthorizationRoles.McpRead, AuthorizationRoles.OfflineAccess],
            CreatedBy = actualUser.Id,
            CreatedUtc = utcNow,
            UpdatedUtc = utcNow
        }, o => o.ImmediateConsistency());

        const string newPassword = "NewP@ssword2";
        var changePasswordResult = await SendRequestAsAsync<TokenResult>(r => r
            .Post()
            .BasicAuthorization(email, password)
            .AppendPath("auth/change-password")
            .Content(new ChangePasswordModel
            {
                CurrentPassword = password,
                Password = newPassword
            })
            .StatusCodeShouldBeOk()
        );

        Assert.NotNull(changePasswordResult);
        Assert.NotEmpty(changePasswordResult.Token);

        Assert.Null(await _tokenRepository.GetByIdAsync(result.Token));
        Assert.Null(await _oauthTokenRepository.GetByIdAsync(oauthToken.Id, o => o.ImmediateConsistency()));
        Assert.NotNull(await _tokenRepository.GetByIdAsync(changePasswordResult.Token));
    }

    [Fact]
    public async Task ChangePasswordShouldFailWithCurrentPasswordAsync()
    {
        const string email = "test6@exceptionless.io";
        const string password = "Test6 password";
        const string salt = "1234567890123456";
        string passwordHash = password.ToSaltedHash(salt);

        var user = new User
        {
            EmailAddress = email,
            Password = passwordHash,
            Salt = salt,
            FullName = "User 6",
            Roles = AuthorizationRoles.AllScopes
        };

        user.MarkEmailAddressVerified();
        await _userRepository.AddAsync(user);

        var result = await SendRequestAsAsync<TokenResult>(r => r
            .Post()
            .AppendPath("auth/login")
            .Content(new Login
            {
                Email = email,
                Password = password,
            })
            .StatusCodeShouldBeOk()
        );

        Assert.NotNull(result);
        Assert.NotEmpty(result.Token);

        var token = await _tokenRepository.GetByIdAsync(result.Token);
        Assert.NotNull(token);

        Assert.NotNull(token.UserId);
        var actualUser = await _userRepository.GetByIdAsync(token.UserId);
        Assert.NotNull(actualUser);
        Assert.Equal(email, actualUser.EmailAddress);

        var problemDetails = await SendRequestAsAsync<ValidationProblemDetails>(r => r
            .Post()
            .BasicAuthorization(email, password)
            .AppendPath("auth/change-password")
            .Content(new ChangePasswordModel
            {
                CurrentPassword = password,
                Password = password
            })
            .StatusCodeShouldBeUnprocessableEntity()
        );

        Assert.NotNull(problemDetails);
        Assert.Single(problemDetails.Errors);
        Assert.Contains(problemDetails.Errors, error => String.Equals(error.Key, "password"));

        Assert.NotNull(await _tokenRepository.GetByIdAsync(result.Token));
    }

    [Fact]
    public async Task CanResetPasswordAsync()
    {
        const string email = "test6@exceptionless.io";
        const string password = "Test6 password";
        const string salt = "1234567890123456";
        string passwordHash = password.ToSaltedHash(salt);

        var user = new User
        {
            EmailAddress = email,
            Password = passwordHash,
            Salt = salt,
            FullName = "User 6",
            Roles = AuthorizationRoles.AllScopes
        };

        user.MarkEmailAddressVerified();
        user.CreatePasswordResetToken(TimeProvider);
        Assert.NotNull(user.PasswordResetToken);
        Assert.True(user.PasswordResetTokenExpiration.IsAfter(TimeProvider.GetUtcNow().UtcDateTime));

        await _userRepository.AddAsync(user);

        var result = await SendRequestAsAsync<TokenResult>(r => r
            .Post()
            .AppendPath("auth/login")
            .Content(new Login
            {
                Email = email,
                Password = password,
            })
            .StatusCodeShouldBeOk()
        );

        Assert.NotNull(result);
        Assert.NotEmpty(result.Token);

        var token = await _tokenRepository.GetByIdAsync(result.Token);
        Assert.NotNull(token);

        Assert.NotNull(token.UserId);
        var actualUser = await _userRepository.GetByIdAsync(token.UserId);
        Assert.NotNull(actualUser);
        Assert.Equal(email, actualUser.EmailAddress);
        var utcNow = TimeProvider.GetUtcNow().UtcDateTime;
        var oauthToken = await _oauthTokenRepository.AddAsync(new OAuthToken
        {
            Id = ObjectId.GenerateNewId().ToString(),
            UserId = actualUser.Id,
            ClientId = "test-change-password-client",
            GrantId = StringExtensions.GetNewToken(),
            Resource = "http://localhost:7110/mcp",
            AccessTokenHash = OAuthService.CreateTokenHash("change-password-oauth-access-token"),
            RefreshTokenHash = OAuthService.CreateTokenHash("change-password-oauth-refresh-token"),
            OrganizationIds = [TestConstants.OrganizationId],
            Scopes = [AuthorizationRoles.McpRead, AuthorizationRoles.OfflineAccess],
            CreatedBy = actualUser.Id,
            CreatedUtc = utcNow,
            UpdatedUtc = utcNow
        }, o => o.ImmediateConsistency());

        const string newPassword = "NewP@ssword2";
        await SendRequestAsync(r => r
            .Post()
            .BasicAuthorization(email, password)
            .AppendPath("auth/reset-password")
            .Content(new ResetPasswordModel
            {
                PasswordResetToken = user.PasswordResetToken,
                Password = newPassword
            })
            .StatusCodeShouldBeOk()
        );

        Assert.Null(await _tokenRepository.GetByIdAsync(result.Token));
        Assert.Null(await _oauthTokenRepository.GetByIdAsync(oauthToken.Id, o => o.ImmediateConsistency()));
    }

    [Fact]
    public async Task ResetPasswordShouldFailWithCurrentPasswordAsync()
    {
        const string email = "test6@exceptionless.io";
        const string password = "Test6 password";
        const string salt = "1234567890123456";
        string passwordHash = password.ToSaltedHash(salt);

        var user = new User
        {
            EmailAddress = email,
            Password = passwordHash,
            Salt = salt,
            FullName = "User 6",
            Roles = AuthorizationRoles.AllScopes
        };

        user.MarkEmailAddressVerified();
        user.CreatePasswordResetToken(TimeProvider);
        Assert.NotNull(user.PasswordResetToken);
        Assert.True(user.PasswordResetTokenExpiration.IsAfter(TimeProvider.GetUtcNow().UtcDateTime));

        await _userRepository.AddAsync(user);

        var result = await SendRequestAsAsync<TokenResult>(r => r
            .Post()
            .AppendPath("auth/login")
            .Content(new Login
            {
                Email = email,
                Password = password,
            })
            .StatusCodeShouldBeOk()
        );

        Assert.NotNull(result);
        Assert.NotEmpty(result.Token);

        var token = await _tokenRepository.GetByIdAsync(result.Token);
        Assert.NotNull(token);

        Assert.NotNull(token.UserId);
        var actualUser = await _userRepository.GetByIdAsync(token.UserId);
        Assert.NotNull(actualUser);
        Assert.Equal(email, actualUser.EmailAddress);

        var problemDetails = await SendRequestAsAsync<ValidationProblemDetails>(r => r
            .Post()
            .BasicAuthorization(email, password)
            .AppendPath("auth/reset-password")
            .Content(new ResetPasswordModel
            {
                PasswordResetToken = user.PasswordResetToken,
                Password = password
            })
            .StatusCodeShouldBeUnprocessableEntity()
        );

        Assert.NotNull(problemDetails);
        Assert.Single(problemDetails.Errors);
        Assert.Contains(problemDetails.Errors, error => String.Equals(error.Key, "password"));

        Assert.NotNull(await _tokenRepository.GetByIdAsync(result.Token));
    }

    [Fact]
    public async Task ForgotPasswordCreatesResetTokenAsync()
    {
        const string email = "forgot-password@exceptionless.io";
        var user = new User
        {
            EmailAddress = email,
            FullName = "Forgot Password",
            Roles = AuthorizationRoles.AllScopes
        };

        user.MarkEmailAddressVerified();
        await _userRepository.AddAsync(user);

        await SendRequestAsync(r => r
            .AppendPath($"auth/forgot-password/{email}")
            .StatusCodeShouldBeOk()
        );

        var updatedUser = await _userRepository.GetByEmailAddressAsync(email);
        Assert.NotNull(updatedUser);
        Assert.False(String.IsNullOrEmpty(updatedUser.PasswordResetToken));
        Assert.True(updatedUser.PasswordResetTokenExpiration.IsAfter(TimeProvider.GetUtcNow().UtcDateTime));
    }

    [Fact]
    public Task ForgotPasswordForUnknownEmailReturnsOkAsync()
    {
        return SendRequestAsync(r => r
            .AppendPath("auth/forgot-password/missing-password-user@exceptionless.io")
            .StatusCodeShouldBeOk()
        );
    }

    [Fact]
    public async Task CancelResetPasswordClearsTokenAsync()
    {
        const string email = "cancel-reset-password@exceptionless.io";
        var user = new User
        {
            EmailAddress = email,
            FullName = "Cancel Reset Password",
            Roles = AuthorizationRoles.AllScopes
        };

        user.MarkEmailAddressVerified();
        user.CreatePasswordResetToken(TimeProvider);
        string token = user.PasswordResetToken!;
        await _userRepository.AddAsync(user);

        await SendRequestAsync(r => r
            .Post()
            .AppendPath($"auth/cancel-reset-password/{token}")
            .StatusCodeShouldBeOk()
        );

        var updatedUser = await _userRepository.GetByEmailAddressAsync(email);
        Assert.NotNull(updatedUser);
        Assert.Null(updatedUser.PasswordResetToken);
        Assert.Equal(DateTime.MinValue, updatedUser.PasswordResetTokenExpiration);
    }

    [Fact]
    public async Task EmailAddressAvailabilityReturnsCreatedForExistingUserAsync()
    {
        const string email = "existing-email-check@exceptionless.io";
        var user = new User
        {
            EmailAddress = email,
            FullName = "Existing Email Check",
            Roles = AuthorizationRoles.AllScopes
        };

        user.MarkEmailAddressVerified();
        await _userRepository.AddAsync(user);

        await SendRequestAsync(r => r
            .AppendPath($"auth/check-email-address/{email}")
            .StatusCodeShouldBeCreated()
        );
    }

    [Fact]
    public Task EmailAddressAvailabilityReturnsNoContentForMissingUserAsync()
    {
        return SendRequestAsync(r => r
            .AppendPath("auth/check-email-address/missing-email-check@exceptionless.io")
            .StatusCodeShouldBeNoContent()
        );
    }

    [Fact]
    public async Task CanLogoutUserAsync()
    {
        const string email = "test7@exceptionless.io";
        const string password = "Test7 password";
        const string salt = "1234567890123456";
        string passwordHash = password.ToSaltedHash(salt);

        var user = new User
        {
            EmailAddress = email,
            Password = passwordHash,
            Salt = salt,
            FullName = "User 7",
            Roles = AuthorizationRoles.AllScopes
        };

        user.MarkEmailAddressVerified();
        await _userRepository.AddAsync(user);

        var result = await SendRequestAsAsync<TokenResult>(r => r
            .Post()
            .AppendPath("auth/login")
            .Content(new Login
            {
                Email = email,
                Password = password,
            })
            .StatusCodeShouldBeOk()
        );

        Assert.NotNull(result);

        // Verify that the token is valid
        var token = await _tokenRepository.GetByIdAsync(result.Token);
        Assert.NotNull(token);
        Assert.Equal(TokenType.Authentication, token.Type);
        Assert.False(token.IsDisabled);
        Assert.False(token.IsSuspended);

        await SendRequestAsync(r => r
            .BearerToken(result.Token)
            .AppendPath("auth/logout")
            .StatusCodeShouldBeOk()
        );

        token = await _tokenRepository.GetByIdAsync(result.Token);
        Assert.Null(token);
    }

    [Fact]
    public async Task CanLogoutUserAccessTokenAsync()
    {
        var token = await _tokenRepository.GetByIdAsync(TestConstants.UserApiKey);
        Assert.NotNull(token);
        Assert.Equal(TokenType.Access, token.Type);
        Assert.False(token.IsDisabled);
        Assert.False(token.IsSuspended);

        await SendRequestAsync(r => r
            .BearerToken(token.Id)
            .AppendPath("auth/logout")
            .StatusCodeShouldBeForbidden()
        );

        token = (await _tokenRepository.GetByIdAsync(token.Id))!;
        Assert.NotNull(token);
        Assert.Equal(TokenType.Access, token.Type);
        Assert.False(token.IsDisabled);
        Assert.False(token.IsSuspended);
    }


    [Fact]
    public async Task GetIntercomToken_WithValidAuthenticatedUser_ReturnsJwtAsync()
    {
        // Arrange
        _intercomOptions.IntercomSecret = "test-intercom-secret-with-adequate-length-12345";
        const string email = "intercom-token@exceptionless.io";
        const string password = "Test password";
        const string salt = "1234567890123456";
        var issuedAt = new DateTimeOffset(2026, 3, 19, 12, 0, 0, TimeSpan.Zero);

        TimeProvider.SetUtcNow(issuedAt);

        var user = new User
        {
            EmailAddress = email,
            FullName = "Intercom User",
            Password = password.ToSaltedHash(salt),
            Roles = AuthorizationRoles.AllScopes,
            Salt = salt
        };

        user.MarkEmailAddressVerified();
        await _userRepository.AddAsync(user, o => o.ImmediateConsistency());

        var authToken = await SendRequestAsAsync<TokenResult>(r => r
            .Post()
            .AppendPath("auth/login")
            .Content(new Login
            {
                Email = email,
                Password = password
            })
            .StatusCodeShouldBeOk()
        );
        Assert.NotNull(authToken);

        // Act
        var intercomToken = await SendRequestAsAsync<TokenResult>(r => r
            .BearerToken(authToken.Token)
            .AppendPath("auth/intercom")
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(intercomToken);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(intercomToken.Token);
        Assert.Equal(user.Id, jwt.Payload["user_id"]);
        Assert.Equal(issuedAt.UtcDateTime, jwt.Payload.IssuedAt);
        Assert.Equal(issuedAt.AddHours(1).ToUnixTimeSeconds(), jwt.Payload.Expiration);
    }

    [Fact]
    public Task GetIntercomToken_WhenUnauthenticated_ReturnsUnauthorizedAsync()
    {
        // Arrange
        _intercomOptions.IntercomSecret = "test-intercom-secret-with-adequate-length-12345";

        // Act
        return SendRequestAsync(r => r
            .AppendPath("auth/intercom")
            .StatusCodeShouldBeUnauthorized()
        );
    }

    [Fact]
    public async Task GetIntercomToken_WhenIntercomIsDisabled_ReturnsUnprocessableEntityAsync()
    {
        // Arrange
        _intercomOptions.IntercomSecret = null;

        // Act
        var problemDetails = await SendRequestAsAsync<ValidationProblemDetails>(r => r
            .BearerToken(TestConstants.UserApiKey)
            .AppendPath("auth/intercom")
            .StatusCodeShouldBeUnprocessableEntity()
        );

        // Assert
        Assert.NotNull(problemDetails);
        Assert.True(problemDetails.Errors.TryGetValue("intercom", out string[]? intercomErrors));
        Assert.Contains("Intercom is not enabled.", intercomErrors);
    }

    [Fact]
    public async Task CanLogoutClientAccessTokenAsync()
    {
        var token = await _tokenRepository.GetByIdAsync(TestConstants.ApiKey);
        Assert.NotNull(token);
        Assert.Equal(TokenType.Access, token.Type);
        Assert.False(token.IsDisabled);
        Assert.False(token.IsSuspended);

        await SendRequestAsync(r => r
            .BearerToken(token.Id)
            .AppendPath("auth/logout")
            .StatusCodeShouldBeForbidden()
        );

        token = (await _tokenRepository.GetByIdAsync(token.Id))!;
        Assert.NotNull(token);
        Assert.Equal(TokenType.Access, token.Type);
        Assert.False(token.IsDisabled);
        Assert.False(token.IsSuspended);
    }

    private async Task AssertExternalLoginAsync(TokenResult? result, string providerName, string providerUserId)
    {
        Assert.NotNull(result);
        Assert.False(String.IsNullOrEmpty(result.Token));

        var user = await _userRepository.GetByEmailAddressAsync(TestOAuthProviderClient.GetEmailAddress(providerUserId));
        Assert.NotNull(user);
        Assert.True(user.IsEmailAddressVerified);
        var account = Assert.Single(user.OAuthAccounts);
        Assert.Equal(providerName, account.Provider);
        Assert.Equal(providerUserId, account.ProviderUserId);
        Assert.Equal(user.EmailAddress, account.Username);
    }

    private Task<TokenResult?> SendExternalLoginAsync(string providerPath, string code)
    {
        return SendRequestAsAsync<TokenResult>(r => r
            .Post()
            .AppendPaths("auth", providerPath)
            .Content(new ExternalAuthInfo
            {
                ClientId = "client-id",
                Code = code,
                RedirectUri = "http://localhost/callback"
            })
            .StatusCodeShouldBeOk()
        );
    }

    private sealed record AuthOptionsState(
        bool EnableAccountCreation,
        bool EnableActiveDirectoryAuth,
        string? FacebookId,
        string? FacebookSecret,
        string? GitHubId,
        string? GitHubSecret,
        string? GoogleId,
        string? GoogleSecret,
        string? MicrosoftId,
        string? MicrosoftSecret
    )
    {
        public static AuthOptionsState Capture(AuthOptions options)
        {
            return new AuthOptionsState(
                options.EnableAccountCreation,
                options.EnableActiveDirectoryAuth,
                options.FacebookId,
                options.FacebookSecret,
                options.GitHubId,
                options.GitHubSecret,
                options.GoogleId,
                options.GoogleSecret,
                options.MicrosoftId,
                options.MicrosoftSecret);
        }

        public void Apply(AuthOptions options)
        {
            options.EnableAccountCreation = EnableAccountCreation;
            options.EnableActiveDirectoryAuth = EnableActiveDirectoryAuth;
            options.FacebookId = FacebookId;
            options.FacebookSecret = FacebookSecret;
            options.GitHubId = GitHubId;
            options.GitHubSecret = GitHubSecret;
            options.GoogleId = GoogleId;
            options.GoogleSecret = GoogleSecret;
            options.MicrosoftId = MicrosoftId;
            options.MicrosoftSecret = MicrosoftSecret;
        }
    }
}
