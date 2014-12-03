using System;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Http;
using CodeSmith.Core.Extensions;
using Exceptionless.Core;
using Exceptionless.Core.Messaging;
using Exceptionless.Core.Repositories;
using Exceptionless.Models;
using Flurl;
using Flurl.Http;

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

        public class AuthPayload {
            public string Code { get; set; }
            public string ClientId { get; set; }
            public string RedirectUri { get; set; }
        }

        [HttpPost]
        [Route("google")]
        public async Task<IHttpActionResult> Google(AuthPayload payload) {
            const string provider = "github";
            const string accessTokenUrl = "https://accounts.google.com/o/oauth2/token";
            const string userApiUrl = "https://www.googleapis.com/plus/v1/people/me/openIdConnect";

            var parameters = new {
                code = payload.Code,
                //client_id = payload.ClientId,
                client_secret = Settings.Current.GitHubAppSecret,
                //redirect_uri = payload.RedirectUri
                grant_type = "authorization_code"
            };
            try {
                // Step 1. Exchange authorization code for access token.
                var token = await accessTokenUrl.PostJsonAsync(parameters);

                // Step 2. Retrieve profile information about the current user.
                var profile = await userApiUrl.WithOAuthBearerToken("").GetJsonAsync();

                User existingUser = _userRepository.GetUserByOAuthProvider(provider, profile.id);

                // Step 3a. Link user accounts.
                if (ExceptionlessUser != null) {
                    // There is already a GitHub account that belongs to you
                    if (existingUser != null)
                        return Conflict();

                    ExceptionlessUser.OAuthAccounts.Add(new OAuthAccount { Provider = provider, ProviderUserId = profile.id, Username = profile.name });
                    _userRepository.Save(ExceptionlessUser);
                    return Ok(new { Token = "TODO" });
                }

                // Step 3b. Create a new user account or return an existing one.
                if (existingUser != null)
                    return Ok(new { Token = "TODO" });

                var user = new User { FullName = profile.name, EmailAddress = profile.name };
                user.OAuthAccounts.Add(new OAuthAccount { Provider = provider, ProviderUserId = profile.id, Username = profile.name });
                user = _userRepository.Add(user);
                return Ok(new { Token = "TODO" });
            } catch (Exception ex) {
                throw;
            }
        }

        public bool HasLocalAccount(string emailAddress) {
            User user = _userRepository.GetByEmailAddress(emailAddress);
            return user != null && !String.IsNullOrEmpty(user.Password);
        }

        private bool DeleteOAuthAccount(string provider, string providerUserId) {
            User user = _userRepository.GetUserByOAuthProvider(provider, providerUserId);
            if (user == null)
                return false;

            // allow the account to be deleted only if there is a local password or there is more than one external login
            if (user.OAuthAccounts.Count <= 1 && String.IsNullOrEmpty(user.Password))
                return false;

            OAuthAccount account = user.OAuthAccounts.Single(o => o.Provider == provider && o.ProviderUserId == providerUserId);
            user.OAuthAccounts.Remove(account);
            _userRepository.Save(user);
            return true;
        }
    }
}