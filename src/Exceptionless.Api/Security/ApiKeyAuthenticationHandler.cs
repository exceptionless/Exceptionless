using System;
using System.Linq;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Exceptionless.Api.Extensions;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Foundatio.Repositories;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Exceptionless.Api.Security {
    public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions {
        public const string ApiKeySchema = "ApiKey";

        public string AuthenticationScheme { get; } = ApiKeySchema;
    }

    public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions> {
        public const string BearerScheme = "bearer";
        public const string BasicScheme = "basic";
        public const string TokenScheme = "token";

        private readonly ITokenRepository _tokenRepository;
        private readonly IUserRepository _userRepository;

        public ApiKeyAuthenticationHandler(ITokenRepository tokenRepository, IUserRepository userRepository, IOptionsMonitor<ApiKeyAuthenticationOptions> options, ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock) : base(options, logger, encoder, clock) {
            _tokenRepository = tokenRepository;
            _userRepository = userRepository;
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync() {
            string authHeaderValue = Request.Headers.TryGetAndReturn("Authorization").FirstOrDefault();
            AuthenticationHeaderValue authHeader = null;
            if (!String.IsNullOrEmpty(authHeaderValue) && !AuthenticationHeaderValue.TryParse(authHeaderValue, out authHeader))
                return AuthenticateResult.Fail("Unable to parse header");

            string scheme = authHeader?.Scheme.ToLower();
            string token = null;
            if (authHeader != null && (scheme == BearerScheme || scheme == TokenScheme)) {
                token = authHeader.Parameter;
            } else if (authHeader != null && scheme == BasicScheme) {
                var authInfo = Request.GetBasicAuth();
                if (authInfo != null) {
                    if (authInfo.Username.ToLower() == "client")
                        token = authInfo.Password;
                    else if (authInfo.Password.ToLower() == "x-oauth-basic" || String.IsNullOrEmpty(authInfo.Password))
                        token = authInfo.Username;
                    else {
                        User user;
                        try {
                            user = await _userRepository.GetByEmailAddressAsync(authInfo.Username);
                        } catch (Exception ex) {
                            return AuthenticateResult.Fail(ex);
                        }

                        if (user == null || !user.IsActive)
                            return AuthenticateResult.Fail("User is not valid");

                        if (String.IsNullOrEmpty(user.Salt))
                            return AuthenticateResult.Fail("User is not valid");

                        string encodedPassword = authInfo.Password.ToSaltedHash(user.Salt);
                        if (!String.Equals(encodedPassword, user.Password))
                            return AuthenticateResult.Fail("User is not valid");

                        return AuthenticateResult.Success(CreateUserAuthenticationTicket(user));
                    }
                }
            } else {
                token = Request.GetQueryString("access_token");
                if (String.IsNullOrEmpty(token))
                    token = Request.GetQueryString("api_key");

                if (String.IsNullOrEmpty(token))
                    token = Request.GetQueryString("apikey");
            }

            if (String.IsNullOrEmpty(token))
                return AuthenticateResult.NoResult();

            var tokenRecord = await _tokenRepository.GetByIdAsync(token, o => o.Cache());
            if (tokenRecord == null) {
                using (Logger.BeginScope(new ExceptionlessState().Property("Headers", Request.Headers)))
                    Logger.LogWarning("Token {Token} for {Path} not found.", token, Request.Path);

                return AuthenticateResult.Fail("Token is not valid");
            }

            if (tokenRecord.ExpiresUtc.HasValue && tokenRecord.ExpiresUtc.Value < Foundatio.Utility.SystemClock.UtcNow) {
                using (Logger.BeginScope(new ExceptionlessState().Property("Headers", Request.Headers)))
                    Logger.LogWarning("Token {Token} for {Path} expired on {TokenExpiresUtc}.", token, Request.Path, tokenRecord.ExpiresUtc.Value);

                return AuthenticateResult.Fail("Token is not valid");
            }

            if (!String.IsNullOrEmpty(tokenRecord.UserId)) {
                var user = await _userRepository.GetByIdAsync(tokenRecord.UserId, o => o.Cache());
                if (user == null) {
                    using (Logger.BeginScope(new ExceptionlessState().Property("Headers", Request.Headers)))
                        Logger.LogWarning("Could not find user for token {Token} with user {user} for {Path}.", token, tokenRecord.UserId, Request.Path);

                    return AuthenticateResult.Fail("Token is not valid");
                }

                return AuthenticateResult.Success(CreateUserAuthenticationTicket(user, tokenRecord));
            }

            return AuthenticateResult.Success(CreateTokenAuthenticationTicket(tokenRecord));
        }

        private AuthenticationTicket CreateUserAuthenticationTicket(User user, Token token = null) {
            Request.SetUser(user);

            var principal = new ClaimsPrincipal(user.ToIdentity(token));
            return new AuthenticationTicket(principal, CreateAuthenticationProperties(token), Options.AuthenticationScheme);
        }

        private AuthenticationTicket CreateTokenAuthenticationTicket(Token token) {
            var principal = new ClaimsPrincipal(token.ToIdentity());
            return new AuthenticationTicket(principal, CreateAuthenticationProperties(token), Options.AuthenticationScheme);
        }

        private AuthenticationProperties CreateAuthenticationProperties(Token token) {
            var utcNow = Foundatio.Utility.SystemClock.UtcNow;
            return new AuthenticationProperties {
                ExpiresUtc = token?.ExpiresUtc ?? utcNow.AddHours(12),
                IssuedUtc = token?.CreatedUtc ?? utcNow
            };
        }
    }

    public static class ApiKeyAuthMiddlewareExtensions {
        public static AuthenticationBuilder AddApiKeyAuthentication(this AuthenticationBuilder builder) {
            return builder.AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationOptions.ApiKeySchema, null, _ => { });
        }
    }
}