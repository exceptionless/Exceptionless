using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Web.Extensions;
using Foundatio.Repositories;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Exceptionless.Web.Security;

public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string ApiKeySchema = "ApiKey";

    public string AuthenticationScheme { get; } = ApiKeySchema;
}

public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    public const string BearerScheme = "bearer";
    public const string BasicScheme = "basic";
    public const string TokenScheme = "token";

    private readonly ITokenRepository _tokenRepository;
    private readonly IUserRepository _userRepository;
    private readonly TimeProvider _timeProvider;

    public ApiKeyAuthenticationHandler(ITokenRepository tokenRepository, IUserRepository userRepository, IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        TimeProvider timeProvider, ILoggerFactory logger, UrlEncoder encoder) : base(options, logger, encoder)
    {
        _tokenRepository = tokenRepository;
        _userRepository = userRepository;
        _timeProvider = timeProvider;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? authHeaderValue = Request.Headers.TryGetAndReturn("Authorization").FirstOrDefault();
        AuthenticationHeaderValue? authHeader = null;
        if (!String.IsNullOrEmpty(authHeaderValue) && !AuthenticationHeaderValue.TryParse(authHeaderValue, out authHeader))
            return AuthenticateResult.Fail("Unable to parse header");

        string? scheme = authHeader?.Scheme.ToLower();
        string? token = null;
        if (authHeader is not null && (scheme == BearerScheme || scheme == TokenScheme))
        {
            token = authHeader.Parameter;
        }
        else if (authHeader is not null && scheme == BasicScheme)
        {
            var authInfo = Request.GetBasicAuth();
            if (authInfo is not null)
            {
                if (authInfo.Username.ToLower() == "client")
                    token = authInfo.Password;
                else if (authInfo.Password.ToLower() == "x-oauth-basic" || String.IsNullOrEmpty(authInfo.Password))
                    token = authInfo.Username;
                else
                {
                    User? user;
                    try
                    {
                        user = await _userRepository.GetByEmailAddressAsync(authInfo.Username);
                    }
                    catch (Exception ex)
                    {
                        return AuthenticateResult.Fail(ex);
                    }

                    if (user is not { IsActive: true })
                        return AuthenticateResult.Fail("User is not valid");

                    if (String.IsNullOrEmpty(user.Salt))
                        return AuthenticateResult.Fail("User is not valid");

                    string encodedPassword = authInfo.Password.ToSaltedHash(user.Salt);
                    if (!String.Equals(encodedPassword, user.Password))
                        return AuthenticateResult.Fail("User is not valid");

                    return AuthenticateResult.Success(CreateUserAuthenticationTicket(user));
                }
            }
        }
        else
        {
            token = Request.GetQueryString("access_token");
            if (String.IsNullOrEmpty(token))
                token = Request.GetQueryString("api_key");

            if (String.IsNullOrEmpty(token))
                token = Request.GetQueryString("apikey");
        }

        if (String.IsNullOrEmpty(token))
            return AuthenticateResult.NoResult();

        Request.HttpContext.Items["ApiKey"] = token;
        var tokenRecord = await _tokenRepository.GetByIdAsync(token, o => o.Cache());
        if (tokenRecord is null)
        {
            Logger.LogInformation("Token {Token} for {Path} not found", token, Request.Path);
            return AuthenticateResult.Fail("Token is not valid");
        }

        if (tokenRecord.IsDisabled || (Request.IsEventPost() && tokenRecord.IsSuspended))
        {
            AppDiagnostics.PostsBlocked.Add(1);
            Logger.LogInformation("Token {Token} is disabled or account is suspended for {Path}", token, Request.Path);

            return AuthenticateResult.Fail("Token is not valid");
        }

        if (tokenRecord.ExpiresUtc.HasValue && tokenRecord.ExpiresUtc.Value < _timeProvider.GetUtcNow().UtcDateTime)
        {
            Logger.LogInformation("Token {Token} for {Path} expired on {TokenExpiresUtc}", token, Request.Path, tokenRecord.ExpiresUtc.Value);

            return AuthenticateResult.Fail("Token is not valid");
        }

        if (!String.IsNullOrEmpty(tokenRecord.UserId))
        {
            var user = await _userRepository.GetByIdAsync(tokenRecord.UserId, o => o.Cache());
            if (user is null)
            {
                Logger.LogInformation("Could not find user for token {Token} with user {UserId} for {Path}", token, tokenRecord.UserId, Request.Path);
                return AuthenticateResult.Fail("Token is not valid");
            }

            return AuthenticateResult.Success(CreateUserAuthenticationTicket(user, tokenRecord));
        }

        return AuthenticateResult.Success(CreateTokenAuthenticationTicket(tokenRecord));
    }

    private AuthenticationTicket CreateUserAuthenticationTicket(User user, Token? token = null)
    {
        Request.SetUser(user);

        var principal = new ClaimsPrincipal(user.ToIdentity(token));
        return new AuthenticationTicket(principal, CreateAuthenticationProperties(token), Options.AuthenticationScheme);
    }

    private AuthenticationTicket CreateTokenAuthenticationTicket(Token token)
    {
        var principal = new ClaimsPrincipal(token.ToIdentity());
        return new AuthenticationTicket(principal, CreateAuthenticationProperties(token), Options.AuthenticationScheme);
    }

    private AuthenticationProperties CreateAuthenticationProperties(Token? token)
    {
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        return new AuthenticationProperties
        {
            ExpiresUtc = token?.ExpiresUtc ?? utcNow.AddHours(12),
            IssuedUtc = token?.CreatedUtc ?? utcNow
        };
    }
}

public static class ApiKeyAuthMiddlewareExtensions
{
    public static AuthenticationBuilder AddApiKeyAuthentication(this AuthenticationBuilder builder)
    {
        return builder.AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationOptions.ApiKeySchema, null, _ => { });
    }
}
