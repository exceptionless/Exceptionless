using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using Exceptionless.Api.Controllers;
using Exceptionless.Api.Models;
using Exceptionless.Api.Tests.Authentication;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Tests.Utility;
using Foundatio.Utility;
using Microsoft.Owin;
using Nest;
using Xunit;
using Xunit.Abstractions;
using User = Exceptionless.Core.Models.User;

namespace Exceptionless.Api.Tests.Controllers {
    public class AuthControllerTests : ElasticTestBase {
        private readonly IUserRepository _userRepository;
        private readonly AuthController _authController;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IProjectRepository _projectRepository;

        private const string SIGNUP_ENDPOINT = "signup";
        private const string LOGIN_ENDPOINT = "login";

        public AuthControllerTests(ITestOutputHelper output) : base(output) {
            Settings.Current.EnableAccountCreation = true;
            Settings.Current.EnableActiveDirectoryAuth = false;

            _authController = GetService<AuthController>();
            _organizationRepository = GetService<IOrganizationRepository>();
            _projectRepository = GetService<IProjectRepository>();
            _userRepository = GetService<IUserRepository>();
            CreateOrganizationAndProjectsAsync().GetAwaiter().GetResult();
        }

        [Theory]
        [InlineData(true, TestDomainLoginProvider.ValidUsername, TestDomainLoginProvider.ValidPassword)]
        [InlineData(true, "test1.2@exceptionless.io", TestDomainLoginProvider.ValidPassword)]
        [InlineData(false, "test1@exceptionless.io", "Password1$")]
        public async Task CannotSignupWhenAccountCreationDisabledWithNoTokenAsync(bool enableAdAuth, string email, string password) {
            _authController.Request = CreateRequestMessage(SIGNUP_ENDPOINT, null, false, false);
            Settings.Current.EnableAccountCreation = false;
            Settings.Current.EnableActiveDirectoryAuth = enableAdAuth;

            if (enableAdAuth && email == TestDomainLoginProvider.ValidUsername) {
                TestDomainLoginProvider provider = new TestDomainLoginProvider();
                email = provider.GetEmailAddressFromUsername(email);
            }

            var signupModel = new SignupModel {
                Email = email,
                InviteToken = "",
                Name = "Test",
                Password = password
            };

            var actionResult = await _authController.SignupAsync(signupModel);
            var result = await actionResult.ExecuteAsync(new CancellationToken());
            Assert.False(result.IsSuccessStatusCode, "Status Code is success.");
            Assert.Equal(System.Net.HttpStatusCode.BadRequest, result.StatusCode);
            Assert.Equal("Account Creation is currently disabled.", GetErrorMessage(result));
        }

        [Theory]
        [InlineData(true, TestDomainLoginProvider.ValidUsername, TestDomainLoginProvider.ValidPassword)]
        [InlineData(true, "test2.2@exceptionless.io", TestDomainLoginProvider.ValidPassword)]
        [InlineData(false, "test2@exceptionless.io", "Password1$")]
        public async Task CannotSignupWhenAccountCreationDisabledWithInvalidTokenAsync(bool enableAdAuth, string email, string password) {
            _authController.Request = CreateRequestMessage(SIGNUP_ENDPOINT, null, false, false);
            Settings.Current.EnableAccountCreation = false;
            Settings.Current.EnableActiveDirectoryAuth = enableAdAuth;

            if (enableAdAuth && email == TestDomainLoginProvider.ValidUsername) {
                TestDomainLoginProvider provider = new TestDomainLoginProvider();
                email = provider.GetEmailAddressFromUsername(email);
            }

            var signupModel = new SignupModel {
                Email = email,
                InviteToken = StringExtensions.GetNewToken(),
                Name = "Test",
                Password = password
            };

            var actionResult = await _authController.SignupAsync(signupModel);
            var result = await actionResult.ExecuteAsync(new CancellationToken());
            Assert.False(result.IsSuccessStatusCode, "Status Code is success.");
            Assert.Equal(System.Net.HttpStatusCode.BadRequest, result.StatusCode);
            Assert.Equal("Account Creation is currently disabled.", GetErrorMessage(result));
        }

        [Theory]
        [InlineData(true, TestDomainLoginProvider.ValidUsername, TestDomainLoginProvider.ValidPassword)]
        [InlineData(false, "test3@exceptionless.io", "Password1$")]
        public async Task CanSignupWhenAccountCreationDisabledWithValidTokenAsync(bool enableAdAuth, string email, string password) {
            _authController.Request = CreateRequestMessage(SIGNUP_ENDPOINT, null, false, false);
            Settings.Current.EnableAccountCreation = false;
            Settings.Current.EnableActiveDirectoryAuth = enableAdAuth;

            if (enableAdAuth && email == TestDomainLoginProvider.ValidUsername) {
                var provider = new TestDomainLoginProvider();
                email = provider.GetEmailAddressFromUsername(email);
            }

            var results = await _organizationRepository.GetAllAsync();
            var organization = results.Documents.First();

            var invite = new Invite {
                Token = StringExtensions.GetNewToken(),
                EmailAddress = email.ToLowerInvariant(),
                DateAdded = SystemClock.UtcNow
            };
            organization.Invites.Add(invite);

            organization = await _organizationRepository.SaveAsync(organization);
            await _configuration.Client.RefreshAsync(Indices.All);

            Assert.NotNull(organization.GetInvite(invite.Token));

            var signupModel = new SignupModel {
                Email = email,
                InviteToken = invite.Token,
                Name = "Test",
                Password = password
            };
            var actionResult = await _authController.SignupAsync(signupModel);
            var result = await actionResult.ExecuteAsync(new CancellationToken());
            Assert.True(result.IsSuccessStatusCode, "Status Code is failure.");
            Assert.Equal(System.Net.HttpStatusCode.OK, result.StatusCode);

            var tokenResult = GetResult<TokenResult>(result);
            Assert.NotNull(tokenResult);
            Assert.False(string.IsNullOrEmpty(tokenResult.Token));
        }

        [Fact]
        public async Task CanSignupWhenAccountCreationDisabledWithValidTokenAndInvalidAdAccountAsync() {
            _authController.Request = CreateRequestMessage(SIGNUP_ENDPOINT, null, false, false);
            Settings.Current.EnableAccountCreation = false;
            Settings.Current.EnableActiveDirectoryAuth = true;

            string email = "testuser1@exceptionless.io";
            string password = "invalidAccount1";

            var orgs = await _organizationRepository.GetAllAsync();
            var organization = orgs.Documents.First();
            var invite = new Invite {
                Token = StringExtensions.GetNewToken(),
                EmailAddress = email.ToLowerInvariant(),
                DateAdded = SystemClock.UtcNow
            };

            organization.Invites.Add(invite);
            await _organizationRepository.SaveAsync(organization);
            await _configuration.Client.RefreshAsync(Indices.All);

            Assert.NotNull(organization.GetInvite(invite.Token));

            var signupModel = new SignupModel {
                Email = email,
                InviteToken = invite.Token,
                Name = "Test",
                Password = password
            };

            var actionResult = await _authController.SignupAsync(signupModel);
            var result = await actionResult.ExecuteAsync(new CancellationToken());
            Assert.False(result.IsSuccessStatusCode, "Status Code is success.");
            Assert.Equal(System.Net.HttpStatusCode.BadRequest, result.StatusCode);
        }

        [Fact]
        public async Task CanSignupWhenAccountCreationEnabledWithNoTokenAsync() {
            _authController.Request = CreateRequestMessage(SIGNUP_ENDPOINT, null, false, false);
            Settings.Current.EnableAccountCreation = true;

            var signupModel = new SignupModel {
                Email = "test4@exceptionless.io",
                InviteToken = "",
                Name = "Test",
                Password = "Password1$"
            };

            var actionResult = await _authController.SignupAsync(signupModel);
            var result = await actionResult.ExecuteAsync(new CancellationToken());
            Assert.True(result.IsSuccessStatusCode, "Status Code is failure.");
            Assert.Equal(System.Net.HttpStatusCode.OK, result.StatusCode);

            var tokenResult = GetResult<TokenResult>(result);
            Assert.NotNull(tokenResult);
            Assert.False(string.IsNullOrEmpty(tokenResult.Token));
        }

        [Fact]
        public async Task CanSignupWhenAccountCreationEnabledWithNoTokenAndValidAdAccountAsync() {
            _authController.Request = CreateRequestMessage(SIGNUP_ENDPOINT, null, false, false);
            Settings.Current.EnableAccountCreation = true;
            Settings.Current.EnableActiveDirectoryAuth = true;

            TestDomainLoginProvider provider = new TestDomainLoginProvider();
            string email = provider.GetEmailAddressFromUsername(TestDomainLoginProvider.ValidUsername);

            var signupModel = new SignupModel {
                Email = email,
                InviteToken = "",
                Name = "Test",
                Password = TestDomainLoginProvider.ValidPassword
            };

            var actionResult = await _authController.SignupAsync(signupModel);
            var result = await actionResult.ExecuteAsync(new CancellationToken());
            Assert.True(result.IsSuccessStatusCode, "Status Code is failure.");
            Assert.Equal(System.Net.HttpStatusCode.OK, result.StatusCode);

            var tokenResult = GetResult<TokenResult>(result);
            Assert.NotNull(tokenResult);
            Assert.False(string.IsNullOrEmpty(tokenResult.Token));
        }

        [Fact]
        public async Task CanSignupWhenAccountCreationEnabledWithNoTokenAndInvalidAdAccountAsync() {
            _authController.Request = CreateRequestMessage(SIGNUP_ENDPOINT, null, false, false);
            Settings.Current.EnableAccountCreation = true;
            Settings.Current.EnableActiveDirectoryAuth = true;

            var signupModel = new SignupModel {
                Email = "testuser2@exceptionless.io",
                InviteToken = "",
                Name = "Test",
                Password = "literallydoesntmatter"
            };
            var actionResult = await _authController.SignupAsync(signupModel);
            var result = await actionResult.ExecuteAsync(new CancellationToken());
            Assert.False(result.IsSuccessStatusCode, "Status Code is success.");
            Assert.Equal(System.Net.HttpStatusCode.BadRequest, result.StatusCode);
        }

        [Fact]
        public async Task CanSignupWhenAccountCreationEnabledWithValidTokenAsync() {
            _authController.Request = CreateRequestMessage(SIGNUP_ENDPOINT, null, false, false);
            Settings.Current.EnableAccountCreation = true;

            var orgs = await _organizationRepository.GetAllAsync();
            var organization = orgs.Documents.First();
            var email = "test5@exceptionless.io";
            var invite = new Invite {
                Token = StringExtensions.GetNewToken(),
                EmailAddress = email.ToLowerInvariant(),
                DateAdded = SystemClock.UtcNow
            };
            organization.Invites.Add(invite);
            await _organizationRepository.SaveAsync(organization);
            await _configuration.Client.RefreshAsync(Indices.All);

            Assert.NotNull(organization.GetInvite(invite.Token));

            var signupModel = new SignupModel {
                Email = email,
                InviteToken = invite.Token,
                Name = "Test",
                Password = "Password1$"
            };
            var actionResult = await _authController.SignupAsync(signupModel);
            var result = await actionResult.ExecuteAsync(new CancellationToken());
            Assert.True(result.IsSuccessStatusCode, "Status Code is failure.");
            Assert.Equal(System.Net.HttpStatusCode.OK, result.StatusCode);

            var tokenResult = GetResult<TokenResult>(result);
            Assert.NotNull(tokenResult);
            Assert.False(string.IsNullOrEmpty(tokenResult.Token));
        }

        [Fact]
        public async Task CanSignupWhenAccountCreationEnabledWithValidTokenAndValidAdAccountAsync() {
            _authController.Request = CreateRequestMessage(SIGNUP_ENDPOINT, null, false, false);
            Settings.Current.EnableAccountCreation = true;
            Settings.Current.EnableActiveDirectoryAuth = true;

            TestDomainLoginProvider provider = new TestDomainLoginProvider();
            string email = provider.GetEmailAddressFromUsername(TestDomainLoginProvider.ValidUsername);

            var results = await _organizationRepository.GetAllAsync();
            var organization = results.Documents.First();
            var invite = new Invite {
                Token = StringExtensions.GetNewToken(),
                EmailAddress = email.ToLowerInvariant(),
                DateAdded = SystemClock.UtcNow
            };
            organization.Invites.Add(invite);
            await _organizationRepository.SaveAsync(organization);
            await _configuration.Client.RefreshAsync(Indices.All);

            Assert.NotNull(organization.GetInvite(invite.Token));

            var signupModel = new SignupModel {
                Email = email,
                InviteToken = invite.Token,
                Name = "Test",
                Password = TestDomainLoginProvider.ValidPassword
            };
            var actionResult = await _authController.SignupAsync(signupModel);
            var result = await actionResult.ExecuteAsync(new CancellationToken());
            Assert.True(result.IsSuccessStatusCode, "Status Code is failure.");
            Assert.Equal(System.Net.HttpStatusCode.OK, result.StatusCode);

            var tokenResult = GetResult<TokenResult>(result);
            Assert.NotNull(tokenResult);
            Assert.False(string.IsNullOrEmpty(tokenResult.Token));
        }

        [Fact]
        public async Task CanSignupWhenAccountCreationEnabledWithValidTokenAndInvalidAdAccountAsync() {
            _authController.Request = CreateRequestMessage(SIGNUP_ENDPOINT, null, false, false);
            Settings.Current.EnableAccountCreation = true;
            Settings.Current.EnableActiveDirectoryAuth = true;

            string email = "testuser4@exceptionless.io";

            var results = await _organizationRepository.GetAllAsync();
            var organization = results.Documents.First();
            var invite = new Invite {
                Token = StringExtensions.GetNewToken(),
                EmailAddress = email.ToLowerInvariant(),
                DateAdded = SystemClock.UtcNow
            };
            organization.Invites.Add(invite);
            await _organizationRepository.SaveAsync(organization);
            await _configuration.Client.RefreshAsync(Indices.All);

            Assert.NotNull(organization.GetInvite(invite.Token));

            var signupModel = new SignupModel {
                Email = email,
                InviteToken = invite.Token,
                Name = "Test",
                Password = TestDomainLoginProvider.ValidPassword
            };
            var actionResult = await _authController.SignupAsync(signupModel);
            var result = await actionResult.ExecuteAsync(new CancellationToken());
            Assert.False(result.IsSuccessStatusCode, "Status Code is success.");
            Assert.Equal(System.Net.HttpStatusCode.BadRequest, result.StatusCode);
        }

        [Fact]
        public async Task LoginValidAsync() {
            _authController.Request = CreateRequestMessage(LOGIN_ENDPOINT, null, false, false);
            Settings.Current.EnableActiveDirectoryAuth = false;

            // add user
            const string email = "test6@exceptionless.io";
            const string password = "Test6 password";
            const string salt = "1234567890123456";
            string passwordHash = password.ToSaltedHash(salt);
            var user = new User {
                EmailAddress = email,
                Password = passwordHash,
                Salt = salt,
                IsEmailAddressVerified = true,
                FullName = "User 6"
            };
            await _userRepository.AddAsync(user);

            await _configuration.Client.RefreshAsync(Indices.All);

            // create model
            var loginModel = new LoginModel {
                Email = email,
                Password = password
            };

            var actionResult = await _authController.LoginAsync(loginModel);
            var result = await actionResult.ExecuteAsync(new CancellationToken());
            Assert.True(result.IsSuccessStatusCode, "Status Code is failure.");
            Assert.Equal(System.Net.HttpStatusCode.OK, result.StatusCode);

            var tokenResult = GetResult<TokenResult>(result);
            Assert.NotNull(tokenResult);
            Assert.False(string.IsNullOrEmpty(tokenResult.Token));
        }

        [Fact]
        public async Task LoginInvalidPasswordAsync() {
            _authController.Request = CreateRequestMessage(LOGIN_ENDPOINT, null, false, false);
            Settings.Current.EnableActiveDirectoryAuth = false;

            // add user
            const string email = "test7@exceptionless.io";
            const string password = "Test7 password";
            const string salt = "1234567890123456";
            string passwordHash = password.ToSaltedHash(salt);

            var user = new User {
                EmailAddress = email,
                Password = passwordHash,
                Salt = salt,
                IsEmailAddressVerified = true,
                FullName = "User 7"
            };

            await _userRepository.AddAsync(user);
            await _configuration.Client.RefreshAsync(Indices.All);

            // create model
            var loginModel = new LoginModel {
                Email = email,
                Password = "This password ain't right"
            };

            var actionResult = await _authController.LoginAsync(loginModel);
            var result = await actionResult.ExecuteAsync(new CancellationToken());
            Assert.Equal(System.Net.HttpStatusCode.Unauthorized, result.StatusCode);
        }

        [Fact]
        public async Task LoginNoSuchUserAsync() {
            _authController.Request = CreateRequestMessage(LOGIN_ENDPOINT, null, false, false);
            Settings.Current.EnableActiveDirectoryAuth = false;

            // add user
            const string email = "test8@exceptionless.io";
            const string password = "Test8 password";
            const string salt = "1234567890123456";
            string passwordHash = password.ToSaltedHash(salt);
            var user = new User {
                EmailAddress = email,
                Password = passwordHash,
                Salt = salt,
                IsEmailAddressVerified = true,
                FullName = "User 8"
            };
            await _userRepository.AddAsync(user);

            await _configuration.Client.RefreshAsync(Indices.All);

            // create model
            var loginModel = new LoginModel {
                Email = "Thisguydoesntexist@exceptionless.io",
                Password = "This password ain't right"
            };

            var actionResult = await _authController.LoginAsync(loginModel);
            var result = await actionResult.ExecuteAsync(new CancellationToken());
            Assert.Equal(System.Net.HttpStatusCode.Unauthorized, result.StatusCode);
        }

        [Fact]
        public async Task LoginValidExistingActiveDirectoryAsync() {
            _authController.Request = CreateRequestMessage(LOGIN_ENDPOINT, null, false, false);
            Settings.Current.EnableActiveDirectoryAuth = true;

            // add user
            var provider = new TestDomainLoginProvider();
            string email = provider.GetEmailAddressFromUsername(TestDomainLoginProvider.ValidUsername);
            var user = new User {
                EmailAddress = email,
                IsEmailAddressVerified = true,
                FullName = "User 6"
            };

            await _userRepository.AddAsync(user);
            await _configuration.Client.RefreshAsync(Indices.All);

            // create model
            var loginModel = new LoginModel {
                Email = email,
                Password = TestDomainLoginProvider.ValidPassword
            };

            var actionResult = await _authController.LoginAsync(loginModel);
            var result = await actionResult.ExecuteAsync(new CancellationToken());
            Assert.True(result.IsSuccessStatusCode, "Status Code is failure.");
            Assert.Equal(System.Net.HttpStatusCode.OK, result.StatusCode);

            var tokenResult = GetResult<TokenResult>(result);
            Assert.NotNull(tokenResult);
            Assert.False(string.IsNullOrEmpty(tokenResult.Token));
        }

        [Fact]
        public async Task LoginValidNonExistantActiveDirectoryAsync() {
            _authController.Request = CreateRequestMessage(LOGIN_ENDPOINT, null, false, false);
            Settings.Current.EnableActiveDirectoryAuth = true;

            // create model
            var provider = new TestDomainLoginProvider();
            string email = provider.GetEmailAddressFromUsername(TestDomainLoginProvider.ValidUsername);
            var loginModel = new LoginModel {
                Email = email,
                Password = TestDomainLoginProvider.ValidPassword
            };

            var actionResult = await _authController.LoginAsync(loginModel);
            var result = await actionResult.ExecuteAsync(new CancellationToken());
            Assert.Equal(System.Net.HttpStatusCode.Unauthorized, result.StatusCode);
        }

        [Fact]
        public async Task LoginInvalidNonExistantActiveDirectoryAsync() {
            _authController.Request = CreateRequestMessage(LOGIN_ENDPOINT, null, false, false);
            Settings.Current.EnableActiveDirectoryAuth = true;

            // create model
            var loginModel = new LoginModel {
                Email = TestDomainLoginProvider.ValidUsername + ".au",
                Password = "Totallywrongpassword1234"
            };

            var actionResult = await _authController.LoginAsync(loginModel);
            var result = await actionResult.ExecuteAsync(new CancellationToken());
            Assert.Equal(System.Net.HttpStatusCode.Unauthorized, result.StatusCode);

            // Verify that a user account was not added
            var provider = new TestDomainLoginProvider();
            string email = provider.GetEmailAddressFromUsername(TestDomainLoginProvider.ValidUsername);
            var user = await _userRepository.GetByEmailAddressAsync(email + ".au");
            Assert.Null(user);
        }

        [Fact]
        public async Task LoginInvalidExistingActiveDirectoryAsync() {
            _authController.Request = CreateRequestMessage(LOGIN_ENDPOINT, null, false, false);
            Settings.Current.EnableActiveDirectoryAuth = true;

            // add user
            var provider = new TestDomainLoginProvider();
            string email = provider.GetEmailAddressFromUsername(TestDomainLoginProvider.ValidUsername);
            var user = new User {
                EmailAddress = email,
                IsEmailAddressVerified = true,
                FullName = "User 6"
            };
            await _userRepository.AddAsync(user);
            await _configuration.Client.RefreshAsync(Indices.All);

            // create model
            var loginModel = new LoginModel {
                Email = TestDomainLoginProvider.ValidUsername,
                Password = "Totallywrongpassword1234"
            };

            var actionResult = await _authController.LoginAsync(loginModel);
            var result = await actionResult.ExecuteAsync(new CancellationToken());
            Assert.Equal(System.Net.HttpStatusCode.Unauthorized, result.StatusCode);
        }

        private HttpRequestMessage CreateRequestMessage(string endpoint, ClaimsPrincipal user, bool isCompressed, bool isJson, string charset = "utf-8") {
            var request = new HttpRequestMessage();

            var context = new OwinContext();
            context.Request.User = Thread.CurrentPrincipal = user;
            request.SetOwinContext(context);
            request.SetConfiguration(new HttpConfiguration());
            request.Content = new HttpMessageContent(new HttpRequestMessage(HttpMethod.Post, $"/api/auth/{endpoint}"));
            if (isCompressed)
                request.Content.Headers.ContentEncoding.Add("gzip");
            request.Content.Headers.ContentType.MediaType = isJson ? "application/json" : "text/plain";
            request.Content.Headers.ContentType.CharSet = charset;

            return request;
        }

        private static string GetErrorMessage(HttpResponseMessage message) {
            var httpError = GetResult<HttpError>(message);
            Assert.NotNull(httpError);
            Debug.Assert(httpError != null, "httpError != null");
            return httpError["Message"] as string;
        }

        private static T GetResult<T>(HttpResponseMessage message) where T : class {
            var content = message.Content as ObjectContent<T>;
            var result = content?.Value as T;
            return result;
        }

        public async Task CreateOrganizationAndProjectsAsync() {
            await _organizationRepository.AddAsync(OrganizationData.GenerateSampleOrganizations());
            await _projectRepository.AddAsync(ProjectData.GenerateSampleProjects());
            await _configuration.Client.RefreshAsync(Indices.All);
        }
    }
}