using System;
using System.Net;
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
using Newtonsoft.Json.Linq;
using NLog.Fluent;
using OAuth2.Client.Impl;
using OAuth2.Configuration;
using OAuth2.Infrastructure;
using OAuth2.Models;

namespace Exceptionless.Api.Controllers {
    [RoutePrefix(API_PREFIX + "/auth")]
    public class AuthController : ExceptionlessApiController {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IUserRepository _userRepository;
        private readonly IMailer _mailer;
        private readonly TokenManager _tokenManager;

        private static bool _isFirstUserChecked;

        public AuthController(IOrganizationRepository organizationRepository, IUserRepository userRepository, IMailer mailer, TokenManager tokenManager) {
            _organizationRepository = organizationRepository;
            _userRepository = userRepository;
            _mailer = mailer;
            _tokenManager = tokenManager;
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
        public IHttpActionResult Login(LoginModel model) {
            if (model == null || String.IsNullOrWhiteSpace(model.Email)) {
                Log.Error().Message("Login failed: Email Address is required.").Tag("Login").Write();
                return BadRequest("Email Address is required.");
            }

            if (String.IsNullOrWhiteSpace(model.Password)) {
                Log.Error().Message("Login failed: Password is required.").Tag("Login").Property("Email Address", model.Email).Write();
                return BadRequest("Password is required.");
            }

            User user;
            try {
                user = _userRepository.GetByEmailAddress(model.Email);
            } catch (Exception ex) {
                Log.Error().Exception(ex).Critical().Message("Login failed: {0}", ex.Message).Tag("Login").Property("Email Address", model.Email).Write();
                return Unauthorized();
            }

            if (user == null) {
                Log.Error().Message("Login failed: No user was found with this email address.").Tag("Login").Property("Email Address", model.Email).Write();
                return Unauthorized();
            }
            
            if (!user.IsActive) {
                Log.Error().Message("Login failed: The user account \"{0}\" is inactive.", user.Id).Tag("Login").Property("User", user).Write();
                return Unauthorized();
            }

            if (String.IsNullOrEmpty(user.Salt)) {
                Log.Error().Message("Login failed: The user account \"{0}\" has no salt defined.", user.Id).Tag("Login").Property("User", user).Write();
                return Unauthorized();
            }

            string encodedPassword = model.Password.ToSaltedHash(user.Salt);
            if (!String.Equals(encodedPassword, user.Password)) {
                Log.Error().Message("Login failed: Invalid Password for user account \"{0}\".", user.Id).Tag("Login").Property("User", user).Write();
                return Unauthorized();
            }

            if (!String.IsNullOrEmpty(model.InviteToken))
				AddInvitedUserToOrganization(model.InviteToken, user);

            Log.Info().Message("Login").Tag("Login").Property("User", user).Write();
            return Ok(new TokenResult { Token = GetToken(user) });
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
        public IHttpActionResult Signup(SignupModel model) {
            if (!Settings.Current.EnableAccountCreation) 
                return BadRequest("Account Creation is currently disabled.");

            if (model == null || String.IsNullOrWhiteSpace(model.Email)) {
                Log.Error().Message("Signup failed: Email Address is required.").Tag("Signup").Property("Name", model != null ? model.Name : "<null>").Write();
                return BadRequest("Email Address is required.");
            }

            if (String.IsNullOrWhiteSpace(model.Name)) {
                Log.Error().Message("Signup failed: Name is required.").Tag("Signup").Property("Name", model.Name).Write();
                return BadRequest("Name is required.");
            }

            if (!IsValidPassword(model.Password)) {
                Log.Error().Message("Signup failed: Invalid Password").Tag("Signup").Property("Email Address", model.Email).Property("Password Length", model.Password != null ? model.Password.Length : 0).Write();
                return BadRequest("Password must be at least 6 characters long.");
            }

            User user;
            try {
                user = _userRepository.GetByEmailAddress(model.Email);
            } catch (Exception ex) {
                Log.Error().Exception(ex).Critical().Message("Signup failed: {0}", ex.Message).Tag("Signup").Property("Email Address", model.Email).Write();
                return BadRequest();
            }

            if (user != null) {
                Log.Error().Message("Signup failed: A user already exists with \"{0}\" email address.", user.Id).Tag("Signup").Property("Email Address", model.Email).Write();
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
            AddGlobalAdminRoleIfFirstUser(user);

            user.Salt = Core.Extensions.StringExtensions.GetRandomString(16);
            user.Password = model.Password.ToSaltedHash(user.Salt);

            try {
                user = _userRepository.Save(user);
            } catch (ValidationException ex) {
                var errors = String.Join(", ", ex.Errors);
                Log.Error().Critical().Message("Signup failed: {0}", errors).Tag("Signup").Property("Email Address", model.Email).Write();
                return BadRequest(errors);
            } catch (Exception ex) {
                Log.Error().Exception(ex).Critical().Message("Signup failed: {0}", ex.Message).Tag("Signup").Property("User", user).Write();
                return BadRequest("An error occurred.");
            }

            if (!String.IsNullOrEmpty(model.InviteToken))
                AddInvitedUserToOrganization(model.InviteToken, user);

            if (!user.IsEmailAddressVerified)
                _mailer.SendVerifyEmail(user);

            Log.Info().Message("Signup").Tag("Signup").Property("User", user).Write();
            return Ok(new TokenResult { Token = GetToken(user) });
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost]
        [Route("github")]
        [ResponseType(typeof(TokenResult))]
        public IHttpActionResult GitHub(JObject value) {
            var authInfo = value.ToObject<ExternalAuthInfo>();
            if (authInfo == null || String.IsNullOrEmpty(authInfo.Code)) {
                Log.Error().Message("External login failed: Unable to get auth info.").Tag("External Login", "GitHub").Property("Auth Info", authInfo).Write();
                return NotFound();
            }

            if (String.IsNullOrEmpty(Settings.Current.GitHubAppId) || String.IsNullOrEmpty(Settings.Current.GitHubAppSecret))
                return NotFound();

            var client = new GitHubWithPrivateEmailsClient(new RequestFactory(), new RuntimeClientConfiguration {
                ClientId = Settings.Current.GitHubAppId,
                ClientSecret = Settings.Current.GitHubAppSecret,
                RedirectUri = authInfo.RedirectUri
            });

            UserInfo userInfo;
            try {
                userInfo = client.GetUserInfo(authInfo.Code);
            } catch (Exception ex) {
                Log.Error().Exception(ex).Critical().Message("External login failed: {0}", ex.Message).Tag("External Login", "GitHub").Property("Auth Info", authInfo).Write();
                return BadRequest("Unable to get user info.");
            }

            User user;
            try {
                user = AddExternalLogin(userInfo);
            } catch (ApplicationException) {
                return BadRequest("Account Creation is currently disabled.");
            } catch (Exception ex) {
                Log.Error().Exception(ex).Critical().Message("External login failed: {0}", ex.Message).Tag("External Login", "GitHub").Property("User Info", userInfo).Property("Auth Info", authInfo).Write();
                return BadRequest("An error occurred while processing user info.");
            }

            if (user == null) {
                Log.Error().Message("External login failed: Unable to process user info.").Critical().Tag("External Login", "GitHub").Property("User Info", userInfo).Property("Auth Info", authInfo).Write();
                return BadRequest("Unable to process user info.");
            }

            if (!String.IsNullOrWhiteSpace(authInfo.InviteToken))
                AddInvitedUserToOrganization(authInfo.InviteToken, user);

            Log.Info().Message("Login").Tag("External Login", "GitHub").Property("User", user).Write();
            return Ok(new TokenResult { Token = GetToken(user) });
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost]
        [Route("google")]
        [ResponseType(typeof(TokenResult))]
        public IHttpActionResult Google(JObject value) {
            var authInfo = value.ToObject<ExternalAuthInfo>();
            if (authInfo == null || String.IsNullOrEmpty(authInfo.Code)) {
                Log.Error().Message("External login failed: Unable to get auth info.").Tag("External Login", "Google").Property("Auth Info", authInfo).Write();
                return NotFound();
            }

            if (String.IsNullOrEmpty(Settings.Current.GoogleAppId) || String.IsNullOrEmpty(Settings.Current.GoogleAppSecret))
                return NotFound();

            var client = new GoogleClient(new RequestFactory(), new RuntimeClientConfiguration {
                ClientId = Settings.Current.GoogleAppId,
                ClientSecret = Settings.Current.GoogleAppSecret,
                RedirectUri = authInfo.RedirectUri
            });

            UserInfo userInfo;
            try {
                userInfo = client.GetUserInfo(authInfo.Code);
            } catch (Exception ex) {
                Log.Error().Exception(ex).Critical().Message("External login failed: {0}", ex.Message).Tag("External Login", "Google").Property("Auth Info", authInfo).Write();
                return BadRequest("Unable to get user info.");
            }

            User user;
            try {
                user = AddExternalLogin(userInfo);
            } catch (ApplicationException) {
                return BadRequest("Account Creation is currently disabled.");
            } catch (Exception ex) {
                Log.Error().Exception(ex).Critical().Message("External login failed: {0}", ex.Message).Tag("External Login", "Google").Property("User Info", userInfo).Property("Auth Info", authInfo).Write();
                return BadRequest("An error occurred while processing user info.");
            }

            if (user == null) {
                Log.Error().Message("External login failed: Unable to process user info.").Critical().Tag("External Login", "Google").Property("User Info", userInfo).Property("Auth Info", authInfo).Write();
                return BadRequest("Unable to process user info.");
            }

            if (!String.IsNullOrWhiteSpace(authInfo.InviteToken))
                AddInvitedUserToOrganization(authInfo.InviteToken, user);

            Log.Info().Message("Login").Tag("External Login", "Google").Property("User", user).Write();
            return Ok(new TokenResult { Token = GetToken(user) });
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost]
        [Route("facebook")]
        [ResponseType(typeof(TokenResult))]
        public IHttpActionResult Facebook(JObject value) {
            var authInfo = value.ToObject<ExternalAuthInfo>();
            if (authInfo == null || String.IsNullOrEmpty(authInfo.Code)) {
                Log.Error().Message("External login failed: Unable to get auth info.").Tag("External Login", "Facebook").Property("Auth Info", authInfo).Write();
                return NotFound();
            }

            if (String.IsNullOrEmpty(Settings.Current.FacebookAppId) || String.IsNullOrEmpty(Settings.Current.FacebookAppSecret))
                return NotFound();

            var client = new FacebookClient(new RequestFactory(), new RuntimeClientConfiguration {
                ClientId = Settings.Current.FacebookAppId,
                ClientSecret = Settings.Current.FacebookAppSecret,
                RedirectUri = authInfo.RedirectUri
            });

            UserInfo userInfo;
            try {
                userInfo = client.GetUserInfo(authInfo.Code);
            } catch (Exception ex) {
                Log.Error().Exception(ex).Critical().Message("External login failed: {0}", ex.Message).Tag("External Login", "Facebook").Property("Auth Info", authInfo).Write();
                return BadRequest("Unable to get user info.");
            }

            User user;
            try {
                user = AddExternalLogin(userInfo);
            } catch (ApplicationException) {
                return BadRequest("Account Creation is currently disabled.");
            } catch (Exception ex) {
                Log.Error().Exception(ex).Critical().Message("External login failed: {0}", ex.Message).Tag("External Login", "Facebook").Property("User Info", userInfo).Property("Auth Info", authInfo).Write();
                return BadRequest("An error occurred while processing user info.");
            }

            if (user == null) {
                Log.Error().Message("External login failed: Unable to process user info.").Critical().Tag("External Login", "Facebook").Property("User Info", userInfo).Property("Auth Info", authInfo).Write();
                return BadRequest("Unable to process user info.");
            }

            if (!String.IsNullOrWhiteSpace(authInfo.InviteToken))
                AddInvitedUserToOrganization(authInfo.InviteToken, user);

            Log.Info().Message("Login").Tag("External Login", "Facebook").Property("User", user).Write();
            return Ok(new TokenResult { Token = GetToken(user) });
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost]
        [Route("live")]
        [ResponseType(typeof(TokenResult))]
        public IHttpActionResult Live(JObject value) {
            var authInfo = value.ToObject<ExternalAuthInfo>();
            if (authInfo == null || String.IsNullOrEmpty(authInfo.Code)) {
                Log.Error().Message("External login failed: Unable to get auth info.").Tag("External Login", "WindowsLive").Property("Auth Info", authInfo).Write();
                return NotFound();
            }

            if (String.IsNullOrEmpty(Settings.Current.MicrosoftAppId) || String.IsNullOrEmpty(Settings.Current.MicrosoftAppSecret))
                return NotFound();

            var client = new WindowsLiveClient(new RequestFactory(), new RuntimeClientConfiguration {
                ClientId = Settings.Current.MicrosoftAppId,
                ClientSecret = Settings.Current.MicrosoftAppSecret,
                RedirectUri = authInfo.RedirectUri
            });

            UserInfo userInfo;
            try {
                userInfo = client.GetUserInfo(authInfo.Code);
            } catch (Exception ex) {
                Log.Error().Exception(ex).Critical().Message("External login failed: {0}", ex.Message).Tag("External Login", "WindowsLive").Property("Auth Info", authInfo).Write();
                return BadRequest("External login failed: Unable to get user info.");
            }

            User user;
            try {
                user = AddExternalLogin(userInfo);
            } catch (ApplicationException) {
                return BadRequest("Account Creation is currently disabled.");
            } catch (Exception ex) {
                Log.Error().Exception(ex).Critical().Message("External login failed: {0}", ex.Message).Tag("External Login", "WindowsLive").Property("User Info", userInfo).Property("Auth Info", authInfo).Write();
                return BadRequest("An error occurred while processing user info.");
            }

            if (user == null) {
                Log.Error().Message("External login failed: Unable to process user info.").Critical().Tag("External Login", "WindowsLive").Property("User Info", userInfo).Property("Auth Info", authInfo).Write();
                return BadRequest("Unable to process user info.");
            }

            if (!String.IsNullOrWhiteSpace(authInfo.InviteToken))
                AddInvitedUserToOrganization(authInfo.InviteToken, user);

            Log.Info().Message("Login").Tag("External Login", "WindowsLive").Property("User", user).Write();
            return Ok(new TokenResult { Token = GetToken(user) });
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost]
        [Route("unlink/{providerName:minlength(1)}")]
        [Authorize(Roles = AuthorizationRoles.User)]
        public IHttpActionResult RemoveExternalLogin(string providerName, [NakedBody] string providerUserId) {
            if (String.IsNullOrEmpty(providerName) || String.IsNullOrEmpty(providerUserId)) {
                Log.Error().Message("Remove external login failed: Invalid Provider Name or Provider User Id.").Tag("External Login", providerName).Property("User", ExceptionlessUser).Property("Provider User Id", providerUserId).Write();
                return BadRequest("Invalid Provider Name or Provider User Id.");
            }

            if (ExceptionlessUser.OAuthAccounts.Count <= 1 && String.IsNullOrEmpty(ExceptionlessUser.Password)) {
                Log.Error().Message("Remove external login failed: You must set a local password before removing your external login.").Tag("External Login", providerName).Property("User", ExceptionlessUser).Property("Provider User Id", providerUserId).Write();
                return BadRequest("You must set a local password before removing your external login.");
            }

            if (ExceptionlessUser.RemoveOAuthAccount(providerName, providerUserId))
                _userRepository.Save(ExceptionlessUser);

            Log.Info().Message("Remove External Login").Tag("External Login", providerName).Property("User", ExceptionlessUser).Write();
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
        public IHttpActionResult ChangePassword(ChangePasswordModel model) {
            if (model == null || !IsValidPassword(model.Password)) {
                Log.Error().Message("Change password failed: The New Password must be at least 6 characters long.").Tag("Change Password").Property("User", ExceptionlessUser).Property("Password Length", model != null && model.Password != null ? model.Password.Length : 0).Write();
                return BadRequest("The New Password must be at least 6 characters long.");
            }

            // User has a local account..
            if (!String.IsNullOrWhiteSpace(ExceptionlessUser.Password)) {
                if (String.IsNullOrWhiteSpace(model.CurrentPassword)) {
                    Log.Error().Message("Change password failed: The current password is incorrect.").Tag("Change Password").Property("User", ExceptionlessUser).Write();
                    return BadRequest("The current password is incorrect.");
                }

                string encodedPassword = model.CurrentPassword.ToSaltedHash(ExceptionlessUser.Salt);
                if (!String.Equals(encodedPassword, ExceptionlessUser.Password)) {
                    Log.Error().Message("Change password failed: The current password is incorrect.").Tag("Change Password").Property("User", ExceptionlessUser).Write();
                    return BadRequest("The current password is incorrect.");
                }
            }

            ChangePassword(ExceptionlessUser, model.Password);

            Log.Info().Message("Change Password").Property("User", ExceptionlessUser).Write();
            return Ok();
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet]
        [Route("check-email-address/{email:minlength(1)}")]
        public IHttpActionResult IsEmailAddressAvailable(string email) {
            if (String.IsNullOrWhiteSpace(email))
                return StatusCode(HttpStatusCode.NoContent);

            if (ExceptionlessUser != null && String.Equals(ExceptionlessUser.EmailAddress, email, StringComparison.OrdinalIgnoreCase))
                return StatusCode(HttpStatusCode.Created);

            if (_userRepository.GetByEmailAddress(email) == null)
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
        public IHttpActionResult ForgotPassword(string email) {
            if (String.IsNullOrWhiteSpace(email)) {
                Log.Error().Message("Forgot password failed: Please specify a valid Email Address.").Tag("Forgot Password").Write();
                return BadRequest("Please specify a valid Email Address.");
            }

            var user = _userRepository.GetByEmailAddress(email);
            if (user == null) {
                Log.Error().Message("Forgot password failed: No user was found with \"{0}\" Email Address.", email).Tag("Forgot Password").Property("Email Address", email).Write();
                return BadRequest("No user was found with this Email Address.");
            }

            user.CreatePasswordResetToken();
            _userRepository.Save(user);

            _mailer.SendPasswordReset(user);

            Log.Info().Message("Forgot Password").Property("User", user).Write();
            return Ok();
        }

        /// <summary>
        /// Reset password
        /// </summary>
        /// <param name="model">The reset password model.</param>
        /// <response code="400">Invalid reset password model.</response>
        [HttpPost]
        [Route("reset-password")]
        public IHttpActionResult ResetPassword(ResetPasswordModel model) {
            if (model == null || String.IsNullOrEmpty(model.PasswordResetToken)) {
                Log.Error().Message("Reset password failed: Invalid Password Reset Token.").Tag("Reset Password").Write();
                return BadRequest("Invalid Password Reset Token.");
            }

            var user = _userRepository.GetByPasswordResetToken(model.PasswordResetToken);
            if (user == null) {
                Log.Error().Message("Reset password failed: Invalid Password Reset Token.").Tag("Reset Password").Write();
                return BadRequest("Invalid Password Reset Token.");
            }

            if (!user.HasValidPasswordResetTokenExpiration()) {
                Log.Error().Message("Reset password failed: Verify Email Address Token has expired.").Tag("Reset Password").Property("User", user).Write();
                return BadRequest("Verify Email Address Token has expired.");
            }

            if (!IsValidPassword(model.Password)) {
                Log.Error().Message("Reset password failed: The New Password must be at least 6 characters long.").Tag("Reset Password").Property("User", user).Property("Password Length", model.Password != null ? model.Password.Length : 0).Write();
                return BadRequest("The New Password must be at least 6 characters long.");
            }

            user.MarkEmailAddressVerified();
            ChangePassword(user, model.Password);

            Log.Info().Message("Reset Password").Property("User", user).Write();
            return Ok();
        }

        /// <summary>
        /// Cancel reset password
        /// </summary>
        /// <param name="token">The password reset token.</param>
        /// <response code="400">Invalid password reset token.</response>
        [HttpPost]
        [Route("cancel-reset-password/{token:minlength(1)}")]
        public IHttpActionResult CancelResetPassword(string token) {
            if (String.IsNullOrEmpty(token)) {
                Log.Error().Message("Cancel reset password failed: Invalid Password Reset Token.").Tag("Reset Password").Write();
                return BadRequest("Invalid password reset token.");
            }

            var user = _userRepository.GetByPasswordResetToken(token);
            if (user == null)
                return Ok();
            
            user.ResetPasswordResetToken();
            _userRepository.Save(user);

            Log.Info().Message("Cancel Reset Password").Property("User", user).Write();
            return Ok();
        }

        private void AddGlobalAdminRoleIfFirstUser(User user) {
            if (_isFirstUserChecked)
                return;

            bool isFirstUser = _userRepository.Count() == 0;
            if (isFirstUser)
                user.Roles.Add(AuthorizationRoles.GlobalAdmin);

            _isFirstUserChecked = true;
        }

        private User AddExternalLogin(UserInfo userInfo) {
            User existingUser = _userRepository.GetUserByOAuthProvider(userInfo.ProviderName, userInfo.Id);

            // Link user accounts.
            if (ExceptionlessUser != null) {
                if (existingUser != null) {
                    if (existingUser.Id != ExceptionlessUser.Id) {
                        // Existing user account is not the current user. Remove it and we'll add it to the current user below.
                        if (!existingUser.RemoveOAuthAccount(userInfo.ProviderName, userInfo.Id))
                            return null;

                        _userRepository.Save(existingUser);
                    } else {
                        // User is already logged in.
                        return ExceptionlessUser;
                    }
                }

                // Add it to the current user if it doesn't already exist and save it.
                ExceptionlessUser.AddOAuthAccount(userInfo.ProviderName, userInfo.Id, userInfo.Email);
                _userRepository.Save(ExceptionlessUser);
                return ExceptionlessUser;
            }

            // Create a new user account or return an existing one.
            if (existingUser != null) {
                if (!existingUser.IsEmailAddressVerified) {
                    existingUser.MarkEmailAddressVerified();
                    _userRepository.Save(existingUser);
                }

                return existingUser;
            }

            // Check to see if a user already exists with this email address.
            User user = !String.IsNullOrEmpty(userInfo.Email) ? _userRepository.GetByEmailAddress(userInfo.Email) : null;
            if (user == null) {
                if (!Settings.Current.EnableAccountCreation)
                    throw new ApplicationException("Account Creation is currently disabled.");

                user = new User { FullName = userInfo.GetFullName(), EmailAddress = userInfo.Email };
                user.Roles.Add(AuthorizationRoles.Client);
                user.Roles.Add(AuthorizationRoles.User);
                AddGlobalAdminRoleIfFirstUser(user);
            }

            user.MarkEmailAddressVerified();
            user.AddOAuthAccount(userInfo.ProviderName, userInfo.Id, userInfo.Email);
            _userRepository.Save(user);

            return user;
        }

        private void AddInvitedUserToOrganization(string token, User user) {
            if (String.IsNullOrWhiteSpace(token) || user == null)
                return;

            Invite invite;
            var organization = _organizationRepository.GetByInviteToken(token, out invite);
            if (organization == null) {
                Log.Info().Message("Unable to find organization with the invite token: {0}", token).Write();
                return;
            }

            if (!user.IsEmailAddressVerified && String.Equals(user.EmailAddress, invite.EmailAddress, StringComparison.OrdinalIgnoreCase)) {
                Log.Info().Message("Marking the invited users email address \"{0}\" as verified", user.EmailAddress).Write();
                user.MarkEmailAddressVerified();
                _userRepository.Save(user);
            }

            if (!user.OrganizationIds.Contains(organization.Id)) {
                Log.Info().Message("Joined From Invite").Tag("Invite").Property("Organization", organization).Property("User", user).Write();
                user.OrganizationIds.Add(organization.Id);
                _userRepository.Save(user);
            }

            organization.Invites.Remove(invite);
            _organizationRepository.Save(organization);
        }

        private void ChangePassword(User user, string password) {
            if (String.IsNullOrEmpty(user.Salt))
                user.Salt = Core.Extensions.StringExtensions.GetNewToken();

            user.Password = password.ToSaltedHash(user.Salt);
            user.ResetPasswordResetToken();
            _userRepository.Save(user);
        }


        private string GetToken(User user) {
            var token = _tokenManager.Create(user);
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