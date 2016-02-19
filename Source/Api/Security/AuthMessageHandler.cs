using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Api.Extensions;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Models;

namespace Exceptionless.Api.Security {
    public class AuthMessageHandler : DelegatingHandler {
        public const string BearerScheme = "bearer";
        public const string BasicScheme = "basic";
        public const string TokenScheme = "token";

        private readonly ITokenRepository _tokenRepository;
        private readonly IUserRepository _userRepository;

        public AuthMessageHandler(ITokenRepository tokenRepository, IUserRepository userRepository) {
            _tokenRepository = tokenRepository;
            _userRepository = userRepository;
        }

        protected virtual Task<HttpResponseMessage> BaseSendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            return base.SendAsync(request, cancellationToken);
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            var authHeader = request.Headers.Authorization;
            string scheme = authHeader?.Scheme.ToLower();
            string token = null;
            if (authHeader != null && (scheme == BearerScheme || scheme == TokenScheme)) {
                token = authHeader.Parameter;
            } else if (authHeader != null && scheme == BasicScheme) {
                var authInfo = request.GetBasicAuth();
                if (authInfo != null) {
                    if (authInfo.Username.ToLower() == "client")
                        token = authInfo.Password;
                    else if (authInfo.Password.ToLower() == "x-oauth-basic" || String.IsNullOrEmpty(authInfo.Password))
                        token = authInfo.Username;
                    else {
                        User user;
                        try {
                            user = await _userRepository.GetByEmailAddressAsync(authInfo.Username);
                        } catch (Exception) {
                            return new HttpResponseMessage(HttpStatusCode.Unauthorized);
                        }

                        if (user == null || !user.IsActive)
                            return new HttpResponseMessage(HttpStatusCode.Unauthorized);

                        if (String.IsNullOrEmpty(user.Salt))
                            return new HttpResponseMessage(HttpStatusCode.Unauthorized);

                        string encodedPassword = authInfo.Password.ToSaltedHash(user.Salt);
                        if (!String.Equals(encodedPassword, user.Password))
                            return new HttpResponseMessage(HttpStatusCode.Unauthorized);

                        SetupUserRequest(request, user);
                        return await BaseSendAsync(request, cancellationToken);
                    }
                }
            } else if (request.Headers.Contains("X-ApiKey")) {
                token = request.Headers.First(h => h.Key == "X-ApiKey").Value.FirstOrDefault();
            } else {
                token = request.GetQueryString("access_token");
                if (String.IsNullOrEmpty(token))
                    token = request.GetQueryString("api_key");

                if (String.IsNullOrEmpty(token))
                    token = request.GetQueryString("apikey");
            }

            if (String.IsNullOrEmpty(token))
                return await BaseSendAsync(request, cancellationToken);

            var tokenRecord = await _tokenRepository.GetByIdAsync(token, true);
            if (tokenRecord == null)
                return new HttpResponseMessage(HttpStatusCode.Unauthorized);

            if (tokenRecord.ExpiresUtc.HasValue && tokenRecord.ExpiresUtc.Value < DateTime.UtcNow)
                return new HttpResponseMessage(HttpStatusCode.Unauthorized);

            if (!String.IsNullOrEmpty(tokenRecord.UserId)) {
                var user = await _userRepository.GetByIdAsync(tokenRecord.UserId, true);
                if (user == null)
                    return new HttpResponseMessage(HttpStatusCode.Unauthorized);

                SetupUserRequest(request, user);
            } else {
                SetupTokenRequest(request, tokenRecord);
            }

            return await BaseSendAsync(request, cancellationToken);
        }

        private void SetupUserRequest(HttpRequestMessage request, User user) {
            request.GetRequestContext().Principal = new ClaimsPrincipal(user.ToIdentity());
            request.SetUser(user);
        }

        private void SetupTokenRequest(HttpRequestMessage request, Token token) {
            request.GetRequestContext().Principal = new ClaimsPrincipal(token.ToIdentity());
        }
    }
}