using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Description;
using Exceptionless.Api.Extensions;
using Exceptionless.Api.Models;
using Exceptionless.Api.Utility;
using Exceptionless.Core;
using Exceptionless.Core.Authentication;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.DateTimeExtensions;
using FluentValidation;
using Foundatio.Caching;
using Foundatio.Logging;
using Foundatio.Repositories;
using Foundatio.Utility;
using Newtonsoft.Json.Linq;
using OAuth2.Client;
using OAuth2.Client.Impl;
using OAuth2.Configuration;
using OAuth2.Infrastructure;
using OAuth2.Models;

namespace Exceptionless.Api.Controllers {
    [RoutePrefix(API_PREFIX + "/auth")]
    public class AuthController : ExceptionlessApiController {
        private readonly IDomainLoginProvider _domainLoginProvider;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IUserRepository _userRepository;
        private readonly ITokenRepository _tokenRepository;
        private readonly ICacheClient _cache;
        private readonly IMailer _mailer;
        private readonly ILogger _logger;

        private static bool _isFirstUserChecked;

        public AuthController(IOrganizationRepository organizationRepository, IUserRepository userRepository, ITokenRepository tokenRepository, ICacheClient cacheClient, IMailer mailer, ILogger<AuthController> logger, IDomainLoginProvider domainLoginProvider) {
            _domainLoginProvider = domainLoginProvider;
            _organizationRepository = organizationRepository;
            _userRepository = userRepository;
            _tokenRepository = tokenRepository;
            _cache = new ScopedCacheClient(cacheClient, "Auth");
            _mailer = mailer;
            _logger = logger;
        }

        /// <summary>
        /// Login
        /// </summary>
        /// <remarks>
        /// Log in with your email address and password to generate a token scoped with your users roles.
        ///
        /// <code>{ "email": "noreply@exceptionless.io", "password": "exceptionless" }</code>
        ///
        /// This token can then be used to access the api. You can use this token in the header (bearer authentication)
        /// or append it onto the query string: ?access_token=MY_TOKEN
        ///
        /// Please note that you can also use this token on the documentation site by placing it in the
        /// headers api_key input box.
        /// </remarks>
        /// <param name="model">The login model.</param>
        /// <response code="400">The login model is invalid.</response>
        /// <response code="401">Login failed.</response>
        [HttpPost]
        [Route("login")]
        [ResponseType(typeof(TokenResult))]
        public async Task<IHttpActionResult> LoginAsync(LoginModel model) {
            string email = model?.Email?.Trim().ToLowerInvariant();
            if (String.IsNullOrEmpty(email)) {
                _logger.Error().Message("Login failed: Email Address is required.").Tag("Login").SetActionContext(ActionContext).Write();
                return BadRequest("Email Address is required.");
            }

            if (String.IsNullOrWhiteSpace(model.Password)) {
                _logger.Error().Message("Login failed for \"{0}\": Password is required.", email).Tag("Login").Identity(email).SetActionContext(ActionContext).Write();
                return BadRequest("Password is required.");
            }

            // Only allow 5 password attempts per 15 minute period.
            string userLoginAttemptsCacheKey = $"user:{email}:attempts";
            long userLoginAttempts = await _cache.IncrementAsync(userLoginAttemptsCacheKey, 1, SystemClock.UtcNow.Ceiling(TimeSpan.FromMinutes(15)));

            // Only allow 15 login attempts per 15 minute period by a single ip.
            string ipLoginAttemptsCacheKey = $"ip:{Request.GetClientIpAddress()}:attempts";
            long ipLoginAttempts = await _cache.IncrementAsync(ipLoginAttemptsCacheKey, 1, SystemClock.UtcNow.Ceiling(TimeSpan.FromMinutes(15)));

            if (userLoginAttempts > 5) {
                _logger.Error().Message("Login denied for \"{0}\" for the {1} time.", email, userLoginAttempts).Tag("Login").Identity(email).SetActionContext(ActionContext).Write();
                return Unauthorized();
            }

            if (ipLoginAttempts > 15) {
                _logger.Error().Message("Login denied for \"{0}\" for the {1} time.", Request.GetClientIpAddress(), ipLoginAttempts).Tag("Login").Identity(email).SetActionContext(ActionContext).Write();
                return Unauthorized();
            }

            User user;
            try {
                user = await _userRepository.GetByEmailAddressAsync(email);
            } catch (Exception ex) {
                _logger.Error().Exception(ex).Critical().Message("Login failed for \"{0}\": {1}", email, ex.Message).Tag("Login").Identity(email).SetActionContext(ActionContext).Write();
                return Unauthorized();
            }

            if (user == null) {
                _logger.Error().Message("Login failed for \"{0}\": User not found.", email).Tag("Login").Identity(email).SetActionContext(ActionContext).Write();
                return Unauthorized();
            }

            if (!user.IsActive) {
                _logger.Error().Message("Login failed for \"{0}\": The user is inactive.", user.EmailAddress).Tag("Login").Identity(user.EmailAddress).Property("User", user).SetActionContext(ActionContext).Write();
                return Unauthorized();
            }

            if (!Settings.Current.EnableActiveDirectoryAuth) {
                if (String.IsNullOrEmpty(user.Salt)) {
                    _logger.Error().Message("Login failed for \"{0}\": The user has no salt defined.", user.EmailAddress).Tag("Login").Identity(user.EmailAddress).Property("User", user).SetActionContext(ActionContext).Write();
                    return Unauthorized();
                }

                string encodedPassword = model.Password.ToSaltedHash(user.Salt);
                if (!String.Equals(encodedPassword, user.Password)) {
                    _logger.Error().Message("Login failed for \"{0}\": Invalid Password.", user.EmailAddress).Tag("Login").Identity(user.EmailAddress).Property("User", user).SetActionContext(ActionContext).Write();
                    return Unauthorized();
                }
            } else {
                if (!IsValidActiveDirectoryLogin(email, model.Password)) {
                    _logger.Error().Message("Domain login failed for \"{0}\": Invalid Password or Account.", user.EmailAddress).Tag("Login").Identity(user.EmailAddress).Property("User", user).SetActionContext(ActionContext).Write();
                    return Unauthorized();
                }
            }

            if (!String.IsNullOrEmpty(model.InviteToken))
                await AddInvitedUserToOrganizationAsync(model.InviteToken, user);

            await _cache.RemoveAsync(userLoginAttemptsCacheKey);
            await _cache.IncrementAsync(ipLoginAttemptsCacheKey, -1, SystemClock.UtcNow.Ceiling(TimeSpan.FromMinutes(15)));

            _logger.Info().Message("\"{0}\" logged in.", user.EmailAddress).Tag("Login").Identity(user.EmailAddress).Property("User", user).SetActionContext(ActionContext).Write();
            return Ok(new TokenResult { Token = await GetTokenAsync(user) });
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet]
        [Route("logout")]
        [Authorize(Roles = AuthorizationRoles.User)]
        public async Task<IHttpActionResult> LogoutAsync() {
            if (User.IsTokenAuthType())
                return Ok();

            var id = User.GetLoggedInUsersTokenId();
            if (String.IsNullOrEmpty(id))
                return Ok();

            try {
                await _tokenRepository.RemoveAsync(id);
            } catch (Exception ex) {
                _logger.Error().Exception(ex).Critical().Message("Logout failed for \"{0}\": {1}", CurrentUser.EmailAddress, ex.Message).Tag("Logout").Identity(CurrentUser.EmailAddress).SetActionContext(ActionContext).Write();
            }

            return Ok();
        }

        /// <summary>
        /// Signup
        /// </summary>
        /// <param name="model">The signup model.</param>
        /// <response code="400">The signup model is invalid.</response>
        /// <response code="401">Signup failed.</response>
        [HttpPost]
        [Route("signup")]
        [ResponseType(typeof(TokenResult))]
        public async Task<IHttpActionResult> SignupAsync(SignupModel model) {
            bool valid = await IsAccountCreationEnabledAsync(model?.InviteToken);
            if (!valid)
                return BadRequest("Account Creation is currently disabled.");

            string email = model?.Email?.Trim().ToLowerInvariant();
            if (String.IsNullOrEmpty(email)) {
                _logger.Error().Message("Signup failed: Email Address is required.").Tag("Signup").Property("Name", model != null ? model.Name : "<null>").SetActionContext(ActionContext).Write();
                return BadRequest("Email Address is required.");
            }

            if (String.IsNullOrWhiteSpace(model.Name)) {
                _logger.Error().Message("Signup failed for \"{0}\": Name is required.", email).Tag("Signup").Identity(email).SetActionContext(ActionContext).Write();
                return BadRequest("Name is required.");
            }

            if (!IsValidPassword(model.Password)) {
                _logger.Error().Message("Signup failed for \"{0}\": Invalid Password", email).Tag("Signup").Identity(email).Property("Password Length", model.Password?.Length ?? 0).SetActionContext(ActionContext).Write();
                return BadRequest("Password must be at least 6 characters long.");
            }

            User user;
            try {
                user = await _userRepository.GetByEmailAddressAsync(email);
            } catch (Exception ex) {
                _logger.Error().Exception(ex).Critical().Message("Signup failed for \"{0}\": {1}", email, ex.Message).Tag("Signup").Identity(email).SetActionContext(ActionContext).Write();
                return BadRequest();
            }

            if (user != null)
                return await LoginAsync(model);

            string ipSignupAttemptsCacheKey = $"ip:{Request.GetClientIpAddress()}:signup:attempts";
            bool hasValidInviteToken = !String.IsNullOrWhiteSpace(model.InviteToken) && await _organizationRepository.GetByInviteTokenAsync(model.InviteToken) != null;
            if (!hasValidInviteToken) {
                // Only allow 10 signups per hour period by a single ip.
                long ipSignupAttempts = await _cache.IncrementAsync(ipSignupAttemptsCacheKey, 1, SystemClock.UtcNow.Ceiling(TimeSpan.FromHours(1)));
                if (ipSignupAttempts > 10) {
                    _logger.Error().Message("Signup denied for \"{0}\" for the {1} time.", email, ipSignupAttempts).Tag("Signup").Identity(email).SetActionContext(ActionContext).Write();
                    return BadRequest();
                }
            }

            if (Settings.Current.EnableActiveDirectoryAuth && !IsValidActiveDirectoryLogin(email, model.Password)) {
                _logger.Error().Message("Signup failed for \"{0}\": Active Directory authentication failed.", email).Tag("Signup").Identity(email).SetActionContext(ActionContext).Write();
                return BadRequest();
            }

            user = new User {
                IsActive = true,
                FullName = model.Name.Trim(),
                EmailAddress = email,
                IsEmailAddressVerified = Settings.Current.EnableActiveDirectoryAuth
            };
            user.CreateVerifyEmailAddressToken();
            user.Roles.Add(AuthorizationRoles.Client);
            user.Roles.Add(AuthorizationRoles.User);
            await AddGlobalAdminRoleIfFirstUserAsync(user);

            if (!Settings.Current.EnableActiveDirectoryAuth) {
                user.Salt = Core.Extensions.StringExtensions.GetRandomString(16);
                user.Password = model.Password.ToSaltedHash(user.Salt);
            }

            try {
                user = await _userRepository.AddAsync(user, o => o.Cache());
            } catch (ValidationException ex) {
                string errors = String.Join(", ", ex.Errors);
                _logger.Error().Critical().Message("Signup failed for \"{0}\": {1}", email, errors).Tag("Signup").Identity(user.EmailAddress).Property("User", user).SetActionContext(ActionContext).Write();
                return BadRequest(errors);
            } catch (Exception ex) {
                _logger.Error().Exception(ex).Critical().Message("Signup failed for \"{0}\": {1}", email, ex.Message).Tag("Signup").Identity(user.EmailAddress).Property("User", user).SetActionContext(ActionContext).Write();
                return BadRequest("An error occurred.");
            }

            if (hasValidInviteToken)
                await AddInvitedUserToOrganizationAsync(model.InviteToken, user);

            if (!user.IsEmailAddressVerified)
                await _mailer.SendUserEmailVerifyAsync(user);

            _logger.Info().Message("\"{0}\" signed up.", user.EmailAddress).Tag("Signup").Identity(user.EmailAddress).Property("User", user).SetActionContext(ActionContext).Write();
            return Ok(new TokenResult { Token = await GetTokenAsync(user) });
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost]
        [Route("github")]
        [ResponseType(typeof(TokenResult))]
        public Task<IHttpActionResult> GitHubAsync(JObject value) {
            return ExternalLoginAsync(value.ToObject<ExternalAuthInfo>(), Settings.Current.GitHubAppId, Settings.Current.GitHubAppSecret, (f, c) => new GitHubClient(f, c));
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost]
        [Route("google")]
        [ResponseType(typeof(TokenResult))]
        public Task<IHttpActionResult> GoogleAsync(JObject value) {
            return ExternalLoginAsync(value.ToObject<ExternalAuthInfo>(), Settings.Current.GoogleAppId, Settings.Current.GoogleAppSecret, (f, c) => new GoogleClient(f, c));
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost]
        [Route("facebook")]
        [ResponseType(typeof(TokenResult))]
        public Task<IHttpActionResult> FacebookAsync(JObject value) {
            return ExternalLoginAsync(value.ToObject<ExternalAuthInfo>(), Settings.Current.FacebookAppId, Settings.Current.FacebookAppSecret, (f, c) => new FacebookClient(f, c));
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost]
        [Route("live")]
        [ResponseType(typeof(TokenResult))]
        public Task<IHttpActionResult> LiveAsync(JObject value) {
            return ExternalLoginAsync(value.ToObject<ExternalAuthInfo>(), Settings.Current.MicrosoftAppId, Settings.Current.MicrosoftAppSecret, (f, c) => new WindowsLiveClient(f, c));
        }

        /// <summary>
        /// Removes an external login provider from the account
        /// </summary>
        /// <param name="providerName">The provider name.</param>
        /// <response code="400">Invalid provider name.</response>
        /// <response code="500">An error while saving the user account.</response>
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost]
        [Route("unlink/{providerName:minlength(1)}")]
        [Authorize(Roles = AuthorizationRoles.User)]
        [ResponseType(typeof(TokenResult))]
        public async Task<IHttpActionResult> RemoveExternalLoginAsync(string providerName, [NakedBody] string providerUserId) {
            if (String.IsNullOrWhiteSpace(providerName) || String.IsNullOrWhiteSpace(providerUserId)) {
                _logger.Error().Message("Remove external login failed for \"{0}\": Invalid Provider Name or Provider User Id.", CurrentUser.EmailAddress).Tag("External Login", providerName).Identity(CurrentUser.EmailAddress).Property("User", CurrentUser).Property("Provider User Id", providerUserId).SetActionContext(ActionContext).Write();
                return BadRequest("Invalid Provider Name or Provider User Id.");
            }

            if (CurrentUser.OAuthAccounts.Count <= 1 && String.IsNullOrEmpty(CurrentUser.Password)) {
                _logger.Error().Message("Remove external login failed for \"{0}\": You must set a local password before removing your external login.", CurrentUser.EmailAddress).Tag("External Login", providerName).Identity(CurrentUser.EmailAddress).Property("User", CurrentUser).Property("Provider User Id", providerUserId).SetActionContext(ActionContext).Write();
                return BadRequest("You must set a local password before removing your external login.");
            }

            try {
                if (CurrentUser.RemoveOAuthAccount(providerName, providerUserId))
                    await _userRepository.SaveAsync(CurrentUser, o => o.Cache());
            } catch (Exception ex) {
                _logger.Error().Exception(ex).Critical().Message("Error removing external login for \"{0}\": {1}", CurrentUser.EmailAddress, ex.Message).Tag("RemoveExternalLoginAsync").Identity(CurrentUser.EmailAddress).SetActionContext(ActionContext).Write();
                throw;
            }

            await ResetUserTokensAsync(CurrentUser, nameof(RemoveExternalLoginAsync));

            _logger.Info().Message("\"{0}\" removed an external login: \"{1}\"", CurrentUser.EmailAddress, providerName).Tag("External Login", providerName).Identity(CurrentUser.EmailAddress).Property("User", CurrentUser).SetActionContext(ActionContext).Write();
            return Ok(new TokenResult { Token = await GetTokenAsync(CurrentUser) });
        }

        /// <summary>
        /// Change password
        /// </summary>
        /// <param name="model">The change password model.</param>
        /// <response code="400">Invalid change password model.</response>
        [HttpPost]
        [Route("change-password")]
        [Authorize(Roles = AuthorizationRoles.User)]
        [ResponseType(typeof(TokenResult))]
        public async Task<IHttpActionResult> ChangePasswordAsync(ChangePasswordModel model) {
            if (model == null || !IsValidPassword(model.Password)) {
                _logger.Error().Message("Change password failed for \"{0}\": The New Password must be at least 6 characters long.", CurrentUser.EmailAddress).Tag("Change Password").Identity(CurrentUser.EmailAddress).Property("User", CurrentUser).Property("Password Length", model?.Password?.Length ?? 0).SetActionContext(ActionContext).Write();
                return BadRequest("The New Password must be at least 6 characters long.");
            }

            // User has a local account..
            if (!String.IsNullOrWhiteSpace(CurrentUser.Password)) {
                if (String.IsNullOrWhiteSpace(model.CurrentPassword)) {
                    _logger.Error().Message("Change password failed for \"{0}\": The current password is incorrect.", CurrentUser.EmailAddress).Tag("Change Password").Identity(CurrentUser.EmailAddress).Property("User", CurrentUser).SetActionContext(ActionContext).Write();
                    return BadRequest("The current password is incorrect.");
                }

                string encodedPassword = model.CurrentPassword.ToSaltedHash(CurrentUser.Salt);
                if (!String.Equals(encodedPassword, CurrentUser.Password)) {
                    _logger.Error().Message("Change password failed for \"{0}\": The current password is incorrect.", CurrentUser.EmailAddress).Tag("Change Password").Identity(CurrentUser.EmailAddress).Property("User", CurrentUser).SetActionContext(ActionContext).Write();
                    return BadRequest("The current password is incorrect.");
                }
            }

            await ChangePasswordAsync(CurrentUser, model.Password, nameof(ChangePasswordAsync));
            await ResetUserTokensAsync(CurrentUser, nameof(ChangePasswordAsync));

            _logger.Info().Message("\"{0}\" changed their password.", CurrentUser.EmailAddress).Identity(CurrentUser.EmailAddress).Property("User", CurrentUser).SetActionContext(ActionContext).Write();
            return Ok(new TokenResult { Token = await GetTokenAsync(CurrentUser) });
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet]
        [Route("check-email-address/{email:minlength(1)}")]
        public async Task<IHttpActionResult> IsEmailAddressAvailableAsync(string email) {
            if (String.IsNullOrWhiteSpace(email))
                return StatusCode(HttpStatusCode.NoContent);

            email = email.Trim().ToLowerInvariant();
            if (CurrentUser != null && String.Equals(CurrentUser.EmailAddress, email, StringComparison.InvariantCultureIgnoreCase))
                return StatusCode(HttpStatusCode.Created);

            // Only allow 3 checks attempts per hour period by a single ip.
            string ipEmailAddressAttemptsCacheKey = $"ip:{Request.GetClientIpAddress()}:email:attempts";
            long attempts = await _cache.IncrementAsync(ipEmailAddressAttemptsCacheKey, 1, SystemClock.UtcNow.Ceiling(TimeSpan.FromHours(1)));

            if (attempts > 3 || await _userRepository.GetByEmailAddressAsync(email) == null)
                return StatusCode(HttpStatusCode.NoContent);

            return StatusCode(HttpStatusCode.Created);
        }

        /// <summary>
        /// Forgot password
        /// </summary>
        /// <param name="email">The email address.</param>
        /// <response code="400">Invalid email address.</response>
        [HttpGet]
        [Route("forgot-password/{email:minlength(1)}")]
        public async Task<IHttpActionResult> ForgotPasswordAsync(string email) {
            if (String.IsNullOrWhiteSpace(email)) {
                _logger.Error().Message("Forgot password failed: Please specify a valid Email Address.").Tag("Forgot Password").SetActionContext(ActionContext).Write();
                return BadRequest("Please specify a valid Email Address.");
            }

            email = email.Trim().ToLowerInvariant();
            var user = await _userRepository.GetByEmailAddressAsync(email);
            if (user == null) {
                _logger.Error().Message("Forgot password failed for \"{0}\": No user was found.", email).Tag("Forgot Password").Identity(email).Property("Email Address", email).SetActionContext(ActionContext).Write();
                return Ok();
            }

            user.CreatePasswordResetToken();
            await _userRepository.SaveAsync(user, o => o.Cache());

            await _mailer.SendUserPasswordResetAsync(user);
            _logger.Info().Message("\"{0}\" forgot their password.", user.EmailAddress).Identity(user.EmailAddress).Property("User", user).SetActionContext(ActionContext).Write();
            return Ok();
        }

        /// <summary>
        /// Reset password
        /// </summary>
        /// <param name="model">The reset password model.</param>
        /// <response code="400">Invalid reset password model.</response>
        [HttpPost]
        [Route("reset-password")]
        public async Task<IHttpActionResult> ResetPasswordAsync(ResetPasswordModel model) {
            if (String.IsNullOrEmpty(model?.PasswordResetToken)) {
                _logger.Error().Message("Reset password failed: Invalid Password Reset Token.").Tag("Reset Password").SetActionContext(ActionContext).Write();
                return BadRequest("Invalid Password Reset Token.");
            }

            var user = await _userRepository.GetByPasswordResetTokenAsync(model.PasswordResetToken);
            if (user == null) {
                _logger.Error().Message("Reset password failed: Invalid Password Reset Token.").Tag("Reset Password").SetActionContext(ActionContext).Write();
                return BadRequest("Invalid Password Reset Token.");
            }

            if (!user.HasValidPasswordResetTokenExpiration()) {
                _logger.Error().Message("Reset password failed for \"{0}\": Password Reset Token has expired.", user.EmailAddress).Tag("Reset Password").Identity(user.EmailAddress).Property("User", user).SetActionContext(ActionContext).Write();
                return BadRequest("Password Reset Token has expired.");
            }

            if (!IsValidPassword(model.Password)) {
                _logger.Error().Message("Reset password failed for \"{0}\": The New Password must be at least 6 characters long.", user.EmailAddress).Tag("Reset Password").Identity(user.EmailAddress).Property("User", user).Property("Password Length", model.Password?.Length ?? 0).SetActionContext(ActionContext).Write();
                return BadRequest("The New Password must be at least 6 characters long.");
            }

            user.MarkEmailAddressVerified();
            await ChangePasswordAsync(user, model.Password, nameof(ResetPasswordAsync));
            await ResetUserTokensAsync(user, nameof(ResetPasswordAsync));

            _logger.Info().Message("\"{0}\" reset their password.", user.EmailAddress).Identity(user.EmailAddress).Property("User", user).SetActionContext(ActionContext).Write();
            return Ok();
        }

        /// <summary>
        /// Cancel reset password
        /// </summary>
        /// <param name="token">The password reset token.</param>
        /// <response code="400">Invalid password reset token.</response>
        [HttpPost]
        [Route("cancel-reset-password/{token:minlength(1)}")]
        public async Task<IHttpActionResult> CancelResetPasswordAsync(string token) {
            if (String.IsNullOrEmpty(token)) {
                _logger.Error().Message("Cancel reset password failed: Invalid Password Reset Token.").Tag("Reset Password").SetActionContext(ActionContext).Write();
                return BadRequest("Invalid password reset token.");
            }

            var user = await _userRepository.GetByPasswordResetTokenAsync(token);
            if (user == null)
                return Ok();

            user.ResetPasswordResetToken();
            await _userRepository.SaveAsync(user, o => o.Cache());

            _logger.Info().Message("\"{0}\" canceled the reset password", user.EmailAddress).Identity(user.EmailAddress).Property("User", user).SetActionContext(ActionContext).Write();
            return Ok();
        }

        private async Task AddGlobalAdminRoleIfFirstUserAsync(User user) {
            if (_isFirstUserChecked)
                return;

            bool isFirstUser =  await _userRepository.CountAsync() == 0;
            if (isFirstUser)
                user.Roles.Add(AuthorizationRoles.GlobalAdmin);

            _isFirstUserChecked = true;
        }

        private async Task<IHttpActionResult> ExternalLoginAsync<TClient>(ExternalAuthInfo authInfo, string appId, string appSecret, Func<IRequestFactory, IClientConfiguration, TClient> createClient) where TClient : OAuth2Client {
            if (String.IsNullOrEmpty(authInfo?.Code)) {
                _logger.Error().Message("External login failed: Unable to get auth info.").Tag("External Login").Property("Auth Info", authInfo).SetActionContext(ActionContext).Write();
                return NotFound();
            }

            if (String.IsNullOrEmpty(appId) || String.IsNullOrEmpty(appSecret))
                return NotFound();

            var client = createClient(new RequestFactory(), new RuntimeClientConfiguration {
                ClientId = appId,
                ClientSecret = appSecret,
                RedirectUri = authInfo.RedirectUri
            });

            UserInfo userInfo;
            try {
                userInfo = client.GetUserInfo(authInfo.Code);
            } catch (Exception ex) {
                _logger.Error().Exception(ex).Critical().Message("External login failed: {0}", ex.Message).Tag("External Login", client.Name).Property("Auth Info", authInfo).SetActionContext(ActionContext).Write();
                return BadRequest("Unable to get user info.");
            }

            User user;
            try {
                user = await FromExternalLoginAsync(userInfo);
            } catch (ApplicationException ex) {
                _logger.Error().Exception(ex).Critical().Message("External login failed for \"{0}\": {1}", userInfo.Email, ex.Message).Tag("External Login", client.Name).Property("User Info", userInfo).Property("Auth Info", authInfo).SetActionContext(ActionContext).Write();
                return BadRequest("Account Creation is currently disabled.");
            } catch (Exception ex) {
                _logger.Error().Exception(ex).Critical().Message("External login failed for \"{0}\": {1}", userInfo.Email, ex.Message).Tag("External Login", client.Name).Property("User Info", userInfo).Property("Auth Info", authInfo).SetActionContext(ActionContext).Write();
                return BadRequest("An error occurred while processing user info.");
            }

            if (user == null) {
                _logger.Error().Critical().Message("External login failed for \"{0}\": Unable to process user info.", userInfo.Email).Tag("External Login", client.Name).Property("User Info", userInfo).Property("Auth Info", authInfo).SetActionContext(ActionContext).Write();
                return BadRequest("Unable to process user info.");
            }

            if (!String.IsNullOrWhiteSpace(authInfo.InviteToken))
                await AddInvitedUserToOrganizationAsync(authInfo.InviteToken, user);

            _logger.Info().Message("\"{0}\" logged in.", user.EmailAddress).Tag("External Login", client.Name).Identity(user.EmailAddress).Property("User", user).SetActionContext(ActionContext).Write();
            return Ok(new TokenResult { Token = await GetTokenAsync(user) });
        }

        private async Task<User> FromExternalLoginAsync(UserInfo userInfo) {
            var existingUser = await _userRepository.GetUserByOAuthProviderAsync(userInfo.ProviderName, userInfo.Id);

            // Link user accounts.
            if (CurrentUser != null) {
                if (existingUser != null) {
                    if (existingUser.Id != CurrentUser.Id) {
                        // Existing user account is not the current user. Remove it and we'll add it to the current user below.
                        if (!existingUser.RemoveOAuthAccount(userInfo.ProviderName, userInfo.Id)) {
                            _logger.Error().Message("Unable to remove existing oauth account for existing user \"{0}\"", existingUser.EmailAddress).Tag("External Login").Identity(CurrentUser.EmailAddress).Property("User Info", userInfo).Property("User", CurrentUser).Property("ExistingUser", existingUser).SetActionContext(ActionContext).Write();
                            return null;
                        }

                        await _userRepository.SaveAsync(existingUser, o => o.Cache());
                    } else {
                        // User is already logged in.
                        return CurrentUser;
                    }
                }

                // Add it to the current user if it doesn't already exist and save it.
                CurrentUser.AddOAuthAccount(userInfo.ProviderName, userInfo.Id, userInfo.Email);
                await _userRepository.SaveAsync(CurrentUser, o => o.Cache());
                return CurrentUser;
            }

            // Create a new user account or return an existing one.
            if (existingUser != null) {
                if (!existingUser.IsEmailAddressVerified) {
                    existingUser.MarkEmailAddressVerified();
                    await _userRepository.SaveAsync(existingUser, o => o.Cache());
                }

                return existingUser;
            }

            // Check to see if a user already exists with this email address.
            var user = !String.IsNullOrEmpty(userInfo.Email) ? await _userRepository.GetByEmailAddressAsync(userInfo.Email) : null;
            if (user == null) {
                if (!Settings.Current.EnableAccountCreation)
                    throw new ApplicationException("Account Creation is currently disabled.");

                user = new User { FullName = userInfo.GetFullName(), EmailAddress = userInfo.Email };
                user.Roles.Add(AuthorizationRoles.Client);
                user.Roles.Add(AuthorizationRoles.User);
                await AddGlobalAdminRoleIfFirstUserAsync(user);
            }

            user.MarkEmailAddressVerified();
            user.AddOAuthAccount(userInfo.ProviderName, userInfo.Id, userInfo.Email);

            if (String.IsNullOrEmpty(user.Id))
                await _userRepository.AddAsync(user, o => o.Cache());
            else
                await _userRepository.SaveAsync(user, o => o.Cache());

            return user;
        }

        private async Task<bool> IsAccountCreationEnabledAsync(string token) {
            if (Settings.Current.EnableAccountCreation)
                return true;

            if (String.IsNullOrEmpty(token))
                return false;

            var organization = await _organizationRepository.GetByInviteTokenAsync(token);
            return organization != null;
        }

        private async Task AddInvitedUserToOrganizationAsync(string token, User user) {
            if (String.IsNullOrWhiteSpace(token) || user == null)
                return;

            var organization = await _organizationRepository.GetByInviteTokenAsync(token);
            var invite = organization?.GetInvite(token);
            if (organization == null || invite == null) {
                _logger.Info().Message("Unable to add the invited user \"{0}\". Invalid invite token: {1}", user.EmailAddress, token).Identity(user.EmailAddress).Property("User", user).SetActionContext(ActionContext).Write();
                return;
            }

            if (!user.IsEmailAddressVerified && String.Equals(user.EmailAddress, invite.EmailAddress, StringComparison.OrdinalIgnoreCase)) {
                _logger.Info().Message("Marking the invited users email address \"{0}\" as verified.", user.EmailAddress).Identity(user.EmailAddress).Property("User", user).SetActionContext(ActionContext).Write();
                user.MarkEmailAddressVerified();
                await _userRepository.SaveAsync(user, o => o.Cache());
            }

            if (!user.OrganizationIds.Contains(organization.Id)) {
                _logger.Info().Message("\"{0}\" joined from invite.", user.EmailAddress).Tag("Invite").Property("Organization", organization).Identity(user.EmailAddress).Property("User", user).SetActionContext(ActionContext).Write();
                user.OrganizationIds.Add(organization.Id);
                await _userRepository.SaveAsync(user, o => o.Cache());
            }

            organization.Invites.Remove(invite);
            await _organizationRepository.SaveAsync(organization, o => o.Cache());
        }

        private async Task ChangePasswordAsync(User user, string password, string tag) {
            if (String.IsNullOrEmpty(user.Salt))
                user.Salt = Core.Extensions.StringExtensions.GetNewToken();

            user.Password = password.ToSaltedHash(user.Salt);
            user.ResetPasswordResetToken();

            try {
                await _userRepository.SaveAsync(user, o => o.Cache());
                _logger.Info().Message("Changed password for \"{0}\"", user.EmailAddress).Tag(tag).Identity(user.EmailAddress).SetActionContext(ActionContext).Write();
            } catch (Exception ex) {
                _logger.Error().Exception(ex).Critical().Message("Error changing password for \"{0}\": {1}", user.EmailAddress, ex.Message).Tag(tag).Identity(user.EmailAddress).SetActionContext(ActionContext).Write();
                throw;
            }
        }

        private async Task ResetUserTokensAsync(User user, string tag) {
            try {
                var total = await _tokenRepository.RemoveAllByUserIdAsync(user.Id, o => o.ImmediateConsistency(true));
                _logger.Info().Message("Removed user {0} tokens for \"{1}\"", total, user.EmailAddress).Tag(tag).Identity(user.EmailAddress).SetActionContext(ActionContext).Write();
            } catch (Exception ex) {
                _logger.Error().Exception(ex).Critical().Message("Error removing user tokens for \"{0}\": {1}", user.EmailAddress, ex.Message).Tag(tag).Identity(user.EmailAddress).SetActionContext(ActionContext).Write();
            }
        }

        private async Task<string> GetTokenAsync(User user) {
            var userTokens = await _tokenRepository.GetByUserIdAsync(user.Id);
            var validAccessToken = userTokens.Documents.FirstOrDefault(t => (!t.ExpiresUtc.HasValue || t.ExpiresUtc > SystemClock.UtcNow) && t.Type == TokenType.Access);
            if (validAccessToken != null)
                return validAccessToken.Id;

            var token = await _tokenRepository.AddAsync(new Token {
                Id = Core.Extensions.StringExtensions.GetNewToken(),
                UserId = user.Id,
                CreatedUtc = SystemClock.UtcNow,
                UpdatedUtc = SystemClock.UtcNow,
                ExpiresUtc = SystemClock.UtcNow.AddMonths(3),
                CreatedBy = user.Id,
                Type = TokenType.Access
            }, o => o.Cache());

            return token.Id;
        }

        private bool IsValidActiveDirectoryLogin(string email, string password) {
            string domainUsername = _domainLoginProvider.GetUsernameFromEmailAddress(email);
            return domainUsername != null && _domainLoginProvider.Login(domainUsername, password);
        }

        private static bool IsValidPassword(string password) {
            if (String.IsNullOrWhiteSpace(password))
                return false;

            password = password.Trim();
            return password.Length >= 6 && password.Length <= 100;
        }
    }
}
