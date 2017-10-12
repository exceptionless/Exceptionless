using System;
using System.Linq;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Threading.Tasks;
using Exceptionless.Api.Extensions;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Models;
using Foundatio.Repositories;
using Foundatio.Utility;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Api.Security {
    public class ApiKeyMiddleware {
        public const string BearerScheme = "bearer";
        public const string BasicScheme = "basic";
        public const string TokenScheme = "token";

        private readonly RequestDelegate _next;
        private readonly ITokenRepository _tokenRepository;
        private readonly IUserRepository _userRepository;
        private readonly ILogger _logger;

        public ApiKeyMiddleware(RequestDelegate next, IUserRepository userRepository, ITokenRepository tokenRepository, ILogger<ApiKeyMiddleware> logger) {
            _next = next;
            _tokenRepository = tokenRepository;
            _userRepository = userRepository;
            _logger = logger;
        }

        public async Task Invoke(HttpContext context) {
            string authHeaderValue = context.Request.Headers.TryGetAndReturn("Authorization").FirstOrDefault();
            AuthenticationHeaderValue authHeader = null;
            if (!String.IsNullOrEmpty(authHeaderValue))
                authHeader = AuthenticationHeaderValue.Parse(authHeaderValue);
            string scheme = authHeader?.Scheme.ToLower();
            string token = null;
            if (authHeader != null && (scheme == BearerScheme || scheme == TokenScheme))
                token = authHeader.Parameter;
            else if (authHeader != null && scheme == BasicScheme) {
                var authInfo = context.Request.GetBasicAuth();
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
                            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                            return;
                        }

                        if (user == null || !user.IsActive) {
                            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                            return;
                        }

                        if (String.IsNullOrEmpty(user.Salt)) {
                            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                            return;
                        }

                        string encodedPassword = authInfo.Password.ToSaltedHash(user.Salt);
                        if (!String.Equals(encodedPassword, user.Password)) {
                            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                            return;
                        }

                        SetupUserRequest(context, user);
                        await _next(context);
                        return;
                    }
                }
            } else {
                token = context.Request.GetQueryString("access_token");
                if (String.IsNullOrEmpty(token))
                    token = context.Request.GetQueryString("api_key");

                if (String.IsNullOrEmpty(token))
                    token = context.Request.GetQueryString("apikey");
            }

            if (String.IsNullOrEmpty(token)) {
                await _next(context);
                return;
            }

            var tokenRecord = await _tokenRepository.GetByIdAsync(token, o => o.Cache());
            if (tokenRecord == null) {
                using (_logger.BeginScope(new ExceptionlessState().Property("Headers", context.Request.Headers)))
                    _logger.LogWarning("Token {Token} for {Path} not found.", token, context.Request.Path);
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            if (tokenRecord.ExpiresUtc.HasValue && tokenRecord.ExpiresUtc.Value < SystemClock.UtcNow) {
                using (_logger.BeginScope(new ExceptionlessState().Property("Headers", context.Request.Headers)))
                    _logger.LogWarning("Token {Token} for {Path} expired on {TokenExpiresUtc}.", token, context.Request.Path, tokenRecord.ExpiresUtc.Value);
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            if (!String.IsNullOrEmpty(tokenRecord.UserId)) {
                var user = await _userRepository.GetByIdAsync(tokenRecord.UserId, o => o.Cache());
                if (user == null) {
                    using (_logger.BeginScope(new ExceptionlessState().Property("Headers", context.Request.Headers)))
                        _logger.LogWarning("Could not find user for token {Token} with user {user} for {Path}.", token, tokenRecord.UserId, context.Request.Path);

                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return;
                }

                SetupUserRequest(context, user, tokenRecord);
            } else {
                SetupTokenRequest(context, tokenRecord);
            }

            await _next(context);
        }

        private void SetupUserRequest(HttpContext context, User user, Token token = null) {
            context.User = new ClaimsPrincipal(user.ToIdentity(token));
            context.Request.SetUser(user);
        }

        private void SetupTokenRequest(HttpContext context, Token token) {
            context.User = new ClaimsPrincipal(token.ToIdentity());
        }
    }
}