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
                Log.Error().Message("Login failed: Email Address is required.").Tag("Login").ContextProperty("HttpActionContext", ActionContext).Write();
                return BadRequest("Email Address is required.");
            }

            if (String.IsNullOrWhiteSpace(model.Password)) {
                Log.Error().Message("Login failed for \"{0}\": Password is required.", model.Email).Tag("Login").Identity(model.Email).ContextProperty("HttpActionContext", ActionContext).Write();
                return BadRequest("Password is required.");
            }

            User user;
            try {
                user = _userRepository.GetByEmailAddress(model.Email);
            } catch (Exception ex) {
                Log.Error().Exception(ex).Critical().Message("Login failed for \"{0}\": {1}", model.Email, ex.Message).Tag("Login").Identity(model.Email).ContextProperty("HttpActionContext", ActionContext).Write();
                return Unauthorized();
            }

            if (user == null) {
                Log.Error().Message("Login failed for \"{0}\": User not found.", model.Email).Tag("Login").Identity(model.Email).ContextProperty("HttpActionContext", ActionContext).Write();
                return Unauthorized();
            }
            
            if (!user.IsActive) {
                Log.Error().Message("Login failed for \"{0}\": The user is inactive.", user.EmailAddress).Tag("Login").Identity(user.EmailAddress).Property("User", user).ContextProperty("HttpActionContext", ActionContext).Write();
                return Unauthorized();
            }

            if (String.IsNullOrEmpty(user.Salt)) {
                Log.Error().Message("Login failed for \"{0}\": The user has no salt defined.", user.EmailAddress).Tag("Login").Identity(user.EmailAddress).Property("User", user).ContextProperty("HttpActionContext", ActionContext).Write();
                return Unauthorized();
            }

            string encodedPassword = model.Password.ToSaltedHash(user.Salt);
            if (!String.Equals(encodedPassword, user.Password)) {
                Log.Error().Message("Login failed for \"{0}\": Invalid Password.", user.EmailAddress).Tag("Login").Identity(user.EmailAddress).Property("User", user).ContextProperty("HttpActionContext", ActionContext).Write();
                return Unauthorized();
            }

            if (!String.IsNullOrEmpty(model.InviteToken))
                AddInvitedUserToOrganization(model.InviteToken, user);

            Log.Info().Message("\"{0}\" logged in.", user.EmailAddress).Tag("Login").Identity(user.EmailAddress).Property("User", user).ContextProperty("HttpActionContext", ActionContext).Write();
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
                Log.Error().Message("Signup failed: Email Address is required.").Tag("Signup").Property("Name", model != null ? model.Name : "<null>").ContextProperty("HttpActionContext", ActionContext).Write();
                return BadRequest("Email Address is required.");
            }

            if (String.IsNullOrWhiteSpace(model.Name)) {
                Log.Error().Message("Signup failed for \"{0}\": Name is required.", model.Email).Tag("Signup").Identity(model.Email).ContextProperty("HttpActionContext", ActionContext).Write();
                return BadRequest("Name is required.");
            }

            if (!IsValidPassword(model.Password)) {
                Log.Error().Message("Signup failed for \"{0}\": Invalid Password", model.Email).Tag("Signup").Identity(model.Email).Property("Password Length", model.Password != null ? model.Password.Length : 0).ContextProperty("HttpActionContext", ActionContext).Write();
                return BadRequest("Password must be at least 6 characters long.");
            }

            User user;
            try {
                user = _userRepository.GetByEmailAddress(model.Email);
            } catch (Exception ex) {
                Log.Error().Exception(ex).Critical().Message("Signup failed for \"{0}\": {1}", model.Email, ex.Message).Tag("Signup").Identity(model.Email).ContextProperty("HttpActionContext", ActionContext).Write();
                return BadRequest();
            }

            if (user != null) {
                Log.Error().Message("Signup failed for \"{0}\": A user already exists.", user.EmailAddress).Tag("Signup").Identity(user.EmailAddress).ContextProperty("HttpActionContext", ActionContext).Write();
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
                Log.Error().Critical().Message("Signup failed for \"{0}\": {1}", model.Email, errors).Tag("Signup").Identity(user.EmailAddress).Property("User", user).ContextProperty("HttpActionContext", ActionContext).Write();
                return BadRequest(errors);
            } catch (Exception ex) {
                Log.Error().Exception(ex).Critical().Message("Signup failed for \"{0}\": {1}", model.Email, ex.Message).Tag("Signup").Identity(user.EmailAddress).Property("User", user).ContextProperty("HttpActionContext", ActionContext).Write();
                return BadRequest("An error occurred.");
            }

            if (!String.IsNullOrEmpty(model.InviteToken))
                AddInvitedUserToOrganization(model.InviteToken, user);

            if (!user.IsEmailAddressVerified)
                _mailer.SendVerifyEmail(user);

            Log.Info().Message("\"{0}\" signed up.", user.EmailAddress).Tag("Signup").Identity(user.EmailAddress).Property("User", user).ContextProperty("HttpActionContext", ActionContext).Write();
            return Ok(new TokenResult { Token = GetToken(user) });
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost]
        [Route("github")]
        [ResponseType(typeof(TokenResult))]
        public IHttpActionResult GitHub(JObject value) {
            return ExternalLogin(value.ToObject<ExternalAuthInfo>(), Settings.Current.GitHubAppId, Settings.Current.GitHubAppSecret, (f, c) => new GitHubWithPrivateEmailsClient(f, c));
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost]
        [Route("google")]
        [ResponseType(typeof(TokenResult))]
        public IHttpActionResult Google(JObject value) {
            return ExternalLogin(value.ToObject<ExternalAuthInfo>(), Settings.Current.GoogleAppId, Settings.Current.GoogleAppSecret, (f, c) => new GoogleClient(f, c));
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost]
        [Route("facebook")]
        [ResponseType(typeof(TokenResult))]
        public IHttpActionResult Facebook(JObject value) {
            return ExternalLogin(value.ToObject<ExternalAuthInfo>(), Settings.Current.FacebookAppId, Settings.Current.FacebookAppSecret, (f, c) => new FacebookClient(f, c));
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost]
        [Route("live")]
        [ResponseType(typeof(TokenResult))]
        public IHttpActionResult Live(JObject value) {
            return ExternalLogin(value.ToObject<ExternalAuthInfo>(), Settings.Current.MicrosoftAppId, Settings.Current.MicrosoftAppSecret, (f, c) => new WindowsLiveClient(f, c));
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost]
        [Route("unlink/{providerName:minlength(1)}")]
        [Authorize(Roles = AuthorizationRoles.User)]
        public IHttpActionResult RemoveExternalLogin(string providerName, [NakedBody] string providerUserId) {
            if (String.IsNullOrEmpty(providerName) || String.IsNullOrEmpty(providerUserId)) {
                Log.Error().Message("Remove external login failed for \"{0}\": Invalid Provider Name or Provider User Id.", ExceptionlessUser.EmailAddress).Tag("External Login", providerName).Identity(ExceptionlessUser.EmailAddress).Property("User", ExceptionlessUser).Property("Provider User Id", providerUserId).ContextProperty("HttpActionContext", ActionContext).Write();
                return BadRequest("Invalid Provider Name or Provider User Id.");
            }

            if (ExceptionlessUser.OAuthAccounts.Count <= 1 && String.IsNullOrEmpty(ExceptionlessUser.Password)) {
                Log.Error().Message("Remove external login failed for \"{0}\": You must set a local password before removing your external login.", ExceptionlessUser.EmailAddress).Tag("External Login", providerName).Identity(ExceptionlessUser.EmailAddress).Property("User", ExceptionlessUser).Property("Provider User Id", providerUserId).ContextProperty("HttpActionContext", ActionContext).Write();
                return BadRequest("You must set a local password before removing your external login.");
            }

            if (ExceptionlessUser.RemoveOAuthAccount(providerName, providerUserId))
                _userRepository.Save(ExceptionlessUser);

            Log.Info().Message("\"{0}\" removed an external login: \"{1}\"", ExceptionlessUser.EmailAddress, providerName).Tag("External Login", providerName).Identity(ExceptionlessUser.EmailAddress).Property("User", ExceptionlessUser).ContextProperty("HttpActionContext", ActionContext).Write();
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
                Log.Error().Message("Change password failed for \"{0}\": The New Password must be at least 6 characters long.", ExceptionlessUser.EmailAddress).Tag("Change Password").Identity(ExceptionlessUser.EmailAddress).Property("User", ExceptionlessUser).Property("Password Length", model != null && model.Password != null ? model.Password.Length : 0).ContextProperty("HttpActionContext", ActionContext).Write();
                return BadRequest("The New Password must be at least 6 characters long.");
            }

            // User has a local account..
            if (!String.IsNullOrWhiteSpace(ExceptionlessUser.Password)) {
                if (String.IsNullOrWhiteSpace(model.CurrentPassword)) {
                    Log.Error().Message("Change password failed for \"{0}\": The current password is incorrect.", ExceptionlessUser.EmailAddress).Tag("Change Password").Identity(ExceptionlessUser.EmailAddress).Property("User", ExceptionlessUser).ContextProperty("HttpActionContext", ActionContext).Write();
                    return BadRequest("The current password is incorrect.");
                }

                string encodedPassword = model.CurrentPassword.ToSaltedHash(ExceptionlessUser.Salt);
                if (!String.Equals(encodedPassword, ExceptionlessUser.Password)) {
                    Log.Error().Message("Change password failed for \"{0}\": The current password is incorrect.", ExceptionlessUser.EmailAddress).Tag("Change Password").Identity(ExceptionlessUser.EmailAddress).Property("User", ExceptionlessUser).ContextProperty("HttpActionContext", ActionContext).Write();
                    return BadRequest("The current password is incorrect.");
                }
            }

            ChangePassword(ExceptionlessUser, model.Password);

            Log.Info().Message("\"{0}\" changed their password.", ExceptionlessUser.EmailAddress).Identity(ExceptionlessUser.EmailAddress).Property("User", ExceptionlessUser).ContextProperty("HttpActionContext", ActionContext).Write();
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
                Log.Error().Message("Forgot password failed: Please specify a valid Email Address.").Tag("Forgot Password").ContextProperty("HttpActionContext", ActionContext).Write();
                return BadRequest("Please specify a valid Email Address.");
            }

            var user = _userRepository.GetByEmailAddress(email);
            if (user == null) {
                Log.Error().Message("Forgot password failed for \"{0}\": No user was found.", email).Tag("Forgot Password").Identity(email).Property("Email Address", email).ContextProperty("HttpActionContext", ActionContext).Write();
                return BadRequest("No user was found with this Email Address.");
            }

            user.CreatePasswordResetToken();
            _userRepository.Save(user);

            _mailer.SendPasswordReset(user);

            Log.Info().Message("\"{0}\" forgot their password.", user.EmailAddress).Identity(user.EmailAddress).Property("User", user).ContextProperty("HttpActionContext", ActionContext).Write();
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
                Log.Error().Message("Reset password failed: Invalid Password Reset Token.").Tag("Reset Password").ContextProperty("HttpActionContext", ActionContext).Write();
                return BadRequest("Invalid Password Reset Token.");
            }

            var user = _userRepository.GetByPasswordResetToken(model.PasswordResetToken);
            if (user == null) {
                Log.Error().Message("Reset password failed: Invalid Password Reset Token.").Tag("Reset Password").ContextProperty("HttpActionContext", ActionContext).Write();
                return BadRequest("Invalid Password Reset Token.");
            }

            if (!user.HasValidPasswordResetTokenExpiration()) {
                Log.Error().Message("Reset password failed for \"{0}\": Password Reset Token has expired.", user.EmailAddress).Tag("Reset Password").Identity(user.EmailAddress).Property("User", user).ContextProperty("HttpActionContext", ActionContext).Write();
                return BadRequest("Password Reset Token has expired.");
            }

            if (!IsValidPassword(model.Password)) {
                Log.Error().Message("Reset password failed for \"{0}\": The New Password must be at least 6 characters long.", user.EmailAddress).Tag("Reset Password").Identity(user.EmailAddress).Property("User", user).Property("Password Length", model.Password != null ? model.Password.Length : 0).ContextProperty("HttpActionContext", ActionContext).Write();
                return BadRequest("The New Password must be at least 6 characters long.");
            }

            user.MarkEmailAddressVerified();
            ChangePassword(user, model.Password);

            Log.Info().Message("\"{0}\" reset their password.", user.EmailAddress).Identity(user.EmailAddress).Property("User", user).ContextProperty("HttpActionContext", ActionContext).Write();
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
                Log.Error().Message("Cancel reset password failed: Invalid Password Reset Token.").Tag("Reset Password").ContextProperty("HttpActionContext", ActionContext).Write();
                return BadRequest("Invalid password reset token.");
            }

            var user = _userRepository.GetByPasswordResetToken(token);
            if (user == null)
                return Ok();
            
            user.ResetPasswordResetToken();
            _userRepository.Save(user);

            Log.Info().Message("\"{0}\" canceled the reset password", user.EmailAddress).Identity(user.EmailAddress).Property("User", user).ContextProperty("HttpActionContext", ActionContext).Write();
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
        
        private IHttpActionResult ExternalLogin<TClient>(ExternalAuthInfo authInfo, string appId, string appSecret, Func<IRequestFactory, IClientConfiguration, TClient> createClient) where TClient : OAuth2Client {
            if (authInfo == null || String.IsNullOrEmpty(authInfo.Code)) {
                Log.Error().Message("External login failed: Unable to get auth info.").Tag("External Login").Property("Auth Info", authInfo).ContextProperty("HttpActionContext", ActionContext).Write();
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
                Log.Error().Exception(ex).Critical().Message("External login failed: {0}", ex.Message).Tag("External Login", client.Name).Property("Auth Info", authInfo).ContextProperty("HttpActionContext", ActionContext).Write();
                return BadRequest("Unable to get user info.");
            }

            User user;
            try {
                user = FromExternalLogin(userInfo);
            } catch (ApplicationException) {
                return BadRequest("Account Creation is currently disabled.");
            } catch (Exception ex) {
                Log.Error().Exception(ex).Critical().Message("External login failed for \"{0}\": {1}", userInfo.Email, ex.Message).Tag("External Login", client.Name).Property("User Info", userInfo).Property("Auth Info", authInfo).ContextProperty("HttpActionContext", ActionContext).Write();
                return BadRequest("An error occurred while processing user info.");
            }

            if (user == null) {
                Log.Error().Critical().Message("External login failed for \"{0}\": Unable to process user info.", userInfo.Email).Tag("External Login", client.Name).Property("User Info", userInfo).Property("Auth Info", authInfo).ContextProperty("HttpActionContext", ActionContext).Write();
                return BadRequest("Unable to process user info.");
            }

            if (!String.IsNullOrWhiteSpace(authInfo.InviteToken))
                AddInvitedUserToOrganization(authInfo.InviteToken, user);

            Log.Info().Message("\"{0}\" logged in.", user.EmailAddress).Tag("External Login", client.Name).Identity(user.EmailAddress).Property("User", user).ContextProperty("HttpActionContext", ActionContext).Write();
            return Ok(new TokenResult { Token = GetToken(user) });
        }

        private User FromExternalLogin(UserInfo userInfo) {
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
                Log.Info().Message("Unable to add the invited user \"{0}\". Invalid invite token: {1}", user.EmailAddress, token).Identity(user.EmailAddress).Property("User", user).ContextProperty("HttpActionContext", ActionContext).Write();
                return;
            }

            if (!user.IsEmailAddressVerified && String.Equals(user.EmailAddress, invite.EmailAddress, StringComparison.OrdinalIgnoreCase)) {
                Log.Info().Message("Marking the invited users email address \"{0}\" as verified.", user.EmailAddress).Identity(user.EmailAddress).Property("User", user).ContextProperty("HttpActionContext", ActionContext).Write();
                user.MarkEmailAddressVerified();
                _userRepository.Save(user);
            }

            if (!user.OrganizationIds.Contains(organization.Id)) {
                Log.Info().Message("\"{0}\" joined from invite.", user.EmailAddress).Tag("Invite").Property("Organization", organization).Identity(user.EmailAddress).Property("User", user).ContextProperty("HttpActionContext", ActionContext).Write();
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
            var token = _tokenManager.GetOrCreate(user);
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