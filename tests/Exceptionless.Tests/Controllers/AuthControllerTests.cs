using System;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Web.Models;
using Exceptionless.Tests.Authentication;
using Exceptionless.Tests.Extensions;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Configuration;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Tests.Utility;
using Foundatio.Queues;
using Foundatio.Utility;
using Foundatio.Repositories;
using Xunit;
using Xunit.Abstractions;
using User = Exceptionless.Core.Models.User;
using FluentRest;

namespace Exceptionless.Tests.Controllers {
    public class AuthControllerTests : IntegrationTestsBase {
        private readonly AuthOptions _authOptions;
        private readonly IUserRepository _userRepository;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly ITokenRepository _tokenRepository;

        public AuthControllerTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory) {
            _authOptions = GetService<AuthOptions>();
            _authOptions.EnableAccountCreation = true;
            _authOptions.EnableActiveDirectoryAuth = false;
            
            _organizationRepository = GetService<IOrganizationRepository>();
            _projectRepository = GetService<IProjectRepository>();
            _userRepository = GetService<IUserRepository>();
            _tokenRepository = GetService<ITokenRepository>();
        }

        protected override async Task ResetDataAsync() {
            await base.ResetDataAsync();
            await CreateTestOrganizationAndProjectsAsync();
        }

        [Fact]
        public async Task CannotSignupWithoutPassword() {
            await SendRequestAsync(r => r
                .Post()
                .AppendPath("auth/signup")
                .Content(new SignupModel {
                    Email = "test@domain.com",
                    Name = "hello"
                })
                .StatusCodeShouldBeBadRequest()
            );
        }

        [Theory]
        [InlineData(true, TestDomainLoginProvider.ValidUsername, TestDomainLoginProvider.ValidPassword)]
        [InlineData(true, "test1.2@exceptionless.io", TestDomainLoginProvider.ValidPassword)]
        [InlineData(false, "test1@exceptionless.io", "Password1$")]
        public async Task CannotSignupWhenAccountCreationDisabledWithNoTokenAsync(bool enableAdAuth, string email, string password) {
            _authOptions.EnableAccountCreation = false;
            _authOptions.EnableActiveDirectoryAuth = enableAdAuth;

            if (enableAdAuth && email == TestDomainLoginProvider.ValidUsername) {
                var provider = new TestDomainLoginProvider();
                email = provider.GetEmailAddressFromUsername(email);
            }

            await SendRequestAsync(r => r
                .Post()
                .AppendPath("auth/signup")
                .Content(new SignupModel {
                    Email = email,
                    InviteToken = "",
                    Name = "Test",
                    Password = password
                })
                .StatusCodeShouldBeBadRequest()
            );
        }

        [Theory]
        [InlineData(true, TestDomainLoginProvider.ValidUsername, TestDomainLoginProvider.ValidPassword)]
        [InlineData(true, "test2.2@exceptionless.io", TestDomainLoginProvider.ValidPassword)]
        [InlineData(false, "test2@exceptionless.io", "Password1$")]
        public async Task CannotSignupWhenAccountCreationDisabledWithInvalidTokenAsync(bool enableAdAuth, string email, string password) {
            _authOptions.EnableAccountCreation = false;
            _authOptions.EnableActiveDirectoryAuth = enableAdAuth;

            if (enableAdAuth && email == TestDomainLoginProvider.ValidUsername) {
                var provider = new TestDomainLoginProvider();
                email = provider.GetEmailAddressFromUsername(email);
            }

            await SendRequestAsync(r => r
                .Post()
                .AppendPath("auth/signup")
                .Content(new SignupModel {
                    Email = email,
                    InviteToken = StringExtensions.GetNewToken(),
                    Name = "Test",
                    Password = password
                })
                .StatusCodeShouldBeBadRequest()
            );
        }

        [Theory]
        [InlineData(true, TestDomainLoginProvider.ValidUsername, TestDomainLoginProvider.ValidPassword)]
        [InlineData(false, "test3@exceptionless.io", "Password1$")]
        public async Task CanSignupWhenAccountCreationDisabledWithValidTokenAsync(bool enableAdAuth, string email, string password) {
            _authOptions.EnableAccountCreation = false;
            _authOptions.EnableActiveDirectoryAuth = enableAdAuth;

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
            organization = await _organizationRepository.SaveAsync(organization, o => o.ImmediateConsistency());
            Assert.NotNull(organization.GetInvite(invite.Token));

            var result = await SendRequestAsAsync<TokenResult>(r => r
               .Post()
               .AppendPath("auth/signup")
               .Content(new SignupModel {
                   Email = email,
                   InviteToken = invite.Token,
                   Name = "Test",
                   Password = password
               })
               .StatusCodeShouldBeOk()
           );

            Assert.NotNull(result);
            Assert.False(String.IsNullOrEmpty(result.Token));
        }

        [Fact]
        public async Task CanSignupWhenAccountCreationDisabledWithValidTokenAndInvalidAdAccountAsync() {
            _authOptions.EnableAccountCreation = false;
            _authOptions.EnableActiveDirectoryAuth = true;

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
            await _organizationRepository.SaveAsync(organization, o => o.ImmediateConsistency());
            Assert.NotNull(organization.GetInvite(invite.Token));

            await SendRequestAsync(r => r
               .Post()
               .AppendPath("auth/signup")
               .Content(new SignupModel {
                   Email = email,
                   InviteToken = invite.Token,
                   Name = "Test",
                   Password = password
               })
               .StatusCodeShouldBeBadRequest()
            );
        }

        [Fact]
        public async Task CanSignupWhenAccountCreationEnabledWithNoTokenAsync() {
            _authOptions.EnableAccountCreation = true;

            var result = await SendRequestAsAsync<TokenResult>(r => r
               .Post()
               .AppendPath("auth/signup")
               .Content(new SignupModel {
                   Email = "test4@exceptionless.io",
                   InviteToken = "",
                   Name = "Test",
                   Password = "Password1$"
               })
               .StatusCodeShouldBeOk()
            );

            Assert.NotNull(result);
            Assert.False(String.IsNullOrEmpty(result.Token));
        }

        [Fact]
        public async Task CanSignupWhenAccountCreationEnabledWithNoTokenAndValidAdAccountAsync() {
            _authOptions.EnableAccountCreation = true;
            _authOptions.EnableActiveDirectoryAuth = true;

            var provider = new TestDomainLoginProvider();
            string email = provider.GetEmailAddressFromUsername(TestDomainLoginProvider.ValidUsername);

            var result = await SendRequestAsAsync<TokenResult>(r => r
               .Post()
               .AppendPath("auth/signup")
               .Content(new SignupModel {
                   Email = email,
                   InviteToken = "",
                   Name = "Test",
                   Password = TestDomainLoginProvider.ValidPassword
               })
               .StatusCodeShouldBeOk()
            );

            Assert.NotNull(result);
            Assert.False(String.IsNullOrEmpty(result.Token));
        }

        [Fact]
        public async Task CanSignupWhenAccountCreationEnabledWithNoTokenAndInvalidAdAccountAsync() {
            _authOptions.EnableAccountCreation = true;
            _authOptions.EnableActiveDirectoryAuth = true;

            await SendRequestAsync(r => r
               .Post()
               .AppendPath("auth/signup")
               .Content(new SignupModel {
                   Email = "testuser2@exceptionless.io",
                   InviteToken = "",
                   Name = "Test",
                   Password = "literallydoesntmatter"
               })
               .StatusCodeShouldBeBadRequest()
            );
        }

        [Fact]
        public async Task CanSignupWhenAccountCreationEnabledWithValidTokenAsync() {
            _authOptions.EnableAccountCreation = true;

            var orgs = await _organizationRepository.GetAllAsync();
            var organization = orgs.Documents.First();
            const string email = "test5@exceptionless.io";
            const string name = "Test";
            const string password = "Password1$";

            var invite = new Invite {
                Token = StringExtensions.GetNewToken(),
                EmailAddress = email.ToLowerInvariant(),
                DateAdded = SystemClock.UtcNow
            };

            organization.Invites.Clear();
            organization.Invites.Add(invite);
            await _organizationRepository.SaveAsync(organization, o => o.ImmediateConsistency());
            Assert.NotNull(organization.GetInvite(invite.Token));

            var result = await SendRequestAsAsync<TokenResult>(r => r
               .Post()
               .AppendPath("auth/signup")
               .Content(new SignupModel {
                   Email = email,
                   InviteToken = invite.Token,
                   Name = name,
                   Password = password
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
            Assert.True(user.IsEmailAddressVerified);
            Assert.Equal(password.ToSaltedHash(user.Salt), user.Password);
            Assert.Contains(organization.Id, user.OrganizationIds);

            organization = await _organizationRepository.GetByIdAsync(organization.Id);
            Assert.Empty(organization.Invites);

            var token = await _tokenRepository.GetByIdAsync(result.Token);
            Assert.NotNull(token);
            Assert.Equal(user.Id, token.UserId);
            Assert.Equal(TokenType.Access, token.Type);

            var mailQueue = GetService<IQueue<MailMessage>>() as InMemoryQueue<MailMessage>;
            Assert.Equal(0, (await mailQueue.GetQueueStatsAsync()).Enqueued);
        }

        [Fact]
        public async Task CanSignupWhenAccountCreationEnabledWithValidTokenAndValidAdAccountAsync() {
            _authOptions.EnableAccountCreation = true;
            _authOptions.EnableActiveDirectoryAuth = true;

            var provider = new TestDomainLoginProvider();
            string email = provider.GetEmailAddressFromUsername(TestDomainLoginProvider.ValidUsername);

            var results = await _organizationRepository.GetAllAsync();
            var organization = results.Documents.First();
            var invite = new Invite {
                Token = StringExtensions.GetNewToken(),
                EmailAddress = email.ToLowerInvariant(),
                DateAdded = SystemClock.UtcNow
            };
            organization.Invites.Add(invite);
            await _organizationRepository.SaveAsync(organization, o => o.ImmediateConsistency());
            Assert.NotNull(organization.GetInvite(invite.Token));

            var result = await SendRequestAsAsync<TokenResult>(r => r
               .Post()
               .AppendPath("auth/signup")
               .Content(new SignupModel {
                   Email = email,
                   InviteToken = invite.Token,
                   Name = "Test",
                   Password = TestDomainLoginProvider.ValidPassword
               })
               .StatusCodeShouldBeOk()
            );

            Assert.NotNull(result);
            Assert.False(String.IsNullOrEmpty(result.Token));
        }

        [Fact]
        public async Task CanSignupWhenAccountCreationEnabledWithValidTokenAndInvalidAdAccountAsync() {
            _authOptions.EnableAccountCreation = true;
            _authOptions.EnableActiveDirectoryAuth = true;

            string email = "testuser4@exceptionless.io";
            var results = await _organizationRepository.GetAllAsync();
            var organization = results.Documents.First();
            var invite = new Invite {
                Token = StringExtensions.GetNewToken(),
                EmailAddress = email.ToLowerInvariant(),
                DateAdded = SystemClock.UtcNow
            };
            organization.Invites.Add(invite);
            await _organizationRepository.SaveAsync(organization, o => o.ImmediateConsistency());
            Assert.NotNull(organization.GetInvite(invite.Token));

            await SendRequestAsync(r => r
               .Post()
               .AppendPath("auth/signup")
               .Content(new SignupModel {
                   Email = email,
                   InviteToken = invite.Token,
                   Name = "Test",
                   Password = TestDomainLoginProvider.ValidPassword
               })
               .StatusCodeShouldBeBadRequest()
            );
        }

        [Fact]
        public async Task SignupShouldFailWhenUsingExistingAccountWithNoPasswordOrInvalidPassword() {
            var userRepo = GetService<IUserRepository>();

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
            await _userRepository.AddAsync(user, o => o.ImmediateConsistency());

            await SendRequestAsync(r => r
                .Post()
                .AppendPath("auth/signup")
                .Content(new SignupModel {
                    Email = email,
                    Name = "Random Name"
                })
                .StatusCodeShouldBeBadRequest()
            );

            await SendRequestAsync(r => r
                .Post()
                .AppendPath("auth/signup")
                .Content(new SignupModel {
                    Email = email,
                    Name = "Random Name",
                    Password = "invalidPass",
                })
                .StatusCodeShouldBeUnauthorized()
            );
        }

        [Fact]
        public async Task LoginValidAsync() {
            _authOptions.EnableActiveDirectoryAuth = false;

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
            await _userRepository.AddAsync(user, o => o.ImmediateConsistency());

            var result = await SendRequestAsAsync<TokenResult>(r => r
               .Post()
               .AppendPath("auth/login")
               .Content(new LoginModel {
                   Email = email,
                   Password = password
               })
               .StatusCodeShouldBeOk()
            );

            Assert.NotNull(result);
            Assert.False(String.IsNullOrEmpty(result.Token));
        }

        [Fact]
        public async Task LoginInvalidPasswordAsync() {
            _authOptions.EnableActiveDirectoryAuth = false;

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

            await _userRepository.AddAsync(user, o => o.ImmediateConsistency());

            await SendRequestAsync(r => r
               .Post()
               .AppendPath("auth/login")
               .Content(new LoginModel {
                   Email = email,
                   Password = "This password ain't right"
               })
               .StatusCodeShouldBeUnauthorized()
            );
        }

        [Fact]
        public async Task LoginNoSuchUserAsync() {
            _authOptions.EnableActiveDirectoryAuth = false;

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
            await _userRepository.AddAsync(user, o => o.ImmediateConsistency());

            await SendRequestAsync(r => r
               .Post()
               .AppendPath("auth/login")
               .Content(new LoginModel {
                   Email = "Thisguydoesntexist@exceptionless.io",
                   Password = "This password ain't right"
               })
               .StatusCodeShouldBeUnauthorized()
            );
        }

        [Fact]
        public async Task LoginValidExistingActiveDirectoryAsync() {
            _authOptions.EnableActiveDirectoryAuth = true;

            var provider = new TestDomainLoginProvider();
            string email = provider.GetEmailAddressFromUsername(TestDomainLoginProvider.ValidUsername);
            var user = new User {
                EmailAddress = email,
                IsEmailAddressVerified = true,
                FullName = "User 6"
            };

            await _userRepository.AddAsync(user, o => o.ImmediateConsistency());

            var result = await SendRequestAsAsync<TokenResult>(r => r
               .Post()
               .AppendPath("auth/login")
               .Content(new LoginModel {
                   Email = email,
                   Password = TestDomainLoginProvider.ValidPassword
               })
               .StatusCodeShouldBeOk()
            );

            Assert.NotNull(result);
            Assert.False(String.IsNullOrEmpty(result.Token));
        }

        [Fact]
        public async Task LoginValidNonExistantActiveDirectoryAsync() {
            _authOptions.EnableActiveDirectoryAuth = true;

            var provider = new TestDomainLoginProvider();
            string email = provider.GetEmailAddressFromUsername(TestDomainLoginProvider.ValidUsername);

            await SendRequestAsync(r => r
               .Post()
               .AppendPath("auth/login")
               .Content(new LoginModel {
                   Email = email,
                   Password = TestDomainLoginProvider.ValidPassword
               })
               .StatusCodeShouldBeUnauthorized()
            );
        }

        [Fact]
        public async Task LoginInvalidNonExistantActiveDirectoryAsync() {
            _authOptions.EnableActiveDirectoryAuth = true;

            await SendRequestAsync(r => r
               .Post()
               .AppendPath("auth/login")
               .Content(new LoginModel {
                   Email = TestDomainLoginProvider.ValidUsername + ".au",
                   Password = "Totallywrongpassword1234"
               })
               .StatusCodeShouldBeUnauthorized()
            );

            // Verify that a user account was not added
            var provider = new TestDomainLoginProvider();
            string email = provider.GetEmailAddressFromUsername(TestDomainLoginProvider.ValidUsername);
            var user = await _userRepository.GetByEmailAddressAsync(email + ".au");
            Assert.Null(user);
        }

        [Fact]
        public async Task LoginInvalidExistingActiveDirectoryAsync() {
            _authOptions.EnableActiveDirectoryAuth = true;

            var provider = new TestDomainLoginProvider();
            string email = provider.GetEmailAddressFromUsername(TestDomainLoginProvider.ValidUsername);
            var user = new User {
                EmailAddress = email,
                IsEmailAddressVerified = true,
                FullName = "User 6"
            };
            await _userRepository.AddAsync(user, o => o.ImmediateConsistency());

            await SendRequestAsync(r => r
               .Post()
               .AppendPath("auth/login")
               .Content(new LoginModel {
                   Email = TestDomainLoginProvider.ValidUsername,
                   Password = "Totallywrongpassword1234"
               })
               .StatusCodeShouldBeUnauthorized()
            );
        }

        [Fact]
        public async Task CanChangePasswordAsync() {
            const string email = "test6@exceptionless.io";
            const string password = "Test6 password";
            const string salt = "1234567890123456";
            string passwordHash = password.ToSaltedHash(salt);

            var user = new User {
                EmailAddress = email,
                Password = passwordHash,
                Salt = salt,
                IsEmailAddressVerified = true,
                FullName = "User 6",
                Roles = AuthorizationRoles.AllScopes
            };

            await _userRepository.AddAsync(user, o => o.Cache().ImmediateConsistency());

            var result = await SendRequestAsAsync<TokenResult>(r => r
                .Post()
                .AppendPath("auth/login")
                .Content(new LoginModel {
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
                .Content(new ChangePasswordModel {
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
        public async Task ChangePasswordShouldFailWithCurrentPasswordAsync() {
            const string email = "test6@exceptionless.io";
            const string password = "Test6 password";
            const string salt = "1234567890123456";
            string passwordHash = password.ToSaltedHash(salt);

            var user = new User {
                EmailAddress = email,
                Password = passwordHash,
                Salt = salt,
                IsEmailAddressVerified = true,
                FullName = "User 6",
                Roles = AuthorizationRoles.AllScopes
            };

            await _userRepository.AddAsync(user, o => o.Cache().ImmediateConsistency());

            var result = await SendRequestAsAsync<TokenResult>(r => r
                .Post()
                .AppendPath("auth/login")
                .Content(new LoginModel {
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

            await SendRequestAsync(r => r
                .Post()
                .BasicAuthorization(email, password)
                .AppendPath("auth/change-password")
                .Content(new ChangePasswordModel {
                    CurrentPassword = password,
                    Password = password
                })
                .StatusCodeShouldBeBadRequest()
            );

            Assert.NotNull(await _tokenRepository.GetByIdAsync(result.Token));
        }

        [Fact]
        public async Task CanResetPasswordAsync() {
            const string email = "test6@exceptionless.io";
            const string password = "Test6 password";
            const string salt = "1234567890123456";
            string passwordHash = password.ToSaltedHash(salt);

            var user = new User {
                EmailAddress = email,
                Password = passwordHash,
                Salt = salt,
                IsEmailAddressVerified = true,
                FullName = "User 6",
                Roles = AuthorizationRoles.AllScopes
            };

            user.CreatePasswordResetToken();
            await _userRepository.AddAsync(user, o => o.Cache().ImmediateConsistency());

            var result = await SendRequestAsAsync<TokenResult>(r => r
                .Post()
                .AppendPath("auth/login")
                .Content(new LoginModel {
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
                .Content(new ResetPasswordModel {
                    PasswordResetToken = user.PasswordResetToken,
                    Password = newPassword
                })
                .StatusCodeShouldBeOk()
            );

            Assert.Null(await _tokenRepository.GetByIdAsync(result.Token));
        }

        [Fact]
        public async Task ResetPasswordShouldFailWithCurrentPasswordAsync() {
            const string email = "test6@exceptionless.io";
            const string password = "Test6 password";
            const string salt = "1234567890123456";
            string passwordHash = password.ToSaltedHash(salt);

            var user = new User {
                EmailAddress = email,
                Password = passwordHash,
                Salt = salt,
                IsEmailAddressVerified = true,
                FullName = "User 6",
                Roles = AuthorizationRoles.AllScopes
            };

            user.CreatePasswordResetToken();
            await _userRepository.AddAsync(user, o => o.Cache().ImmediateConsistency());

            var result = await SendRequestAsAsync<TokenResult>(r => r
                .Post()
                .AppendPath("auth/login")
                .Content(new LoginModel {
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

            await SendRequestAsync(r => r
                .Post()
                .BasicAuthorization(email, password)
                .AppendPath("auth/reset-password")
                .Content(new ResetPasswordModel {
                    PasswordResetToken = user.PasswordResetToken,
                    Password = password
                })
                .StatusCodeShouldBeBadRequest()
            );

            Assert.NotNull(await _tokenRepository.GetByIdAsync(result.Token));
        }

        private Task CreateTestOrganizationAndProjectsAsync() {
            return Task.WhenAll(
                _organizationRepository.AddAsync(OrganizationData.GenerateSampleOrganizations(GetService<BillingManager>(), GetService<BillingPlans>()), o => o.ImmediateConsistency()),
                _projectRepository.AddAsync(ProjectData.GenerateSampleProjects(), o => o.ImmediateConsistency())
            );
        }
    }
}