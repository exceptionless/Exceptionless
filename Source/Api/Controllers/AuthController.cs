using System;
using System.Web.Http;
using Exceptionless.Api.Extensions;
using Exceptionless.Api.Models;
using Exceptionless.Api.Utility;
using Exceptionless.Core;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Messaging;
using Exceptionless.Core.Repositories;
using Exceptionless.Json.Linq;
using Exceptionless.Models;
using NLog.Fluent;
using OAuth2.Client.Impl;
using OAuth2.Configuration;
using OAuth2.Infrastructure;

namespace Exceptionless.Api.Controllers {
    [RoutePrefix(API_PREFIX + "/auth")]
    public class AuthController : ExceptionlessApiController {
        private readonly ITokenRepository _tokenRepository;
        private readonly IUserRepository _userRepository;
        private readonly MembershipSecurity _encoder = new MembershipSecurity();

        private static bool _isFirstUserChecked;

        public AuthController(ITokenRepository tokenRepository, IUserRepository userRepository) {
            _tokenRepository = tokenRepository;
            _userRepository = userRepository;
        }

        [HttpPost]
        [Route("login")]
        public IHttpActionResult Login(LoginModel model) {
            if (model == null || String.IsNullOrEmpty(model.Email) || String.IsNullOrEmpty(model.Password))
                return BadRequest("Email or Password was not specified.");

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

            string encodedPassword = _encoder.GetSaltedHash(model.Password, user.Salt);
            if (!String.Equals(encodedPassword, user.Password))
                return Unauthorized();

            ExceptionlessClient.Default.CreateFeatureUsage("Login").AddObject(user).Submit();
            return Ok(new { Token = GetToken(user) });
        }

        [HttpPost]
        [Route("signup")]
        public IHttpActionResult Signup(SignupModel model) {
            if (model == null || String.IsNullOrEmpty(model.Name) || String.IsNullOrEmpty(model.Email) || String.IsNullOrEmpty(model.Password))
                return BadRequest("Name, Email or Password was not specified.");

            User user;
            try {
                user = _userRepository.GetByEmailAddress(model.Email);
            } catch (Exception) {
                return BadRequest();
            }

            if (user != null)
                return BadRequest("A user already exists with this email address.");

            user = new User { EmailAddress = model.Email, Password = model.Password, FullName = model.Name };
            user.Roles.Add(AuthorizationRoles.User);
            AddGlobalAdminRoleIfFirstUser(user);

            user.Salt = _encoder.GenerateSalt();
            user.Password = _encoder.GetSaltedHash(user.Password, user.Salt);
            user.IsActive = true;

            try {
                user = _userRepository.Save(user, true);
            } catch (Exception ex) {
                ex.ToExceptionless().AddObject(user).MarkAsCritical().AddTags("signup").Submit();
                return BadRequest("An error occurred.");
            }

            ExceptionlessClient.Default.CreateFeatureUsage("Signup").AddObject(user).Submit();
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

            var client = new GitHubClient(new RequestFactory(), new RuntimeClientConfiguration {
                ClientId = Settings.Current.GitHubAppId,
                ClientSecret = Settings.Current.GitHubAppSecret,
                RedirectUri = authInfo.RedirectUri
            });

            OAuth2.Models.UserInfo userInfo;
            try {
                userInfo = client.GetUserInfo(authInfo.Code);
            } catch (Exception ex) {
                return BadRequest("Unable to get user info.");
            }

            User user;
            try {
                user = AddExternalLogin(userInfo);
            } catch (Exception ex) {
                return BadRequest("An error occurred while processing user info.");
            }

            if (user == null)
                return BadRequest("Unable to process user info.");

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

            OAuth2.Models.UserInfo userInfo;
            try {
                userInfo = client.GetUserInfo(authInfo.Code);
            } catch (Exception ex) {
                return BadRequest("Unable to get user info.");
            }

            User user;
            try {
                user = AddExternalLogin(userInfo);
            } catch (Exception ex) {
                return BadRequest("An error occurred while processing user info.");
            }

            if (user == null)
                return BadRequest("Unable to process user info.");

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

            OAuth2.Models.UserInfo userInfo;
            try {
                userInfo = client.GetUserInfo(authInfo.Code);
            } catch (Exception ex) {
                Log.Error().Exception(ex).Message("Unable to get user info.").Write();
                return BadRequest("Unable to get user info.");
            }

            User user;
            try {
                user = AddExternalLogin(userInfo);
            } catch (Exception ex) {
                Log.Error().Exception(ex).Message("Unable to get user info.").Write();
                return BadRequest("An error occurred while processing user info.");
            }

            if (user == null)
                return BadRequest("Unable to process user info.");

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

            OAuth2.Models.UserInfo userInfo;
            try {
                userInfo = client.GetUserInfo(authInfo.Code);
            } catch (Exception ex) {
                Log.Error().Exception(ex).Message("Unable to get user info.").Write();
                return BadRequest("Unable to get user info.");
            }

            User user;
            try {
                user = AddExternalLogin(userInfo);
            } catch (Exception ex) {
                Log.Error().Exception(ex).Message("Unable to get user info.").Write();
                return BadRequest("An error occurred while processing user info.");
            }

            if (user == null)
                return BadRequest("Unable to process user info.");

            return Ok(new { Token = GetToken(user) });
        }

        private void AddGlobalAdminRoleIfFirstUser(User user) {
            if (_isFirstUserChecked)
                return;

            if (_userRepository.Count() == 0)
                user.Roles.Add(AuthorizationRoles.GlobalAdmin);

            _isFirstUserChecked = true;
        }

        private User AddExternalLogin(OAuth2.Models.UserInfo userInfo) {
            ExceptionlessClient.Default.CreateFeatureUsage("External Login").AddObject(userInfo).Submit();
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
                    existingUser.IsEmailAddressVerified = true;
                    _userRepository.Save(existingUser);
                }

                return existingUser;
            }

            // Check to see if a user already exists with this email address.
            User user = !String.IsNullOrEmpty(userInfo.Email) ? _userRepository.GetByEmailAddress(userInfo.Email) : null;
            if (user == null) {
                user = new User { FullName = userInfo.GetFullName(), EmailAddress = userInfo.Email };
                AddGlobalAdminRoleIfFirstUser(user);
            }
            
            user.IsEmailAddressVerified = true;
            user.AddOAuthAccount(userInfo.ProviderName, userInfo.Id, userInfo.Email);
            return _userRepository.Save(user, true);
        }

        private string GetToken(User user) {
            return "d795c4406f6b4bc6ae8d787c65d0274d";
        }
    }
}