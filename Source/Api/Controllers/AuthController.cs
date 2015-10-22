using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Description;
using Exceptionless.Api.Extensions;
using Exceptionless.Api.Models;
using Exceptionless.Api.Security;
using Exceptionless.Api.Utility;
using Exceptionless.Core;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using FluentValidation;
using Foundatio.Logging;
using Newtonsoft.Json.Linq;
using OAuth2.Client;
using OAuth2.Client.Impl;
using OAuth2.Configuration;
using OAuth2.Infrastructure;
using OAuth2.Models;

namespace Exceptionless.Api.Controllers {
    [RoutePrefix(API_PREFIX + "/auth")]
    public class AuthController : ExceptionlessApiController {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IUserRepository _userRepository;
        private readonly ITokenRepository _tokenRepository;
        private readonly IMailer _mailer;

        private static bool _isFirstUserChecked;

        public AuthController(IOrganizationRepository organizationRepository, IUserRepository userRepository, ITokenRepository tokenRepository, IMailer mailer) {
            _organizationRepository = organizationRepository;
            _userRepository = userRepository;
            _tokenRepository = tokenRepository;
            _mailer = mailer;
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
            if (String.IsNullOrWhiteSpace(model?.Email)) {
                Logger.Error().Message("Login failed: Email Address is required.").Tag("Login").SetActionContext(ActionContext).Write();
                return BadRequest("Email Address is required.");
            }

            if (String.IsNullOrWhiteSpace(model.Password)) {
                Logger.Error().Message("Login failed for \"{0}\": Password is required.", model.Email).Tag("Login").Identity(model.Email).SetActionContext(ActionContext).Write();
                return BadRequest("Password is required.");
            }

            User user;
            try {
                user = await _userRepository.GetByEmailAddressAsync(model.Email);
            } catch (Exception ex) {
                Logger.Error().Exception(ex).Critical().Message("Login failed for \"{0}\": {1}", model.Email, ex.Message).Tag("Login").Identity(model.Email).SetActionContext(ActionContext).Write();
                return Unauthorized();
            }

            if (user == null) {
                Logger.Error().Message("Login failed for \"{0}\": User not found.", model.Email).Tag("Login").Identity(model.Email).SetActionContext(ActionContext).Write();
                return Unauthorized();
            }
            
            if (!user.IsActive) {
                Logger.Error().Message("Login failed for \"{0}\": The user is inactive.", user.EmailAddress).Tag("Login").Identity(user.EmailAddress).Property("User", user).SetActionContext(ActionContext).Write();
                return Unauthorized();
            }

            if (String.IsNullOrEmpty(user.Salt)) {
                Logger.Error().Message("Login failed for \"{0}\": The user has no salt defined.", user.EmailAddress).Tag("Login").Identity(user.EmailAddress).Property("User", user).SetActionContext(ActionContext).Write();
                return Unauthorized();
            }

            string encodedPassword = model.Password.ToSaltedHash(user.Salt);
            if (!String.Equals(encodedPassword, user.Password)) {
                Logger.Error().Message("Login failed for \"{0}\": Invalid Password.", user.EmailAddress).Tag("Login").Identity(user.EmailAddress).Property("User", user).SetActionContext(ActionContext).Write();
                return Unauthorized();
            }

            if (!String.IsNullOrEmpty(model.InviteToken))
                await AddInvitedUserToOrganizationAsync(model.InviteToken, user);

            Logger.Info().Message("\"{0}\" logged in.", user.EmailAddress).Tag("Login").Identity(user.EmailAddress).Property("User", user).SetActionContext(ActionContext).Write();
            return Ok(new TokenResult { Token = await GetTokenAsync(user) });
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
            if (!Settings.Current.EnableAccountCreation) 
                return BadRequest("Account Creation is currently disabled.");

            if (String.IsNullOrWhiteSpace(model?.Email)) {
                Logger.Error().Message("Signup failed: Email Address is required.").Tag("Signup").Property("Name", model != null ? model.Name : "<null>").SetActionContext(ActionContext).Write();
                return BadRequest("Email Address is required.");
            }

            if (String.IsNullOrWhiteSpace(model.Name)) {
                Logger.Error().Message("Signup failed for \"{0}\": Name is required.", model.Email).Tag("Signup").Identity(model.Email).SetActionContext(ActionContext).Write();
                return BadRequest("Name is required.");
            }

            if (!IsValidPassword(model.Password)) {
                Logger.Error().Message("Signup failed for \"{0}\": Invalid Password", model.Email).Tag("Signup").Identity(model.Email).Property("Password Length", model.Password != null ? model.Password.Length : 0).SetActionContext(ActionContext).Write();
                return BadRequest("Password must be at least 6 characters long.");
            }

            User user;
            try {
                user = await _userRepository.GetByEmailAddressAsync(model.Email);
            } catch (Exception ex) {
                Logger.Error().Exception(ex).Critical().Message("Signup failed for \"{0}\": {1}", model.Email, ex.Message).Tag("Signup").Identity(model.Email).SetActionContext(ActionContext).Write();
                return BadRequest();
            }

            if (user != null) {
                Logger.Error().Message("Signup failed for \"{0}\": A user already exists.", user.EmailAddress).Tag("Signup").Identity(user.EmailAddress).SetActionContext(ActionContext).Write();
                return BadRequest("A user already exists with this email address.");
            }

            user = new User {
                IsActive = true,
                FullName = model.Name, 
                EmailAddress = model.Email,
                IsEmailAddressVerified = false
            };
            user.CreateVerifyEmailAddressToken();
            user.Roles.Add(AuthorizationRoles.Client);
            user.Roles.Add(AuthorizationRoles.User);
            await AddGlobalAdminRoleIfFirstUserAsync(user);

            user.Salt = Core.Extensions.StringExtensions.GetRandomString(16);
            user.Password = model.Password.ToSaltedHash(user.Salt);

            try {
                user = await _userRepository.SaveAsync(user);
            } catch (ValidationException ex) {
                var errors = String.Join(", ", ex.Errors);
                Logger.Error().Critical().Message("Signup failed for \"{0}\": {1}", model.Email, errors).Tag("Signup").Identity(user.EmailAddress).Property("User", user).SetActionContext(ActionContext).Write();
                return BadRequest(errors);
            } catch (Exception ex) {
                Logger.Error().Exception(ex).Critical().Message("Signup failed for \"{0}\": {1}", model.Email, ex.Message).Tag("Signup").Identity(user.EmailAddress).Property("User", user).SetActionContext(ActionContext).Write();
                return BadRequest("An error occurred.");
            }

            if (!String.IsNullOrEmpty(model.InviteToken))
                await AddInvitedUserToOrganizationAsync(model.InviteToken, user);

            if (!user.IsEmailAddressVerified)
                await _mailer.SendVerifyEmailAsync(user);

            Logger.Info().Message("\"{0}\" signed up.", user.EmailAddress).Tag("Signup").Identity(user.EmailAddress).Property("User", user).SetActionContext(ActionContext).Write();
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

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost]
        [Route("unlink/{providerName:minlength(1)}")]
        [Authorize(Roles = AuthorizationRoles.User)]
        public async Task<IHttpActionResult> RemoveExternalLoginAsync(string providerName, [NakedBody] string providerUserId) {
            if (String.IsNullOrEmpty(providerName) || String.IsNullOrEmpty(providerUserId)) {
                Logger.Error().Message("Remove external login failed for \"{0}\": Invalid Provider Name or Provider User Id.", ExceptionlessUser.EmailAddress).Tag("External Login", providerName).Identity(ExceptionlessUser.EmailAddress).Property("User", ExceptionlessUser).Property("Provider User Id", providerUserId).SetActionContext(ActionContext).Write();
                return BadRequest("Invalid Provider Name or Provider User Id.");
            }

            if (ExceptionlessUser.OAuthAccounts.Count <= 1 && String.IsNullOrEmpty(ExceptionlessUser.Password)) {
                Logger.Error().Message("Remove external login failed for \"{0}\": You must set a local password before removing your external login.", ExceptionlessUser.EmailAddress).Tag("External Login", providerName).Identity(ExceptionlessUser.EmailAddress).Property("User", ExceptionlessUser).Property("Provider User Id", providerUserId).SetActionContext(ActionContext).Write();
                return BadRequest("You must set a local password before removing your external login.");
            }

            if (ExceptionlessUser.RemoveOAuthAccount(providerName, providerUserId))
                await _userRepository.SaveAsync(ExceptionlessUser);

            Logger.Info().Message("\"{0}\" removed an external login: \"{1}\"", ExceptionlessUser.EmailAddress, providerName).Tag("External Login", providerName).Identity(ExceptionlessUser.EmailAddress).Property("User", ExceptionlessUser).SetActionContext(ActionContext).Write();
            return Ok();
        }

        /// <summary>
        /// Change password
        /// </summary>
        /// <param name="model">The change password model.</param>
        /// <response code="400">Invalid change password model.</response>
        [HttpPost]
        [Route("change-password")]
        [Authorize(Roles = AuthorizationRoles.User)]
        public async Task<IHttpActionResult> ChangePasswordAsync(ChangePasswordModel model) {
            if (model == null || !IsValidPassword(model.Password)) {
                Logger.Error().Message("Change password failed for \"{0}\": The New Password must be at least 6 characters long.", ExceptionlessUser.EmailAddress).Tag("Change Password").Identity(ExceptionlessUser.EmailAddress).Property("User", ExceptionlessUser).Property("Password Length", model?.Password != null ? model.Password.Length : 0).SetActionContext(ActionContext).Write();
                return BadRequest("The New Password must be at least 6 characters long.");
            }

            // User has a local account..
            if (!String.IsNullOrWhiteSpace(ExceptionlessUser.Password)) {
                if (String.IsNullOrWhiteSpace(model.CurrentPassword)) {
                    Logger.Error().Message("Change password failed for \"{0}\": The current password is incorrect.", ExceptionlessUser.EmailAddress).Tag("Change Password").Identity(ExceptionlessUser.EmailAddress).Property("User", ExceptionlessUser).SetActionContext(ActionContext).Write();
                    return BadRequest("The current password is incorrect.");
                }

                string encodedPassword = model.CurrentPassword.ToSaltedHash(ExceptionlessUser.Salt);
                if (!String.Equals(encodedPassword, ExceptionlessUser.Password)) {
                    Logger.Error().Message("Change password failed for \"{0}\": The current password is incorrect.", ExceptionlessUser.EmailAddress).Tag("Change Password").Identity(ExceptionlessUser.EmailAddress).Property("User", ExceptionlessUser).SetActionContext(ActionContext).Write();
                    return BadRequest("The current password is incorrect.");
                }
            }

            await ChangePasswordAsync(ExceptionlessUser, model.Password);

            Logger.Info().Message("\"{0}\" changed their password.", ExceptionlessUser.EmailAddress).Identity(ExceptionlessUser.EmailAddress).Property("User", ExceptionlessUser).SetActionContext(ActionContext).Write();
            return Ok();
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet]
        [Route("check-email-address/{email:minlength(1)}")]
        public async Task<IHttpActionResult> IsEmailAddressAvailableAsync(string email) {
            if (String.IsNullOrWhiteSpace(email))
                return StatusCode(HttpStatusCode.NoContent);
            
            if (ExceptionlessUser != null && String.Equals(ExceptionlessUser.EmailAddress, email, StringComparison.OrdinalIgnoreCase))
                return StatusCode(HttpStatusCode.Created);

            if (await _userRepository.GetByEmailAddressAsync(email) == null)
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
                Logger.Error().Message("Forgot password failed: Please specify a valid Email Address.").Tag("Forgot Password").SetActionContext(ActionContext).Write();
                return BadRequest("Please specify a valid Email Address.");
            }

            var user = await _userRepository.GetByEmailAddressAsync(email);
            if (user == null) {
                Logger.Error().Message("Forgot password failed for \"{0}\": No user was found.", email).Tag("Forgot Password").Identity(email).Property("Email Address", email).SetActionContext(ActionContext).Write();
                return BadRequest("No user was found with this Email Address.");
            }

            user.CreatePasswordResetToken();
            await _userRepository.SaveAsync(user);

            await _mailer.SendPasswordResetAsync(user);

            Logger.Info().Message("\"{0}\" forgot their password.", user.EmailAddress).Identity(user.EmailAddress).Property("User", user).SetActionContext(ActionContext).Write();
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
                Logger.Error().Message("Reset password failed: Invalid Password Reset Token.").Tag("Reset Password").SetActionContext(ActionContext).Write();
                return BadRequest("Invalid Password Reset Token.");
            }

            var user = await _userRepository.GetByPasswordResetTokenAsync(model.PasswordResetToken);
            if (user == null) {
                Logger.Error().Message("Reset password failed: Invalid Password Reset Token.").Tag("Reset Password").SetActionContext(ActionContext).Write();
                return BadRequest("Invalid Password Reset Token.");
            }

            if (!user.HasValidPasswordResetTokenExpiration()) {
                Logger.Error().Message("Reset password failed for \"{0}\": Password Reset Token has expired.", user.EmailAddress).Tag("Reset Password").Identity(user.EmailAddress).Property("User", user).SetActionContext(ActionContext).Write();
                return BadRequest("Password Reset Token has expired.");
            }

            if (!IsValidPassword(model.Password)) {
                Logger.Error().Message("Reset password failed for \"{0}\": The New Password must be at least 6 characters long.", user.EmailAddress).Tag("Reset Password").Identity(user.EmailAddress).Property("User", user).Property("Password Length", model.Password != null ? model.Password.Length : 0).SetActionContext(ActionContext).Write();
                return BadRequest("The New Password must be at least 6 characters long.");
            }

            user.MarkEmailAddressVerified();
            await ChangePasswordAsync(user, model.Password);

            Logger.Info().Message("\"{0}\" reset their password.", user.EmailAddress).Identity(user.EmailAddress).Property("User", user).SetActionContext(ActionContext).Write();
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
                Logger.Error().Message("Cancel reset password failed: Invalid Password Reset Token.").Tag("Reset Password").SetActionContext(ActionContext).Write();
                return BadRequest("Invalid password reset token.");
            }

            var user = await _userRepository.GetByPasswordResetTokenAsync(token);
            if (user == null)
                return Ok();
            
            user.ResetPasswordResetToken();
            await _userRepository.SaveAsync(user);

            Logger.Info().Message("\"{0}\" canceled the reset password", user.EmailAddress).Identity(user.EmailAddress).Property("User", user).SetActionContext(ActionContext).Write();
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
                Logger.Error().Message("External login failed: Unable to get auth info.").Tag("External Login").Property("Auth Info", authInfo).SetActionContext(ActionContext).Write();
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
                Logger.Error().Exception(ex).Critical().Message("External login failed: {0}", ex.Message).Tag("External Login", client.Name).Property("Auth Info", authInfo).SetActionContext(ActionContext).Write();
                return BadRequest("Unable to get user info.");
            }

            User user;
            try {
                user = await FromExternalLoginAsync(userInfo);
            } catch (ApplicationException) {
                return BadRequest("Account Creation is currently disabled.");
            } catch (Exception ex) {
                Logger.Error().Exception(ex).Critical().Message("External login failed for \"{0}\": {1}", userInfo.Email, ex.Message).Tag("External Login", client.Name).Property("User Info", userInfo).Property("Auth Info", authInfo).SetActionContext(ActionContext).Write();
                return BadRequest("An error occurred while processing user info.");
            }

            if (user == null) {
                Logger.Error().Critical().Message("External login failed for \"{0}\": Unable to process user info.", userInfo.Email).Tag("External Login", client.Name).Property("User Info", userInfo).Property("Auth Info", authInfo).SetActionContext(ActionContext).Write();
                return BadRequest("Unable to process user info.");
            }

            if (!String.IsNullOrWhiteSpace(authInfo.InviteToken))
                await AddInvitedUserToOrganizationAsync(authInfo.InviteToken, user);

            Logger.Info().Message("\"{0}\" logged in.", user.EmailAddress).Tag("External Login", client.Name).Identity(user.EmailAddress).Property("User", user).SetActionContext(ActionContext).Write();
            return Ok(new TokenResult { Token = await GetTokenAsync(user) });
        }

        private async Task<User> FromExternalLoginAsync(UserInfo userInfo) {
            User existingUser = await _userRepository.GetUserByOAuthProviderAsync(userInfo.ProviderName, userInfo.Id);
            
            // Link user accounts.
            if (ExceptionlessUser != null) {
                if (existingUser != null) {
                    if (existingUser.Id != ExceptionlessUser.Id) {
                        // Existing user account is not the current user. Remove it and we'll add it to the current user below.
                        if (!existingUser.RemoveOAuthAccount(userInfo.ProviderName, userInfo.Id))
                            return null;

                        await _userRepository.SaveAsync(existingUser);
                    } else {
                        // User is already logged in.
                        return ExceptionlessUser;
                    }
                }

                // Add it to the current user if it doesn't already exist and save it.
                ExceptionlessUser.AddOAuthAccount(userInfo.ProviderName, userInfo.Id, userInfo.Email);
                await _userRepository.SaveAsync(ExceptionlessUser);
                return ExceptionlessUser;
            }

            // Create a new user account or return an existing one.
            if (existingUser != null) {
                if (!existingUser.IsEmailAddressVerified) {
                    existingUser.MarkEmailAddressVerified();
                    await _userRepository.SaveAsync(existingUser);
                }

                return existingUser;
            }

            // Check to see if a user already exists with this email address.
            User user = !String.IsNullOrEmpty(userInfo.Email) ? await _userRepository.GetByEmailAddressAsync(userInfo.Email) : null;
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
            await _userRepository.SaveAsync(user);

            return user;
        }

        private async Task AddInvitedUserToOrganizationAsync(string token, User user) {
            if (String.IsNullOrWhiteSpace(token) || user == null)
                return;
            
            var organization = await _organizationRepository.GetByInviteTokenAsync(token);
            var invite = organization.GetInvite(token);
            if (organization == null || invite == null) {
                Logger.Info().Message("Unable to add the invited user \"{0}\". Invalid invite token: {1}", user.EmailAddress, token).Identity(user.EmailAddress).Property("User", user).SetActionContext(ActionContext).Write();
                return;
            }
            
            if (!user.IsEmailAddressVerified && String.Equals(user.EmailAddress, invite.EmailAddress, StringComparison.OrdinalIgnoreCase)) {
                Logger.Info().Message("Marking the invited users email address \"{0}\" as verified.", user.EmailAddress).Identity(user.EmailAddress).Property("User", user).SetActionContext(ActionContext).Write();
                user.MarkEmailAddressVerified();
                await _userRepository.SaveAsync(user);
            }

            if (!user.OrganizationIds.Contains(organization.Id)) {
                Logger.Info().Message("\"{0}\" joined from invite.", user.EmailAddress).Tag("Invite").Property("Organization", organization).Identity(user.EmailAddress).Property("User", user).SetActionContext(ActionContext).Write();
                user.OrganizationIds.Add(organization.Id);
                await _userRepository.SaveAsync(user);
            }

            organization.Invites.Remove(invite);
            await _organizationRepository.SaveAsync(organization);
        }

        private Task ChangePasswordAsync(User user, string password) {
            if (String.IsNullOrEmpty(user.Salt))
                user.Salt = Core.Extensions.StringExtensions.GetNewToken();

            user.Password = password.ToSaltedHash(user.Salt);
            user.ResetPasswordResetToken();
            return _userRepository.SaveAsync(user);
        }

        private async Task<string> GetTokenAsync(User user) {
            var userTokens = await _tokenRepository.GetByUserIdAsync(user.Id);
            var validAccessToken = userTokens.Documents.FirstOrDefault(t => (!t.ExpiresUtc.HasValue || t.ExpiresUtc > DateTime.UtcNow) && t.Type == TokenType.Access);
            if (validAccessToken != null)
                return validAccessToken.Id;

            var token = await _tokenRepository.AddAsync(new Token {
                Id = Core.Extensions.StringExtensions.GetNewToken(),
                UserId = user.Id,
                CreatedUtc = DateTime.UtcNow,
                ModifiedUtc = DateTime.UtcNow,
                CreatedBy = user.Id,
                Type = TokenType.Access
            });

            return token.Id;
        }

        private static bool IsValidPassword(string password) {
            if (String.IsNullOrWhiteSpace(password))
                return false;

            password = password.Trim();
            return password.Length >= 6 && password.Length <= 100;
        }
    }
}