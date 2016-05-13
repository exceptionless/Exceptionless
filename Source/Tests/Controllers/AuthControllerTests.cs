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
using Exceptionless.Api.Tests.Utility;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Tests.Utility;
using Microsoft.Owin;
using Nest;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Api.Tests.Controllers {
    public class AuthControllerTests {
        private static bool _databaseReset;
        private static bool _sampleOrganizationsAdded;
        private static bool _sampleProjectsAdded;

        private readonly ITestOutputHelper _output;
        private readonly IElasticClient _client = IoC.GetInstance<IElasticClient>();
        private readonly IUserRepository _userRepository = IoC.GetInstance<IUserRepository>();
        private readonly AuthController _authController = IoC.GetInstance<AuthController>();
        private readonly IOrganizationRepository _organizationRepository = IoC.GetInstance<IOrganizationRepository>();
        private readonly IProjectRepository _projectRepository = IoC.GetInstance<IProjectRepository>();

	    private const string SIGNUP_ENDPOINT = "signup";
	    private const string LOGIN_ENDPOINT = "login";

        public AuthControllerTests(ITestOutputHelper output) {
            _output = output;
        }

        [Fact]
        public async Task CannotSignupWhenAccountCreationDisabledWithNoTokenAsync() {
            await ResetAsync();

            try {
                _authController.Request = CreateRequestMessage(SIGNUP_ENDPOINT, null, false, false);
                Settings.Current.EnableAccountCreation = false;

                var signupModel = new SignupModel {
                    Email = "test1@exceptionless.io",
                    InviteToken = "",
                    Name = "Test",
                    Password = "Password1$"
                };
                var actionResult = await _authController.SignupAsync(signupModel);

                Assert.IsAssignableFrom<IHttpActionResult>(actionResult);
                var cancellation = new CancellationToken();
                var result = await actionResult.ExecuteAsync(cancellation);
                Assert.False(result.IsSuccessStatusCode, "Status Code is success.");
                Assert.Equal(System.Net.HttpStatusCode.BadRequest, result.StatusCode);
                var error = GetErrorMessage(result);
                Assert.Equal("Account Creation is currently disabled.", error);
            } finally {
                await ResetAsync();
            }
        }

        [Fact]
        public async Task CannotSignupWhenAccountCreationDisabledWithInvalidTokenAsync() {
            await ResetAsync();

            try {
                _authController.Request = CreateRequestMessage(SIGNUP_ENDPOINT, null, false, false);
                Settings.Current.EnableAccountCreation = false;

                var signupModel = new SignupModel {
                    Email = "test2@exceptionless.io",
                    InviteToken = StringExtensions.GetNewToken(),
                    Name = "Test",
                    Password = "Password1$"
                };
                var actionResult = await _authController.SignupAsync(signupModel);

                Assert.IsAssignableFrom<IHttpActionResult>(actionResult);
                var cancellation = new CancellationToken();
                var result = await actionResult.ExecuteAsync(cancellation);
                Assert.False(result.IsSuccessStatusCode, "Status Code is success.");
                Assert.Equal(System.Net.HttpStatusCode.BadRequest, result.StatusCode);
                var error = GetErrorMessage(result);
                Assert.Equal("Account Creation is currently disabled.", error);
            } finally {
                await ResetAsync();
            }
        }

        [Fact]
        public async Task CanSignupWhenAccountCreationDisabledWithValidTokenAsync() {
            await ResetAsync();

            try {
                _authController.Request = CreateRequestMessage(SIGNUP_ENDPOINT, null, false, false);
                Settings.Current.EnableAccountCreation = false;

                var orgs = await _organizationRepository.GetAllAsync();
                var organization = orgs.Documents.First();
                var email = "test3@exceptionless.io";
                var invite = new Invite {
                    Token = StringExtensions.GetNewToken(),
                    EmailAddress = email.ToLowerInvariant(),
                    DateAdded = DateTime.UtcNow
                };
                organization.Invites.Add(invite);
                await _organizationRepository.SaveAsync(organization);
                _client.Refresh(r => r.Force());

                Assert.NotNull(organization.GetInvite(invite.Token));

                var signupModel = new SignupModel {
                    Email = email,
                    InviteToken = invite.Token,
                    Name = "Test",
                    Password = "Password1$"
                };
                var actionResult = await _authController.SignupAsync(signupModel);
                var cancellation = new CancellationToken();
                var result = await actionResult.ExecuteAsync(cancellation);
                var error = GetResult<HttpError>(result);
                if (error != null) {
                    _output.WriteLine("Error: {0}", error.Message);
                }
                Assert.True(result.IsSuccessStatusCode, "Status Code is failure.");
                Assert.Equal(System.Net.HttpStatusCode.OK, result.StatusCode);
                var tokenResult = GetResult<TokenResult>(result);
                Assert.NotNull(tokenResult);
                Assert.False(string.IsNullOrEmpty(tokenResult.Token));
            } finally {
                await ResetAsync();
            }
        }

        [Fact]
        public async Task CanSignupWhenAccountCreationEnabledWithNoTokenAsync() {
            await ResetAsync();

            try {
                _authController.Request = CreateRequestMessage(SIGNUP_ENDPOINT, null, false, false);
                Settings.Current.EnableAccountCreation = true;

                var signupModel = new SignupModel {
                    Email = "test4@exceptionless.io",
                    InviteToken = "",
                    Name = "Test",
                    Password = "Password1$"
                };
                var actionResult = await _authController.SignupAsync(signupModel);
                var cancellation = new CancellationToken();
                var result = await actionResult.ExecuteAsync(cancellation);
                var error = GetResult<HttpError>(result);
                if (error != null) {
                    _output.WriteLine("Error: {0}", error.Message);
                }
                Assert.True(result.IsSuccessStatusCode, "Status Code is failure.");
                Assert.Equal(System.Net.HttpStatusCode.OK, result.StatusCode);
                var tokenResult = GetResult<TokenResult>(result);
                Assert.NotNull(tokenResult);
                Assert.False(string.IsNullOrEmpty(tokenResult.Token));
            } finally {
                await ResetAsync();
            }
        }

        [Fact]
        public async Task CanSignupWhenAccountCreationEnabledWithValidTokenAsync() {
            await ResetAsync();

            try {
                _authController.Request = CreateRequestMessage(SIGNUP_ENDPOINT, null, false, false);
                Settings.Current.EnableAccountCreation = true;

                var orgs = await _organizationRepository.GetAllAsync();
                var organization = orgs.Documents.First();
                var email = "test5@exceptionless.io";
                var invite = new Invite {
                    Token = StringExtensions.GetNewToken(),
                    EmailAddress = email.ToLowerInvariant(),
                    DateAdded = DateTime.UtcNow
                };
                organization.Invites.Add(invite);
                await _organizationRepository.SaveAsync(organization);
                await _client.RefreshAsync(r => r.Force());

                Assert.NotNull(organization.GetInvite(invite.Token));

                var signupModel = new SignupModel {
                    Email = email,
                    InviteToken = invite.Token,
                    Name = "Test",
                    Password = "Password1$"
                };
                var actionResult = await _authController.SignupAsync(signupModel);
                var cancellation = new CancellationToken();
                var result = await actionResult.ExecuteAsync(cancellation);
                var error = GetResult<HttpError>(result);
                if (error != null) {
                    _output.WriteLine("Error: {0}", error.Message);
                }
                Assert.True(result.IsSuccessStatusCode, "Status Code is failure.");
                Assert.Equal(System.Net.HttpStatusCode.OK, result.StatusCode);
                var tokenResult = GetResult<TokenResult>(result);
                Assert.NotNull(tokenResult);
                Assert.False(string.IsNullOrEmpty(tokenResult.Token));
            } finally {
                await ResetAsync();
            }
        }

	    [Fact]
	    public async Task LoginValidAsync() {
		    await ResetAsync();

		    try {
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

				await _client.RefreshAsync(r => r.Force());

				// create model
				var loginModel = new LoginModel {
				    Email = email,
				    Password = password
			    };

				var actionResult = await _authController.LoginAsync(loginModel);
				var cancellation = new CancellationToken();
				var result = await actionResult.ExecuteAsync(cancellation);
				var error = GetResult<HttpError>(result);
				if (error != null)
				{
					_output.WriteLine("Error: {0}", error.Message);
				}
				Assert.True(result.IsSuccessStatusCode, "Status Code is failure.");
				Assert.Equal(System.Net.HttpStatusCode.OK, result.StatusCode);
				var tokenResult = GetResult<TokenResult>(result);
				Assert.NotNull(tokenResult);
				Assert.False(string.IsNullOrEmpty(tokenResult.Token));
			} finally {
			    await ResetAsync();
		    }
	    }

		[Fact]
		public async Task LoginInvalidPasswordAsync()
		{
			await ResetAsync();

			try
			{
				_authController.Request = CreateRequestMessage(LOGIN_ENDPOINT, null, false, false);
				Settings.Current.EnableActiveDirectoryAuth = false;

				// add user
				const string email = "test7@exceptionless.io";
				const string password = "Test7 password";
				const string salt = "1234567890123456";
				string passwordHash = password.ToSaltedHash(salt);
				var user = new User
				{
					EmailAddress = email,
					Password = passwordHash,
					Salt = salt,
					IsEmailAddressVerified = true,
					FullName = "User 7"
				};
				await _userRepository.AddAsync(user);

				await _client.RefreshAsync(r => r.Force());

				// create model
				var loginModel = new LoginModel
				{
					Email = email,
					Password = "This password ain't right"
				};

				var actionResult = await _authController.LoginAsync(loginModel);
				var cancellation = new CancellationToken();
				var result = await actionResult.ExecuteAsync(cancellation);
				var error = GetResult<HttpError>(result);
				if (error != null)
				{
					_output.WriteLine("Error: {0}", error.Message);
				}
				Assert.Equal(System.Net.HttpStatusCode.Unauthorized, result.StatusCode);
			}
			finally
			{
				await ResetAsync();
			}
		}

		[Fact]
		public async Task LoginNoSuchUserAsync()
		{
			await ResetAsync();

			try
			{
				_authController.Request = CreateRequestMessage(LOGIN_ENDPOINT, null, false, false);
				Settings.Current.EnableActiveDirectoryAuth = false;

				// add user
				const string email = "test8@exceptionless.io";
				const string password = "Test8 password";
				const string salt = "1234567890123456";
				string passwordHash = password.ToSaltedHash(salt);
				var user = new User
				{
					EmailAddress = email,
					Password = passwordHash,
					Salt = salt,
					IsEmailAddressVerified = true,
					FullName = "User 8"
				};
				await _userRepository.AddAsync(user);

				await _client.RefreshAsync(r => r.Force());

				// create model
				var loginModel = new LoginModel
				{
					Email = "Thisguydoesntexist@exceptionless.io",
					Password = "This password ain't right"
				};

				var actionResult = await _authController.LoginAsync(loginModel);
				var cancellation = new CancellationToken();
				var result = await actionResult.ExecuteAsync(cancellation);
				var error = GetResult<HttpError>(result);
				if (error != null)
				{
					_output.WriteLine("Error: {0}", error.Message);
				}
				Assert.Equal(System.Net.HttpStatusCode.Unauthorized, result.StatusCode);
			}
			finally
			{
				await ResetAsync();
			}
		}

		[Fact]
		public async Task LoginValidExistingActiveDirectoryAsync()
		{
			await ResetAsync();

			try
			{
				_authController.Request = CreateRequestMessage(LOGIN_ENDPOINT, null, false, false);
				Settings.Current.EnableActiveDirectoryAuth = true;

				// add user
				var provider = new TestDomainLoginProvider();
				string email = provider.GetEmailForLogin(TestDomainLoginProvider.ValidUsername);
				var user = new User
				{
					EmailAddress = email,
					IsEmailAddressVerified = true,
					FullName = "User 6",
					IsFromActiveDirectory = true
				};
				await _userRepository.AddAsync(user);

				await _client.RefreshAsync(r => r.Force());

				// create model
				var loginModel = new LoginModel
				{
					Email = TestDomainLoginProvider.ValidUsername,
					Password = TestDomainLoginProvider.ValidPassword
				};

				var actionResult = await _authController.LoginAsync(loginModel);
				var cancellation = new CancellationToken();
				var result = await actionResult.ExecuteAsync(cancellation);
				var error = GetResult<HttpError>(result);
				if (error != null)
				{
					_output.WriteLine("Error: {0}", error.Message);
				}
				Assert.True(result.IsSuccessStatusCode, "Status Code is failure.");
				Assert.Equal(System.Net.HttpStatusCode.OK, result.StatusCode);
				var tokenResult = GetResult<TokenResult>(result);
				Assert.NotNull(tokenResult);
				Assert.False(string.IsNullOrEmpty(tokenResult.Token));
			}
			finally
			{
				await ResetAsync();
			}
		}

		[Fact]
		public async Task LoginValidNonExistantActiveDirectoryAsync()
		{
			await ResetAsync();

			try
			{
				_authController.Request = CreateRequestMessage(LOGIN_ENDPOINT, null, false, false);
				Settings.Current.EnableActiveDirectoryAuth = true;

				// create model
				var loginModel = new LoginModel
				{
					Email = TestDomainLoginProvider.ValidUsername,
					Password = TestDomainLoginProvider.ValidPassword
				};

				var actionResult = await _authController.LoginAsync(loginModel);
				var cancellation = new CancellationToken();
				var result = await actionResult.ExecuteAsync(cancellation);
				var error = GetResult<HttpError>(result);
				if (error != null)
				{
					_output.WriteLine("Error: {0}", error.Message);
				}
				Assert.True(result.IsSuccessStatusCode, "Status Code is failure.");
				Assert.Equal(System.Net.HttpStatusCode.OK, result.StatusCode);
				var tokenResult = GetResult<TokenResult>(result);
				Assert.NotNull(tokenResult);
				Assert.False(string.IsNullOrEmpty(tokenResult.Token));

				await _client.RefreshAsync(r => r.Force());

				// Verify that a user account was added
				var provider = new TestDomainLoginProvider();
				string email = provider.GetEmailForLogin(TestDomainLoginProvider.ValidUsername);
				var user = await _userRepository.GetByEmailAddressAsync(email);
				Assert.NotNull(user);
				Assert.Equal(provider.GetNameForLogin(TestDomainLoginProvider.ValidUsername), user.FullName);
				Assert.Equal(email, user.EmailAddress);
				Assert.True(user.IsFromActiveDirectory);
			}
			finally
			{
				await ResetAsync();
			}
		}

		[Fact]
		public async Task LoginInvalidNonExistantActiveDirectoryAsync()
		{
			await ResetAsync();

			try
			{
				_authController.Request = CreateRequestMessage(LOGIN_ENDPOINT, null, false, false);
				Settings.Current.EnableActiveDirectoryAuth = true;

				// create model
				var loginModel = new LoginModel
				{
					Email = TestDomainLoginProvider.ValidUsername + ".au",
					Password = "Totallywrongpassword1234"
				};

				var actionResult = await _authController.LoginAsync(loginModel);
				var cancellation = new CancellationToken();
				var result = await actionResult.ExecuteAsync(cancellation);
				var error = GetResult<HttpError>(result);
				if (error != null)
				{
					_output.WriteLine("Error: {0}", error.Message);
				}
				Assert.Equal(System.Net.HttpStatusCode.Unauthorized, result.StatusCode);

				await _client.RefreshAsync(r => r.Force());

				// Verify that a user account was not added
				var provider = new TestDomainLoginProvider();
				string email = provider.GetEmailForLogin(TestDomainLoginProvider.ValidUsername);
				var user = await _userRepository.GetByEmailAddressAsync(email + ".au");
				Assert.Null(user);
			}
			finally
			{
				await ResetAsync();
			}
		}

		[Fact]
		public async Task LoginInvalidExistingActiveDirectoryAsync()
		{
			await ResetAsync();

			try
			{
				_authController.Request = CreateRequestMessage(LOGIN_ENDPOINT, null, false, false);
				Settings.Current.EnableActiveDirectoryAuth = true;

				// add user
				var provider = new TestDomainLoginProvider();
				string email = provider.GetEmailForLogin(TestDomainLoginProvider.ValidUsername);
				var user = new User
				{
					EmailAddress = email,
					IsEmailAddressVerified = true,
					FullName = "User 6",
					IsFromActiveDirectory = true
				};
				await _userRepository.AddAsync(user);

				await _client.RefreshAsync(r => r.Force());

				// create model
				var loginModel = new LoginModel
				{
					Email = TestDomainLoginProvider.ValidUsername,
					Password = "Totallywrongpassword1234"
				};

				var actionResult = await _authController.LoginAsync(loginModel);
				var cancellation = new CancellationToken();
				var result = await actionResult.ExecuteAsync(cancellation);
				var error = GetResult<HttpError>(result);
				if (error != null)
				{
					_output.WriteLine("Error: {0}", error.Message);
				}
				Assert.Equal(System.Net.HttpStatusCode.Unauthorized, result.StatusCode);
			}
			finally
			{
				await ResetAsync();
			}
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
            if (content == null)
                return null;
            var result = content.Value as T;
            return result;
        }

        private bool _isReset;

        private async Task ResetAsync() {
            if (!_isReset) {
                _isReset = true;
                await ResetDatabaseAsync();
                await AddSamplesAsync();
            }
        }

        private async Task ResetDatabaseAsync(bool force = false) {
            if (_databaseReset && !force)
                return;

            await RemoveAllUsersAsync();
            await RemoveAllProjectsAsync();
            await RemoveAllOrganizationsAsync();

            _databaseReset = true;
        }

        public async Task RemoveAllUsersAsync() {
            await _userRepository.RemoveAllAsync();
            await _client.RefreshAsync(r => r.Force());
        }

        public async Task RemoveAllOrganizationsAsync() {
            await _organizationRepository.RemoveAllAsync();
            await _client.RefreshAsync(r => r.Force());
            _sampleOrganizationsAdded = false;
        }

        public async Task RemoveAllProjectsAsync() {
            await _projectRepository.RemoveAllAsync();
            await _client.RefreshAsync(r => r.Force());
            _sampleProjectsAdded = false;
        }

        public async Task AddSampleProjectsAsync() {
            if (_sampleProjectsAdded)
                return;

            await _projectRepository.AddAsync(ProjectData.GenerateSampleProjects());
            await _client.RefreshAsync(r => r.Force());
            _sampleProjectsAdded = true;
        }

        public async Task AddSampleOrganizationsAsync() {
            if (_sampleOrganizationsAdded)
                return;

            await _organizationRepository.AddAsync(OrganizationData.GenerateSampleOrganizations());
            await _client.RefreshAsync(r => r.Force());
            _sampleOrganizationsAdded = true;
        }

        public async Task AddSamplesAsync() {
            await AddSampleProjectsAsync();
            await AddSampleOrganizationsAsync();
        }
    }
}