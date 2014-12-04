using System;
using System.Threading.Tasks;
using System.Web.Http;
using Exceptionless.Api.Extensions;
using Exceptionless.Core;
using Exceptionless.Core.Messaging;
using Exceptionless.Core.Repositories;
using Exceptionless.Json.Linq;
using Exceptionless.Models;
using OAuth2.Client.Impl;
using OAuth2.Configuration;
using OAuth2.Infrastructure;

namespace Exceptionless.Api.Controllers {
    [RoutePrefix(API_PREFIX + "/auth")]
    public class AuthController : ExceptionlessApiController {
        private readonly IMessagePublisher _messagePublisher;
        private readonly ITokenRepository _tokenRepository;
        private readonly IUserRepository _userRepository;

        public AuthController(IMessagePublisher messagePublisher, ITokenRepository tokenRepository, IUserRepository userRepository) {
            _messagePublisher = messagePublisher;
            _tokenRepository = tokenRepository;
            _userRepository = userRepository;
        }

        [HttpPost]
        [AllowAnonymous]
        [Route("google")]
        public IHttpActionResult Google(JObject value) {
            var authInfo = value.ToObject<AuthInfo>();
            if (authInfo == null || String.IsNullOrEmpty(authInfo.Code))
                return NotFound();

            if (String.IsNullOrEmpty(Settings.Current.GoogleAppId) || String.IsNullOrEmpty(Settings.Current.GoogleAppSecret))
                return NotFound();

            var google = new GoogleClient(new RequestFactory(), new RuntimeClientConfiguration {
                ClientId = Settings.Current.GoogleAppId,
                ClientSecret = Settings.Current.GoogleAppSecret,
                RedirectUri = authInfo.RedirectUri
            });

            OAuth2.Models.UserInfo userInfo;

            try {
                userInfo = google.GetUserInfo(authInfo.Code);
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

            string token = "d795c4406f6b4bc6ae8d787c65d0274d";
            return Ok(new { Token = token });
        }

        private User AddExternalLogin(OAuth2.Models.UserInfo userInfo) {
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
            if (!String.IsNullOrEmpty(userInfo.Email))
                existingUser = _userRepository.GetByEmailAddress(userInfo.Email);

            if (existingUser != null) {
                if (!existingUser.IsEmailAddressVerified) {
                    existingUser.IsEmailAddressVerified = true;
                    _userRepository.Save(existingUser);
                }

                return existingUser;
            }

            var user = new User { FullName = userInfo.FirstName + " " + userInfo.LastName, EmailAddress = userInfo.Email, IsEmailAddressVerified = true };
            user.AddOAuthAccount(userInfo.ProviderName, userInfo.Id, userInfo.Email);
            return _userRepository.Add(user);
        }
    }
}