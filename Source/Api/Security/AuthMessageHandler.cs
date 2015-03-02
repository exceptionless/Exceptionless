using System;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Security.Principal;
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

        private readonly TokenManager _tokenManager;
        private readonly IUserRepository _userRepository;

        public AuthMessageHandler(TokenManager tokenManager, IUserRepository userRepository) {
            _tokenManager = tokenManager;
            _userRepository = userRepository;
        }

        protected virtual Task<HttpResponseMessage> BaseSendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            return base.SendAsync(request, cancellationToken);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            var authHeader = request.Headers.Authorization;
            string scheme = authHeader != null ? authHeader.Scheme.ToLower() : null;
            string token = null;
            if (authHeader != null && (scheme == BearerScheme || scheme == TokenScheme))
                token = authHeader.Parameter;
            else if (authHeader != null && scheme == BasicScheme) {
                var authInfo = request.GetBasicAuth();
                if (authInfo != null) {
                    if (authInfo.Username.ToLower() == "client")
                        token = authInfo.Password;
                    else if (authInfo.Password.ToLower() == "x-oauth-basic" || String.IsNullOrEmpty(authInfo.Password))
                        token = authInfo.Username;
                    else {
                        User user;
                        try {
                            user = _userRepository.GetByEmailAddress(authInfo.Username);
                        } catch (Exception) {
                            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
                        }

                        if (user == null || !user.IsActive)
                            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));

                        if (String.IsNullOrEmpty(user.Salt))
                            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));

                        string encodedPassword = authInfo.Password.ToSaltedHash(user.Salt);
                        if (!String.Equals(encodedPassword, user.Password))
                            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));

                        request.GetRequestContext().Principal = new ClaimsPrincipal(user.ToIdentity());

                        return BaseSendAsync(request, cancellationToken);
                    }
                }
            } else {
                string queryToken = request.GetQueryString("access_token");
                if (!String.IsNullOrEmpty(queryToken))
                    token = queryToken;
            }
            
            if (String.IsNullOrEmpty(token))
                return BaseSendAsync(request, cancellationToken);

            //try {
            IPrincipal principal = _tokenManager.Validate(token);
            if (principal != null)
                request.GetRequestContext().Principal = principal;

            return BaseSendAsync(request, cancellationToken);
        }
    }
}