using Exceptionless.Core.Authorization;
using Exceptionless.Core.Configuration;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Tests.Authentication;
using Exceptionless.Tests.Extensions;
using Exceptionless.Tests.Utility;
using Exceptionless.Web.Models;
using FluentRest;
using Foundatio.Queues;
using Foundatio.Repositories;
using Microsoft.AspNetCore.Mvc;
using Xunit;
using Xunit.Abstractions;
using User = Exceptionless.Core.Models.User;

namespace Exceptionless.Tests.Controllers;

public class AuthControllerTests : IntegrationTestsBase
{
    private readonly AuthOptions _authOptions;
    private readonly IUserRepository _userRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ITokenRepository _tokenRepository;

    public AuthControllerTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _authOptions = GetService<AuthOptions>();
        _authOptions.EnableAccountCreation = true;
        _authOptions.EnableActiveDirectoryAuth = false;

        _organizationRepository = GetService<IOrganizationRepository>();
        _userRepository = GetService<IUserRepository>();
        _tokenRepository = GetService<ITokenRepository>();
    }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        var service = GetService<SampleDataService>();
        await service.CreateDataAsync();
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

        var actualUser = await _userRepository.GetByIdAsync(token.UserId);
        Assert.NotNull(actualUser);
        Assert.Equal(email, actualUser.EmailAddress);

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

        var actualUser = await _userRepository.GetByIdAsync(token.UserId);
        Assert.NotNull(actualUser);
        Assert.Equal(email, actualUser.EmailAddress);

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

        token = await _tokenRepository.GetByIdAsync(token.Id);
        Assert.Equal(TokenType.Access, token.Type);
        Assert.False(token.IsDisabled);
        Assert.False(token.IsSuspended);
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

        token = await _tokenRepository.GetByIdAsync(token.Id);
        Assert.Equal(TokenType.Access, token.Type);
        Assert.False(token.IsDisabled);
        Assert.False(token.IsSuspended);
    }
}
