using System;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Api.Extensions;
using Exceptionless.Api.Models;
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
using Foundatio.Repositories;
using Foundatio.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using OAuth2.Client;
using OAuth2.Client.Impl;
using OAuth2.Configuration;
using OAuth2.Infrastructure;
using OAuth2.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Exceptionless.Api.Controllers {
    [Route(API_PREFIX + "/auth")]
    [Authorize(Policy = AuthorizationRoles.UserPolicy)]
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
        [AllowAnonymous]
        [HttpPost("login")]
        [SwaggerResponse(StatusCodes.Status200OK, Type = typeof(TokenResult))]
        public async Task<IActionResult> LoginAsync([FromBody] LoginModel model) {
            string email = model?.Email?.Trim().ToLowerInvariant();
            using (_logger.BeginScope(new ExceptionlessState().Tag("Login").Identity(email).SetHttpContext(HttpContext))) {
                if (String.IsNullOrEmpty(email)) {
                    _logger.LogError("Login failed: Email Address is required.");
                    return BadRequest("Email Address is required.");
                }

                if (String.IsNullOrWhiteSpace(model.Password)) {
                    _logger.LogError("Login failed for {EmailAddress}: Password is required.", email);
                    return BadRequest("Password is required.");
                }

                // Only allow 5 password attempts per 15 minute period.
                string userLoginAttemptsCacheKey = $"user:{email}:attempts";
                long userLoginAttempts = await _cache.IncrementAsync(userLoginAttemptsCacheKey, 1, SystemClock.UtcNow.Ceiling(TimeSpan.FromMinutes(15)));

                // Only allow 15 login attempts per 15 minute period by a single ip.
                string ipLoginAttemptsCacheKey = $"ip:{Request.GetClientIpAddress()}:attempts";
                long ipLoginAttempts = await _cache.IncrementAsync(ipLoginAttemptsCacheKey, 1, SystemClock.UtcNow.Ceiling(TimeSpan.FromMinutes(15)));

                if (userLoginAttempts > 5) {
                    _logger.LogError("Login denied for {EmailAddress} for the {UserLoginAttempts} time.", email, userLoginAttempts);
                    return Unauthorized();
                }

                if (ipLoginAttempts > 15) {
                    _logger.LogError("Login denied for {EmailAddress} for the {IPLoginAttempts} time.", Request.GetClientIpAddress(), ipLoginAttempts);
                    return Unauthorized();
                }

                User user;
                try {
                    user = await _userRepository.GetByEmailAddressAsync(email);
                } catch (Exception ex) {
                    _logger.LogCritical(ex, "Login failed for {EmailAddress}: {Message}", email, ex.Message);
                    return Unauthorized();
                }

                if (user == null) {
                    _logger.LogError("Login failed for {EmailAddress}: User not found.", email);
                    return Unauthorized();
                }

                if (!user.IsActive) {
                    _logger.LogError("Login failed for {EmailAddress}: The user is inactive.", user.EmailAddress);
                    return Unauthorized();
                }

                if (!Settings.Current.EnableActiveDirectoryAuth) {
                    if (String.IsNullOrEmpty(user.Salt)) {
                        _logger.LogError("Login failed for {EmailAddress}: The user has no salt defined.", user.EmailAddress);
                        return Unauthorized();
                    }

                    if (!user.IsCorrectPassword(model.Password)) {
                        _logger.LogError("Login failed for {EmailAddress}: Invalid Password.", user.EmailAddress);
                        return Unauthorized();
                    }

                    if (!PasswordMeetsRequirements(model.Password)) {
                        _logger.LogError("Login denied for {EmailAddress} for invalid password.", email);
                        return StatusCode(423, "Password requirements have changed. Password needs to be reset to meet the new requirements.");
                    }
                } else {
                    if (!IsValidActiveDirectoryLogin(email, model.Password)) {
                        _logger.LogError("Domain login failed for {EmailAddress}: Invalid Password or Account.", user.EmailAddress);
                        return Unauthorized();
                    }
                }

                if (!String.IsNullOrEmpty(model.InviteToken))
                    await AddInvitedUserToOrganizationAsync(model.InviteToken, user);

                await _cache.RemoveAsync(userLoginAttemptsCacheKey);
                await _cache.DecrementAsync(ipLoginAttemptsCacheKey, 1, SystemClock.UtcNow.Ceiling(TimeSpan.FromMinutes(15)));

                _logger.LogInformation("{EmailAddress} logged in.", user.EmailAddress);
                return Ok(new TokenResult { Token = await GetOrCreateAccessTokenAsync(user) });
            }
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("logout")]
        public async Task<IActionResult> LogoutAsync() {
            if (User.IsTokenAuthType())
                return Ok();

            string id = User.GetLoggedInUsersTokenId();
            if (String.IsNullOrEmpty(id))
                return Ok();

            try {
                await _tokenRepository.RemoveAsync(id);
            } catch (Exception ex) {
                using (_logger.BeginScope(new ExceptionlessState().Tag("Logout").Identity(CurrentUser.EmailAddress).SetHttpContext(HttpContext)))
                    _logger.LogCritical(ex, "Logout failed for {EmailAddress}: {Message}", CurrentUser.EmailAddress, ex.Message);
            }

            return Ok();
        }

        /// <summary>
        /// Sign up
        /// </summary>
        /// <param name="model">The sign up model.</param>
        /// <response code="400">The sign up model is invalid.</response>
        /// <response code="401">Sign up failed.</response>
        [AllowAnonymous]
        [HttpPost("signup")]
        [SwaggerResponse(StatusCodes.Status200OK, Type = typeof(TokenResult))]
        public async Task<IActionResult> SignupAsync([FromBody] SignupModel model) {
            bool valid = await IsAccountCreationEnabledAsync(model?.InviteToken);
            if (!valid)
                return BadRequest("Account Creation is currently disabled.");

            string email = model?.Email?.Trim().ToLowerInvariant();
            using (_logger.BeginScope(new ExceptionlessState().Tag("Signup").Identity(email).Property("Name", model != null ? model.Name : "<null>").Property("Password Length", model?.Password?.Length ?? 0).SetHttpContext(HttpContext))) {
                if (String.IsNullOrEmpty(email)) {
                    _logger.LogError("Signup failed: Email Address is required.");
                    return BadRequest("Email Address is required.");
                }

                if (String.IsNullOrWhiteSpace(model.Name)) {
                    _logger.LogError("Signup failed for {EmailAddress}: Name is required.", email);
                    return BadRequest("Name is required.");
                }

                if (!PasswordMeetsRequirements(model.Password)) {
                    _logger.LogError("Signup failed for {EmailAddress}: Invalid Password", email);
                    return BadRequest("Password must be at least 6 characters long.");
                }

                User user;
                try {
                    user = await _userRepository.GetByEmailAddressAsync(email);
                } catch (Exception ex) {
                    _logger.LogCritical(ex, "Signup failed for {EmailAddress}: {Message}", email, ex.Message);
                    return BadRequest();
                }

                if (user != null)
                    return await LoginAsync(model);

                string ipSignupAttemptsCacheKey = $"ip:{Request.GetClientIpAddress()}:signup:attempts";
                bool hasValidInviteToken = !String.IsNullOrWhiteSpace(model.InviteToken) && await _organizationRepository.GetByInviteTokenAsync(model.InviteToken) != null;
                if (!hasValidInviteToken) {
                    // Only allow 10 sign ups per hour period by a single ip.
                    long ipSignupAttempts = await _cache.IncrementAsync(ipSignupAttemptsCacheKey, 1, SystemClock.UtcNow.Ceiling(TimeSpan.FromHours(1)));
                    if (ipSignupAttempts > 10) {
                        _logger.LogError("Signup denied for {EmailAddress} for the {IPSignupAttempts} time.", email, ipSignupAttempts);
                        return BadRequest();
                    }
                }

                if (Settings.Current.EnableActiveDirectoryAuth && !IsValidActiveDirectoryLogin(email, model.Password)) {
                    _logger.LogError("Signup failed for {EmailAddress}: Active Directory authentication failed.", email);
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
                    _logger.LogCritical(ex, "Signup failed for {EmailAddress}: {Message}", email, errors);
                    return BadRequest(errors);
                } catch (Exception ex) {
                    _logger.LogCritical(ex, "Signup failed for {EmailAddress}: {Message}", email, ex.Message);
                    return BadRequest("An error occurred.");
                }

                if (hasValidInviteToken)
                    await AddInvitedUserToOrganizationAsync(model.InviteToken, user);

                if (!user.IsEmailAddressVerified)
                    await _mailer.SendUserEmailVerifyAsync(user);

                _logger.LogInformation("{EmailAddress} signed up.", user.EmailAddress);
                return Ok(new TokenResult { Token = await GetOrCreateAccessTokenAsync(user) });
            }
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [AllowAnonymous]
        [HttpPost("github")]
        [SwaggerResponse(StatusCodes.Status200OK, Type = typeof(TokenResult))]
        public Task<IActionResult> GitHubAsync([FromBody] JObject value) {
            return ExternalLoginAsync(value.ToObject<ExternalAuthInfo>(), 
                Settings.Current.GitHubAppId, 
                Settings.Current.GitHubAppSecret, 
                (f, c) => {
                    c.Scope = "user:email";
                    return new GitHubClient(f, c);
                }
            );
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [AllowAnonymous]
        [HttpPost("google")]
        [SwaggerResponse(StatusCodes.Status200OK, Type = typeof(TokenResult))]
        public Task<IActionResult> GoogleAsync([FromBody] JObject value) {
            return ExternalLoginAsync(value.ToObject<ExternalAuthInfo>(), 
                Settings.Current.GoogleAppId, 
                Settings.Current.GoogleAppSecret, 
                (f, c) => {
                    c.Scope = "profile email";
                    return new GoogleClient(f, c);
                }
            );
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [AllowAnonymous]
        [HttpPost("facebook")]
        [SwaggerResponse(StatusCodes.Status200OK, Type = typeof(TokenResult))]
        public Task<IActionResult> FacebookAsync([FromBody] JObject value) {
            return ExternalLoginAsync(value.ToObject<ExternalAuthInfo>(), 
                Settings.Current.FacebookAppId, 
                Settings.Current.FacebookAppSecret, 
                (f, c) => {
                    c.Scope = "email";
                    return new FacebookClient(f, c);
                }
            );
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [AllowAnonymous]
        [HttpPost("live")]
        [SwaggerResponse(StatusCodes.Status200OK, Type = typeof(TokenResult))]
        public Task<IActionResult> LiveAsync([FromBody] JObject value) {
            return ExternalLoginAsync(value.ToObject<ExternalAuthInfo>(), 
                Settings.Current.MicrosoftAppId, 
                Settings.Current.MicrosoftAppSecret, 
                (f, c) => {
                    c.Scope = "wl.emails";
                    return new WindowsLiveClient(f, c);
                }
            );
        }

        /// <summary>
        /// Removes an external login provider from the account
        /// </summary>
        /// <param name="providerName">The provider name.</param>
        /// <param name="providerUserId">The provider user id.</param>
        /// <response code="400">Invalid provider name.</response>
        /// <response code="500">An error while saving the user account.</response>
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("unlink/{providerName:minlength(1)}")]
        [SwaggerResponse(StatusCodes.Status200OK, Type = typeof(TokenResult))]
        public async Task<IActionResult> RemoveExternalLoginAsync(string providerName, [FromBody] string providerUserId) {
            using (_logger.BeginScope(new ExceptionlessState().Tag("External Login").Tag(providerName).Identity(CurrentUser.EmailAddress).Property("User", CurrentUser).Property("Provider User Id", providerUserId).SetHttpContext(HttpContext))) {
                if (String.IsNullOrWhiteSpace(providerName) || String.IsNullOrWhiteSpace(providerUserId)) {
                    _logger.LogError("Remove external login failed for {EmailAddress}: Invalid Provider Name or Provider User Id.", CurrentUser.EmailAddress);
                    return BadRequest("Invalid Provider Name or Provider User Id.");
                }

                if (CurrentUser.OAuthAccounts.Count <= 1 && String.IsNullOrEmpty(CurrentUser.Password)) {
                    _logger.LogError("Remove external login failed for {EmailAddress}: You must set a local password before removing your external login.", CurrentUser.EmailAddress);
                    return BadRequest("You must set a local password before removing your external login.");
                }

                try {
                    if (CurrentUser.RemoveOAuthAccount(providerName, providerUserId))
                        await _userRepository.SaveAsync(CurrentUser, o => o.Cache());
                } catch (Exception ex) {
                    _logger.LogCritical(ex, "Error removing external login for {EmailAddress}: {Message}", CurrentUser.EmailAddress, ex.Message);
                    throw;
                }

                await ResetUserTokensAsync(CurrentUser, nameof(RemoveExternalLoginAsync));

                _logger.LogInformation("{EmailAddress} removed an external login: {ProviderName}", CurrentUser.EmailAddress, providerName);
                return Ok(new TokenResult { Token = await GetOrCreateAccessTokenAsync(CurrentUser) });
            }
        }

        /// <summary>
        /// Change password
        /// </summary>
        /// <param name="model">The change password model.</param>
        /// <response code="400">Invalid change password model.</response>
        [HttpPost("change-password")]
        [SwaggerResponse(StatusCodes.Status200OK, Type = typeof(TokenResult))]
        public async Task<IActionResult> ChangePasswordAsync([FromBody] ChangePasswordModel model) {
            using (_logger.BeginScope(new ExceptionlessState().Tag("Change Password").Identity(CurrentUser.EmailAddress).Property("User", CurrentUser).Property("Password Length", model?.Password?.Length ?? 0).SetHttpContext(HttpContext))) {
                if (model == null || !PasswordMeetsRequirements(model.Password)) {
                    _logger.LogError("Change password failed for {EmailAddress}: The New Password must be at least 6 characters long.", CurrentUser.EmailAddress);
                    return BadRequest("The New Password must be at least 6 characters long.");
                }

                // User has a local account..
                if (!String.IsNullOrWhiteSpace(CurrentUser.Password)) {
                    if (String.IsNullOrWhiteSpace(model.CurrentPassword)) {
                        _logger.LogError("Change password failed for {EmailAddress}: The current password is incorrect.", CurrentUser.EmailAddress);
                        return BadRequest("The current password is incorrect.");
                    }

                    string encodedPassword = model.CurrentPassword.ToSaltedHash(CurrentUser.Salt);
                    if (!String.Equals(encodedPassword, CurrentUser.Password)) {
                        _logger.LogError("Change password failed for {EmailAddress}: The current password is incorrect.", CurrentUser.EmailAddress);
                        return BadRequest("The current password is incorrect.");
                    }

                    string newPasswordHash = model.Password.ToSaltedHash(CurrentUser.Salt);
                    if (String.Equals(newPasswordHash, CurrentUser.Password)) {
                        _logger.LogError("Change password failed for {EmailAddress}: The new password is the same as the current password.", CurrentUser.EmailAddress);
                        return BadRequest("The new password must be different than the previous password.");
                    }
                }

                await ChangePasswordAsync(CurrentUser, model.Password, nameof(ChangePasswordAsync));
                await ResetUserTokensAsync(CurrentUser, nameof(ChangePasswordAsync));

                string userLoginAttemptsCacheKey = $"user:{CurrentUser.EmailAddress}:attempts";
                await _cache.RemoveAsync(userLoginAttemptsCacheKey);

                string ipLoginAttemptsCacheKey = $"ip:{Request.GetClientIpAddress()}:attempts";
                long attempts = await _cache.DecrementAsync(ipLoginAttemptsCacheKey, 1, SystemClock.UtcNow.Ceiling(TimeSpan.FromMinutes(15)));
                if (attempts <= 0)
                    await _cache.RemoveAsync(ipLoginAttemptsCacheKey);

                _logger.LogInformation("{EmailAddress} changed their password.", CurrentUser.EmailAddress);
                return Ok(new TokenResult { Token = await GetOrCreateAccessTokenAsync(CurrentUser) });
            }
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [AllowAnonymous]
        [HttpGet("check-email-address/{email:minlength(1)}")]
        public async Task<IActionResult> IsEmailAddressAvailableAsync(string email) {
            if (String.IsNullOrWhiteSpace(email))
                return StatusCode(StatusCodes.Status204NoContent);

            email = email.Trim().ToLowerInvariant();
            if (CurrentUser != null && String.Equals(CurrentUser.EmailAddress, email, StringComparison.InvariantCultureIgnoreCase))
                return StatusCode(StatusCodes.Status201Created);

            // Only allow 3 checks attempts per hour period by a single ip.
            string ipEmailAddressAttemptsCacheKey = $"ip:{Request.GetClientIpAddress()}:email:attempts";
            long attempts = await _cache.IncrementAsync(ipEmailAddressAttemptsCacheKey, 1, SystemClock.UtcNow.Ceiling(TimeSpan.FromHours(1)));

            if (attempts > 3 || await _userRepository.GetByEmailAddressAsync(email) == null)
                return StatusCode(StatusCodes.Status204NoContent);

            return StatusCode(StatusCodes.Status201Created);
        }

        /// <summary>
        /// Forgot password
        /// </summary>
        /// <param name="email">The email address.</param>
        /// <response code="400">Invalid email address.</response>
        [AllowAnonymous]
        [HttpGet("forgot-password/{email:minlength(1)}")]
        public async Task<IActionResult> ForgotPasswordAsync(string email) {
            using (_logger.BeginScope(new ExceptionlessState().Tag("Forgot Password").Identity(email).SetHttpContext(HttpContext))) {
                if (String.IsNullOrWhiteSpace(email)) {
                    _logger.LogError("Forgot password failed: Please specify a valid Email Address.");
                    return BadRequest("Please specify a valid Email Address.");
                }

                // Only allow 3 checks attempts per hour period by a single ip.
                string ipResetPasswordAttemptsCacheKey = $"ip:{Request.GetClientIpAddress()}:password:attempts";
                long attempts = await _cache.IncrementAsync(ipResetPasswordAttemptsCacheKey, 1, SystemClock.UtcNow.Ceiling(TimeSpan.FromHours(1)));
                if (attempts > 3) {
                    _logger.LogError("Login denied for {EmailAddress} for the {ResetPasswordAttempts} time.", email, attempts);
                    return Ok();
                }

                email = email.Trim().ToLowerInvariant();
                var user = await _userRepository.GetByEmailAddressAsync(email);
                if (user == null) {
                    _logger.LogError("Forgot password failed for {EmailAddress}: No user was found.", email);
                    return Ok();
                }

                user.CreatePasswordResetToken();
                await _userRepository.SaveAsync(user, o => o.Cache());

                await _mailer.SendUserPasswordResetAsync(user);
                _logger.LogInformation("{EmailAddress} forgot their password.", user.EmailAddress);
                return Ok();
            }
        }

        /// <summary>
        /// Reset password
        /// </summary>
        /// <param name="model">The reset password model.</param>
        /// <response code="400">Invalid reset password model.</response>
        [AllowAnonymous]
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPasswordAsync([FromBody] ResetPasswordModel model) {
            if (String.IsNullOrEmpty(model?.PasswordResetToken)) {
                using (_logger.BeginScope(new ExceptionlessState().Tag("Reset Password").SetHttpContext(HttpContext)))
                    _logger.LogError("Reset password failed: Invalid Password Reset Token.");
                return BadRequest("Invalid Password Reset Token.");
            }

            var user = await _userRepository.GetByPasswordResetTokenAsync(model.PasswordResetToken);
            using (_logger.BeginScope(new ExceptionlessState().Tag("Reset Password").Identity(user?.EmailAddress).Property("User", user).Property("Password Length", model.Password?.Length ?? 0).SetHttpContext(HttpContext))) {
                if (user == null) {
                    _logger.LogError("Reset password failed: Invalid Password Reset Token.");
                    return BadRequest("Invalid Password Reset Token.");
                }

                if (!user.HasValidPasswordResetTokenExpiration()) {
                    _logger.LogError("Reset password failed for {EmailAddress}: Password Reset Token has expired.", user.EmailAddress);
                    return BadRequest("Password Reset Token has expired.");
                }

                if (!PasswordMeetsRequirements(model.Password)) {
                    _logger.LogError("Reset password failed for {EmailAddress}: The New Password must be at least 6 characters long.", user.EmailAddress);
                    return BadRequest("The New Password must be at least 6 characters long.");
                }

                // User has a local account..
                if (!String.IsNullOrWhiteSpace(user.Password)) {
                    string newPasswordHash = model.Password.ToSaltedHash(user.Salt);
                    if (String.Equals(newPasswordHash, user.Password)) {
                        _logger.LogError("Reset password failed for {EmailAddress}: The new password is the same as the current password.", user.EmailAddress);
                        return BadRequest("The new password must be different than the previous password.");
                    }
                }

                user.MarkEmailAddressVerified();
                await ChangePasswordAsync(user, model.Password, nameof(ResetPasswordAsync));
                await ResetUserTokensAsync(user, nameof(ResetPasswordAsync));

                string userLoginAttemptsCacheKey = $"user:{user.EmailAddress}:attempts";
                await _cache.RemoveAsync(userLoginAttemptsCacheKey);

                string ipLoginAttemptsCacheKey = $"ip:{Request.GetClientIpAddress()}:attempts";
                long attempts = await _cache.DecrementAsync(ipLoginAttemptsCacheKey, 1, SystemClock.UtcNow.Ceiling(TimeSpan.FromMinutes(15)));
                if (attempts <= 0)
                    await _cache.RemoveAsync(ipLoginAttemptsCacheKey);

                _logger.LogInformation("{EmailAddress} reset their password.", user.EmailAddress);
                return Ok();
            }
        }

        /// <summary>
        /// Cancel reset password
        /// </summary>
        /// <param name="token">The password reset token.</param>
        /// <response code="400">Invalid password reset token.</response>
        [AllowAnonymous]
        [HttpPost("cancel-reset-password/{token:minlength(1)}")]
        public async Task<IActionResult> CancelResetPasswordAsync(string token) {
            if (String.IsNullOrEmpty(token)) {
                using (_logger.BeginScope(new ExceptionlessState().Tag("Cancel Reset Password").SetHttpContext(HttpContext)))
                    _logger.LogError("Cancel reset password failed: Invalid Password Reset Token.");
                return BadRequest("Invalid password reset token.");
            }

            var user = await _userRepository.GetByPasswordResetTokenAsync(token);
            if (user == null)
                return Ok();

            user.ResetPasswordResetToken();
            await _userRepository.SaveAsync(user, o => o.Cache());

            using (_logger.BeginScope(new ExceptionlessState().Tag("Cancel Reset Password").Identity(user.EmailAddress).Property("User", user).SetHttpContext(HttpContext))) 
                _logger.LogInformation("{EmailAddress} canceled the reset password", user.EmailAddress);

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

        private async Task<IActionResult> ExternalLoginAsync<TClient>(ExternalAuthInfo authInfo, string appId, string appSecret, Func<IRequestFactory, IClientConfiguration, TClient> createClient) where TClient : OAuth2Client {
            using (_logger.BeginScope(new ExceptionlessState().Tag("External Login").Property("Auth Info", authInfo).SetHttpContext(HttpContext))) {
                if (String.IsNullOrEmpty(authInfo?.Code)) {
                    _logger.LogError("External login failed: Unable to get auth info.");
                    return NotFound();
                }

                if (String.IsNullOrEmpty(appId) || String.IsNullOrEmpty(appSecret))
                    return NotFound();

                var client = createClient(new RequestFactory(), new OAuth2.Configuration.ClientConfiguration {
                    ClientId = appId,
                    ClientSecret = appSecret,
                    RedirectUri = authInfo.RedirectUri
                });

                UserInfo userInfo;
                try {
                    userInfo = client.GetUserInfo(authInfo.Code);
                } catch (Exception ex) {
                    _logger.LogCritical(ex, "External login failed: {Message}", ex.Message);
                    return BadRequest("Unable to get user info.");
                }

                User user;
                try {
                    user = await FromExternalLoginAsync(userInfo);
                } catch (ApplicationException ex) {
                    _logger.LogCritical(ex, "External login failed for {EmailAddress}: {Message}", userInfo.Email, ex.Message);
                    return BadRequest("Account Creation is currently disabled.");
                } catch (Exception ex) {
                    _logger.LogCritical(ex, "External login failed for {EmailAddress}: {Message}", userInfo.Email, ex.Message);
                    return BadRequest("An error occurred while processing user info.");
                }

                if (user == null) {
                    _logger.LogCritical("External login failed for {EmailAddress}: Unable to process user info.", userInfo.Email);
                    return BadRequest("Unable to process user info.");
                }

                if (!String.IsNullOrWhiteSpace(authInfo.InviteToken))
                    await AddInvitedUserToOrganizationAsync(authInfo.InviteToken, user);

                _logger.LogInformation("{EmailAddress} logged in.", user.EmailAddress);
                return Ok(new TokenResult { Token = await GetOrCreateAccessTokenAsync(user) });
            }
        }

        private async Task<User> FromExternalLoginAsync(UserInfo userInfo) {
            var existingUser = await _userRepository.GetUserByOAuthProviderAsync(userInfo.ProviderName, userInfo.Id);

            // Link user accounts.
            if (CurrentUser != null) {
                if (existingUser != null) {
                    if (existingUser.Id != CurrentUser.Id) {
                        // Existing user account is not the current user. Remove it and we'll add it to the current user below.
                        if (!existingUser.RemoveOAuthAccount(userInfo.ProviderName, userInfo.Id)) {
                            using (_logger.BeginScope(new ExceptionlessState().Tag("External Login").Identity(CurrentUser.EmailAddress).Property("User Info", userInfo).Property("User", CurrentUser).Property("ExistingUser", existingUser).SetHttpContext(HttpContext)))
                                _logger.LogError("Unable to remove existing oauth account for existing user {EmailAddress}", existingUser.EmailAddress);

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

            using (_logger.BeginScope(new ExceptionlessState().Tag("Invite").Identity(user.EmailAddress).Property("User", user).SetHttpContext(HttpContext))) {
                var organization = await _organizationRepository.GetByInviteTokenAsync(token);
                var invite = organization?.GetInvite(token);
                if (organization == null || invite == null) {
                    _logger.LogInformation("Unable to add the invited user {EmailAddress}. Invalid invite token: {Token}", user.EmailAddress, token);
                    return;
                }

                if (!user.IsEmailAddressVerified && String.Equals(user.EmailAddress, invite.EmailAddress, StringComparison.OrdinalIgnoreCase)) {
                    _logger.LogInformation("Marking the invited users email address {EmailAddress} as verified.", user.EmailAddress);
                    user.MarkEmailAddressVerified();
                    await _userRepository.SaveAsync(user, o => o.Cache());
                }

                if (!user.OrganizationIds.Contains(organization.Id)) {
                    _logger.LogInformation("{EmailAddress} joined from invite.", user.EmailAddress);
                    user.OrganizationIds.Add(organization.Id);
                    await _userRepository.SaveAsync(user, o => o.Cache());
                }

                organization.Invites.Remove(invite);
                await _organizationRepository.SaveAsync(organization, o => o.Cache());
            }
        }

        private async Task ChangePasswordAsync(User user, string password, string tag) {
            using (_logger.BeginScope(new ExceptionlessState().Tag(tag).Identity(user.EmailAddress).SetHttpContext(HttpContext))) {
                if (String.IsNullOrEmpty(user.Salt))
                    user.Salt = Core.Extensions.StringExtensions.GetNewToken();

                user.Password = password.ToSaltedHash(user.Salt);
                user.ResetPasswordResetToken();

                try {
                    await _userRepository.SaveAsync(user, o => o.Cache());
                    _logger.LogInformation("Changed password for {EmailAddress}", user.EmailAddress);
                } catch (Exception ex) {
                    _logger.LogCritical(ex, "Error changing password for {EmailAddress}: {Message}", user.EmailAddress, ex.Message);
                    throw;
                }
            }
        }

        private async Task ResetUserTokensAsync(User user, string tag) {
            using (_logger.BeginScope(new ExceptionlessState().Tag(tag).Identity(user.EmailAddress).SetHttpContext(HttpContext))) {
                try {
                    long total = await _tokenRepository.RemoveAllByUserIdAsync(user.Id, o => o.ImmediateConsistency(true));
                    _logger.LogInformation("Removed user {TokenCount} tokens for {EmailAddress}", total, user.EmailAddress);
                } catch (Exception ex) {
                    _logger.LogCritical(ex, "Error removing user tokens for {EmailAddress}: {Message}", user.EmailAddress, ex.Message);
                }
            }
        }

        private async Task<string> GetOrCreateAccessTokenAsync(User user) {
            var userTokens = await _tokenRepository.GetByTypeAndUserIdAsync(TokenType.Access, user.Id);
            var validAccessToken = userTokens.Documents.FirstOrDefault(t => (!t.ExpiresUtc.HasValue || t.ExpiresUtc > SystemClock.UtcNow));
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

        private static bool PasswordMeetsRequirements(string password) {
            if (String.IsNullOrWhiteSpace(password))
                return false;

            password = password.Trim();
            return password.Length >= 6 && password.Length <= 100;
        }
    }
}
