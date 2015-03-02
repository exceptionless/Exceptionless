using System;
using System.Net;
using System.Web.Http;
using Exceptionless.Api.Extensions;
using Exceptionless.Api.Models;
using Exceptionless.Api.Security;
using Exceptionless.Api.Utility;
using Exceptionless.Core;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.Core.Models;
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
        private readonly DataHelper _dataHelper;

        private static bool _isFirstUserChecked;

        public AuthController(IOrganizationRepository organizationRepository, IUserRepository userRepository, IMailer mailer, TokenManager tokenManager, DataHelper dataHelper) {
            _organizationRepository = organizationRepository;
            _userRepository = userRepository;
            _mailer = mailer;
            _tokenManager = tokenManager;
            _dataHelper = dataHelper;
        }

        [HttpPost]
        [Route("login")]
        public IHttpActionResult Login(LoginModel model) {
            if (model == null || String.IsNullOrWhiteSpace(model.Email))
                return BadRequest("Email Address is required.");

            if (String.IsNullOrWhiteSpace(model.Password))
                return BadRequest("Password is required.");

            User user;
            try {
                user = _userRepository.GetByEmailAddress(model.Email);
            } catch (Exception) {
                return Unauthorized();
            }

            if (user == null || !user.IsActive)
                return Unauthorized();

            if (String.IsNullOrEmpty(user.Salt))
                return Unauthorized();

            string encodedPassword = model.Password.ToSaltedHash(user.Salt);
            if (!String.Equals(encodedPassword, user.Password)) {
                //_exceptionless.CreateFeatureUsage("Invalid Password").AddTags("Login").SetProperty("Email Address", model.Email).SetProperty("Password Length", model.Password != null ? model.Password.Length : 0).Submit();
                return Unauthorized();
            }

            if (!String.IsNullOrEmpty(model.InviteToken))
				AddInvitedUserToOrganization(model.InviteToken, user);

            //_exceptionless.CreateFeatureUsage("Login").AddObject(user).Submit();
            return Ok(new { Token = GetToken(user) });
        }

        [HttpPost]
        [Route("signup")]
        public IHttpActionResult Signup(SignupModel model) {
            if (!Settings.Current.EnableAccountCreation) 
                return BadRequest("Account Creation is currently disabled.");

            if (model == null || String.IsNullOrWhiteSpace(model.Email))
                return BadRequest("Email Address is required.");

            if (String.IsNullOrWhiteSpace(model.Name))
                return BadRequest("Name is required.");

            if (!IsValidPassword(model.Password)) {
                //_exceptionless.CreateFeatureUsage("Invalid Password").AddTags("Signup").SetProperty("Email Address", model.Email).SetProperty("Password Length", model.Password != null ? model.Password.Length : 0).Submit();
                return BadRequest("Password must be at least 6 characters long.");
            }

            User user;
            try {
                user = _userRepository.GetByEmailAddress(model.Email);
            } catch (Exception) {
                return BadRequest();
            }

            if (user != null)
                return BadRequest("A user already exists with this email address.");

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
                return BadRequest(String.Join(", ", ex.Errors));
            } catch (Exception ex) {
                Log.Error().Exception(ex).Write();
                //ex.ToExceptionless().MarkAsCritical().AddTags("Signup").AddObject(user).AddObject(model).Submit();
                return BadRequest("An error occurred.");
            }

            if (!String.IsNullOrEmpty(model.InviteToken))
                AddInvitedUserToOrganization(model.InviteToken, user);

            if (!user.IsEmailAddressVerified)
                _mailer.SendVerifyEmail(user);

            //_exceptionless.CreateFeatureUsage("Signup").AddObject(user).Submit();
            return Ok(new { Token = GetToken(user) });
        }

        [HttpPost]
        [Route("github")]
        public IHttpActionResult Github(JObject value) {
            var authInfo = value.ToObject<ExternalAuthInfo>();
            if (authInfo == null || String.IsNullOrEmpty(authInfo.Code))
                return NotFound();

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
                Log.Error().Exception(ex).Write();
                //ex.ToExceptionless().MarkAsCritical().AddTags("External Login", "GitHub").AddObject(authInfo).Submit();
                return BadRequest("Unable to get user info.");
            }

            User user;
            try {
                user = AddExternalLogin(userInfo);
            } catch (ApplicationException) {
                return BadRequest("Account Creation is currently disabled.");
            } catch (Exception ex) {
                Log.Error().Exception(ex).Write();
                //ex.ToExceptionless().MarkAsCritical().AddTags("External Login", "GitHub").AddObject(authInfo).AddObject(userInfo).Submit();
                return BadRequest("An error occurred while processing user info.");
            }

            if (user == null) {
                //_exceptionless.CreateLog(typeof(AuthController).Name, "Unable to process user info.", "Error").AddTags("External Login", "GitHub").AddObject(authInfo).AddObject(userInfo).Submit();
                return BadRequest("Unable to process user info.");
            }

            if (!String.IsNullOrWhiteSpace(authInfo.InviteToken))
                AddInvitedUserToOrganization(authInfo.InviteToken, user);

            return Ok(new { Token = GetToken(user) });
        }

        [HttpPost]
        [Route("google")]
        public IHttpActionResult Google(JObject value) {
            var authInfo = value.ToObject<ExternalAuthInfo>();
            if (authInfo == null || String.IsNullOrEmpty(authInfo.Code))
                return NotFound();

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
                Log.Error().Exception(ex).Write();
                //ex.ToExceptionless().MarkAsCritical().AddTags("External Login", "Google").AddObject(authInfo).Submit();
                return BadRequest("Unable to get user info.");
            }

            User user;
            try {
                user = AddExternalLogin(userInfo);
            } catch (ApplicationException) {
                return BadRequest("Account Creation is currently disabled.");
            } catch (Exception ex) {
                Log.Error().Exception(ex).Write();
                //ex.ToExceptionless().MarkAsCritical().AddTags("External Login", "Google").AddObject(authInfo).AddObject(userInfo).Submit();
                return BadRequest("An error occurred while processing user info.");
            }

            if (user == null) {
                //_exceptionless.CreateLog(typeof(AuthController).Name, "Unable to process user info.", "Error").AddTags("External Login", "Google").AddObject(authInfo).AddObject(userInfo).Submit();
                return BadRequest("Unable to process user info.");
            }

            if (!String.IsNullOrWhiteSpace(authInfo.InviteToken))
                AddInvitedUserToOrganization(authInfo.InviteToken, user);

            return Ok(new { Token = GetToken(user) });
        }

        [HttpPost]
        [Route("facebook")]
        public IHttpActionResult Facebook(JObject value) {
            var authInfo = value.ToObject<ExternalAuthInfo>();
            if (authInfo == null || String.IsNullOrEmpty(authInfo.Code))
                return NotFound();

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
                Log.Error().Exception(ex).Write();
                //ex.ToExceptionless().MarkAsCritical().AddTags("External Login", "Facebook").AddObject(authInfo).Submit();
                return BadRequest("Unable to get user info.");
            }

            User user;
            try {
                user = AddExternalLogin(userInfo);
            } catch (ApplicationException) {
                return BadRequest("Account Creation is currently disabled.");
            } catch (Exception ex) {
                Log.Error().Exception(ex).Write();
                //ex.ToExceptionless().MarkAsCritical().AddTags("External Login", "Facebook").AddObject(authInfo).AddObject(userInfo).Submit();
                return BadRequest("An error occurred while processing user info.");
            }

            if (user == null) {
                //_exceptionless.CreateLog(typeof(AuthController).Name, "Unable to process user info.", "Error").AddTags("External Login", "Facebook").AddObject(authInfo).AddObject(userInfo).Submit();
                return BadRequest("Unable to process user info.");
            }

            if (!String.IsNullOrWhiteSpace(authInfo.InviteToken))
                AddInvitedUserToOrganization(authInfo.InviteToken, user);

            return Ok(new { Token = GetToken(user) });
        }

        [HttpPost]
        [Route("live")]
        public IHttpActionResult Live(JObject value) {
            var authInfo = value.ToObject<ExternalAuthInfo>();
            if (authInfo == null || String.IsNullOrEmpty(authInfo.Code))
                return NotFound();

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
                Log.Error().Exception(ex).Write();
                //ex.ToExceptionless().MarkAsCritical().AddTags("External Login", "WindowsLive").AddObject(authInfo).Submit();
                return BadRequest("Unable to get user info.");
            }

            User user;
            try {
                user = AddExternalLogin(userInfo);
            } catch (ApplicationException) {
                return BadRequest("Account Creation is currently disabled.");
            } catch (Exception ex) {
                Log.Error().Exception(ex).Write();
                //ex.ToExceptionless().MarkAsCritical().AddTags("External Login", "WindowsLive").AddObject(authInfo).AddObject(userInfo).Submit();
                return BadRequest("An error occurred while processing user info.");
            }

            if (user == null) {
                //_exceptionless.CreateLog(typeof(AuthController).Name, "Unable to process user info.", "Error").AddTags("External Login", "WindowsLive").AddObject(authInfo).AddObject(userInfo).Submit();
                return BadRequest("Unable to process user info.");
            }

            if (!String.IsNullOrWhiteSpace(authInfo.InviteToken))
                AddInvitedUserToOrganization(authInfo.InviteToken, user);

            return Ok(new { Token = GetToken(user) });
        }

        [HttpPost]
        [Route("unlink/{providerName:minlength(1)}")]
        [Authorize(Roles = AuthorizationRoles.User)]
        public IHttpActionResult RemoveExternalLogin(string providerName, [NakedBody] string providerUserId) {
            if (String.IsNullOrEmpty(providerName) || String.IsNullOrEmpty(providerUserId))
                return BadRequest("Invalid Provider Name or Provider User Id.");

            if (ExceptionlessUser.OAuthAccounts.Count <= 1 && String.IsNullOrEmpty(ExceptionlessUser.Password))
                return BadRequest("You must set a local password before removing your external login.");

            if (ExceptionlessUser.RemoveOAuthAccount(providerName, providerUserId))
                _userRepository.Save(ExceptionlessUser);

            //_exceptionless.CreateFeatureUsage("Remove External Login").AddTags(providerName).AddObject(ExceptionlessUser).Submit();
            return Ok();
        }

        [HttpPost]
        [Route("change-password")]
        [Authorize(Roles = AuthorizationRoles.User)]
        public IHttpActionResult ChangePassword(ChangePasswordModel model) {
            if (model == null || !IsValidPassword(model.Password))
                return BadRequest("The New Password must be at least 6 characters long.");

            // User has a local account..
            if (!String.IsNullOrWhiteSpace(ExceptionlessUser.Password)) {
                if (String.IsNullOrWhiteSpace(model.CurrentPassword))
                    return BadRequest("The current password is incorrect.");

                string encodedPassword = model.CurrentPassword.ToSaltedHash(ExceptionlessUser.Salt);
                if (!String.Equals(encodedPassword, ExceptionlessUser.Password))
                    return BadRequest("The current password is incorrect.");
            }

            ChangePassword(ExceptionlessUser, model.Password);

            //_exceptionless.CreateFeatureUsage("Change Password").AddObject(ExceptionlessUser).Submit();
            return Ok();
        }

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

        [HttpGet]
        [Route("forgot-password/{email:minlength(1)}")]
        public IHttpActionResult ForgotPassword(string email) {
            if (String.IsNullOrWhiteSpace(email))
                return BadRequest("Please specify a valid Email Address.");

            var user = _userRepository.GetByEmailAddress(email);
            if (user == null)
                return BadRequest("No user was found with this Email Address.");

            user.PasswordResetToken = Core.Extensions.StringExtensions.GetNewToken();
            user.PasswordResetTokenExpiration = DateTime.Now.AddMinutes(1440);
            _userRepository.Save(user);

            _mailer.SendPasswordReset(user);

            //_exceptionless.CreateFeatureUsage("Forgot Password").AddObject(user).Submit();
            return Ok();
        }

        [HttpPost]
        [Route("reset-password")]
        public IHttpActionResult ResetPassword(ResetPasswordModel model) {
            if (model == null || String.IsNullOrEmpty(model.PasswordResetToken))
                return BadRequest("Invalid Password Reset Token.");

            var user = _userRepository.GetByPasswordResetToken(model.PasswordResetToken);
            if (user == null)
                return BadRequest("Invalid Password Reset Token.");

            if (!user.HasValidEmailAddressTokenExpiration())
                return BadRequest("Verify Email Address Token has expired.");

            if (!IsValidPassword(model.Password))
                return BadRequest("The New Password must be at least 6 characters long.");

            user.MarkEmailAddressVerified();
            ChangePassword(user, model.Password);

            //_exceptionless.CreateFeatureUsage("Reset Password").AddObject(user).Submit();
            return Ok();
        }

        [HttpPost]
        [Route("cancel-reset-password/{token:minlength(1)}")]
        public IHttpActionResult CancelResetPassword(string token) {
            if (String.IsNullOrEmpty(token))
                return BadRequest("Invalid Password Reset Token.");

            var user = _userRepository.GetByPasswordResetToken(token);
            if (user == null)
                return Ok();
            
            user.ResetPasswordResetToken();
            _userRepository.Save(user);

            //_exceptionless.CreateFeatureUsage("Cancel Reset Password").AddObject(user).Submit();
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
            //_exceptionless.CreateFeatureUsage("External Login").AddTags(userInfo.ProviderName).AddObject(userInfo).Submit();
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
                //_exceptionless.CreateFeatureUsage("Joined From Invite").AddObject(organization).AddObject(user).Submit();
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