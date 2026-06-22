using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Configuration;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Foundatio.Caching;
using Foundatio.Repositories;
using Foundatio.Repositories.Utility;

namespace Exceptionless.Core.Services;

public class OAuthService(OAuthOptions options, ICacheClient cacheClient, IOAuthApplicationRepository oauthApplicationRepository, IOAuthClientMetadataService oauthClientMetadataService, ITokenRepository tokenRepository, TimeProvider timeProvider)
{
    public const string CodeChallengeMethod = "S256";
    public static readonly IReadOnlyCollection<string> SupportedScopes =
    [
        AuthorizationRoles.McpRead,
        AuthorizationRoles.ProjectsRead,
        AuthorizationRoles.StacksRead,
        AuthorizationRoles.EventsRead,
        AuthorizationRoles.OfflineAccess
    ];

    private const string AuthorizationCodeCachePrefix = "oauth:code:";
    private const string ClientMetadataNotes = "Discovered from OAuth client metadata document.";

    public bool ClientIdMetadataDocumentSupported => options.EnableClientIdMetadataDocuments;

    public async Task<OAuthClientOptions?> GetClientAsync(string clientId, bool allowClientMetadataDocument = false)
    {
        if (String.IsNullOrWhiteSpace(clientId))
            return null;

        clientId = clientId.Trim();
        var application = await oauthApplicationRepository.GetByClientIdAsync(clientId);
        if (application is not null)
            return application.IsDisabled ? null : MapClient(application);

        if (!allowClientMetadataDocument || !options.EnableClientIdMetadataDocuments || !OAuthClientMetadataService.TryCreateClientMetadataDocumentUri(clientId, out _))
            return null;

        return await GetClientFromMetadataDocumentAsync(clientId);
    }

    private async Task<OAuthClientOptions?> GetClientFromMetadataDocumentAsync(string clientId)
    {
        var metadata = await oauthClientMetadataService.GetClientMetadataAsync(clientId);
        if (metadata is null || !TryCreateObservedApplication(clientId, metadata, out var application))
            return null;

        await oauthApplicationRepository.AddAsync(application, o => o.ImmediateConsistency());
        return MapClient(application);
    }

    private bool TryCreateObservedApplication(string clientId, OAuthClientMetadataDocument metadata, out OAuthApplication application)
    {
        application = null!;

        if (!String.Equals(metadata.ClientId, clientId, StringComparison.Ordinal))
            return false;

        if (metadata.GrantTypes is { Length: > 0 } && !metadata.GrantTypes.Contains(OAuthGrantTypes.AuthorizationCode, StringComparer.Ordinal))
            return false;

        if (metadata.ResponseTypes is { Length: > 0 } && !metadata.ResponseTypes.Contains("code", StringComparer.Ordinal))
            return false;

        if (!String.IsNullOrWhiteSpace(metadata.TokenEndpointAuthMethod) && !String.Equals(metadata.TokenEndpointAuthMethod, "none", StringComparison.Ordinal))
            return false;

        var redirectUris = metadata.RedirectUris?
            .Where(IsSecureRedirectUri)
            .Distinct(StringComparer.Ordinal)
            .Take(20)
            .ToArray() ?? [];

        if (redirectUris.Length == 0)
            return false;

        var metadataScopes = NormalizeScopes(metadata.Scope);
        var scopes = metadataScopes.Count > 0
            ? metadataScopes.Where(s => SupportedScopes.Contains(s, StringComparer.Ordinal)).Distinct(StringComparer.Ordinal).ToArray()
            : SupportedScopes.ToArray();

        if (scopes.Length == 0)
            return false;

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        application = new OAuthApplication
        {
            Id = ObjectId.GenerateNewId().ToString(),
            ClientId = clientId,
            Name = NormalizeClientName(metadata.ClientName, clientId),
            RedirectUris = redirectUris,
            Scopes = scopes,
            Notes = ClientMetadataNotes,
            CreatedByUserId = OAuthApplication.SystemUserId,
            UpdatedByUserId = OAuthApplication.SystemUserId,
            CreatedUtc = utcNow,
            UpdatedUtc = utcNow
        };

        return true;
    }

    private static OAuthClientOptions MapClient(OAuthApplication application)
    {
        return new OAuthClientOptions
        {
            ClientId = application.ClientId,
            Name = application.Name,
            RedirectUris = application.RedirectUris,
            Scopes = application.Scopes,
            IsDisabled = application.IsDisabled
        }.Normalize();
    }

    private static string NormalizeClientName(string? clientName, string clientId)
    {
        string name = String.IsNullOrWhiteSpace(clientName) ? clientId : clientName.Trim();
        return name.Length <= 200 ? name : name[..200];
    }

    private static bool IsSecureRedirectUri(string redirectUri)
    {
        if (String.IsNullOrWhiteSpace(redirectUri) || !Uri.TryCreate(redirectUri, UriKind.Absolute, out var uri) || !String.IsNullOrEmpty(uri.Fragment))
            return false;

        if (String.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            return true;

        return String.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) && (uri.IsLoopback || String.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase));
    }

    public IReadOnlyCollection<string> GetAllowedScopes(OAuthClientOptions client)
    {
        return client.Scopes.Count > 0 ? client.Scopes : SupportedScopes;
    }

    public IReadOnlyCollection<string> NormalizeScopes(string? scopes)
    {
        if (String.IsNullOrWhiteSpace(scopes))
            return [];

        return scopes.Split([' ', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => s.ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    public async Task<OAuthValidationResult> ValidateAuthorizationRequestAsync(OAuthAuthorizeRequest request)
    {
        if (String.IsNullOrWhiteSpace(request.ClientId))
            return OAuthValidationResult.Invalid("invalid_request", "Missing client_id.");

        var client = await GetClientAsync(request.ClientId, allowClientMetadataDocument: true);
        if (client is null)
            return OAuthValidationResult.Invalid("invalid_client", "Unknown OAuth client.");

        if (String.IsNullOrWhiteSpace(request.RedirectUri) || !client.RedirectUris.Contains(request.RedirectUri, StringComparer.Ordinal))
            return OAuthValidationResult.Invalid("invalid_request", "Invalid redirect_uri.");

        if (String.IsNullOrWhiteSpace(request.CodeChallenge) || !String.Equals(request.CodeChallengeMethod, CodeChallengeMethod, StringComparison.Ordinal))
            return OAuthValidationResult.Invalid("invalid_request", "PKCE S256 is required.");

        if (String.IsNullOrWhiteSpace(request.Resource) || !Uri.TryCreate(request.Resource, UriKind.Absolute, out _))
            return OAuthValidationResult.Invalid("invalid_target", "A valid resource is required.");

        var requestedScopes = NormalizeScopes(request.Scope);
        if (requestedScopes.Count == 0)
            requestedScopes = [AuthorizationRoles.McpRead, AuthorizationRoles.ProjectsRead, AuthorizationRoles.StacksRead, AuthorizationRoles.EventsRead];

        var allowedScopes = GetAllowedScopes(client);
        if (requestedScopes.Any(s => !allowedScopes.Contains(s, StringComparer.Ordinal)))
            return OAuthValidationResult.Invalid("invalid_scope", "One or more scopes are not allowed for this client.");

        return OAuthValidationResult.Valid(client, requestedScopes);
    }

    public async Task<string> CreateAuthorizationCodeAsync(OAuthAuthorizeRequest request, string userId)
    {
        string code = StringExtensions.GetNewToken();
        var authorizationCode = new OAuthAuthorizationCode
        {
            ClientId = request.ClientId,
            RedirectUri = request.RedirectUri,
            UserId = userId,
            CodeChallenge = request.CodeChallenge,
            Resource = request.Resource,
            Scopes = NormalizeScopes(request.Scope),
            CreatedUtc = timeProvider.GetUtcNow().UtcDateTime
        };

        if (authorizationCode.Scopes.Count == 0)
            authorizationCode.Scopes = [AuthorizationRoles.McpRead, AuthorizationRoles.ProjectsRead, AuthorizationRoles.StacksRead, AuthorizationRoles.EventsRead];

        await cacheClient.SetAsync(GetAuthorizationCodeCacheKey(code), authorizationCode, options.AuthorizationCodeLifetime);
        return code;
    }

    public async Task<OAuthTokenIssueResult> ExchangeAuthorizationCodeAsync(OAuthTokenRequest request)
    {
        if (!String.Equals(request.GrantType, OAuthGrantTypes.AuthorizationCode, StringComparison.Ordinal))
            return OAuthTokenIssueResult.Invalid("unsupported_grant_type", "Unsupported grant_type.");

        if (String.IsNullOrWhiteSpace(request.Code) || String.IsNullOrWhiteSpace(request.CodeVerifier) || String.IsNullOrWhiteSpace(request.RedirectUri) || String.IsNullOrWhiteSpace(request.ClientId) || String.IsNullOrWhiteSpace(request.Resource))
            return OAuthTokenIssueResult.Invalid("invalid_request", "Missing required token request fields.");

        if (await GetClientAsync(request.ClientId) is null)
            return OAuthTokenIssueResult.Invalid("invalid_client", "Unknown OAuth client.");

        var cacheKey = GetAuthorizationCodeCacheKey(request.Code);
        var codeResult = await cacheClient.GetAsync<OAuthAuthorizationCode>(cacheKey);
        if (!codeResult.HasValue)
            return OAuthTokenIssueResult.Invalid("invalid_grant", "Authorization code is invalid or expired.");

        await cacheClient.RemoveAsync(cacheKey);
        var code = codeResult.Value;
        if (!String.Equals(code.ClientId, request.ClientId, StringComparison.Ordinal) || !String.Equals(code.RedirectUri, request.RedirectUri, StringComparison.Ordinal) || !String.Equals(code.Resource, request.Resource, StringComparison.Ordinal))
            return OAuthTokenIssueResult.Invalid("invalid_grant", "Authorization code does not match the token request.");

        if (!ValidateCodeVerifier(code.CodeChallenge, request.CodeVerifier))
            return OAuthTokenIssueResult.Invalid("invalid_grant", "Invalid PKCE verifier.");

        return OAuthTokenIssueResult.Success(await CreateTokenAsync(code.UserId, code.ClientId, code.Resource, code.Scopes));
    }

    public async Task<OAuthTokenIssueResult> RefreshAsync(OAuthTokenRequest request)
    {
        if (String.IsNullOrWhiteSpace(request.RefreshToken) || String.IsNullOrWhiteSpace(request.ClientId))
            return OAuthTokenIssueResult.Invalid("invalid_request", "Missing refresh_token or client_id.");

        if (await GetClientAsync(request.ClientId) is null)
            return OAuthTokenIssueResult.Invalid("invalid_client", "Unknown OAuth client.");

        var results = await tokenRepository.GetByRefreshTokenAsync(request.RefreshToken, o => o.ImmediateConsistency());
        var token = results.Documents.FirstOrDefault();
        if (token is null || token.IsDisabled || token.IsSuspended || token.OAuthType != OAuthTokenType.Access || !String.Equals(token.OAuthClientId, request.ClientId, StringComparison.Ordinal))
            return OAuthTokenIssueResult.Invalid("invalid_grant", "Refresh token is invalid.");

        if (token.OAuthRefreshExpiresUtc.HasValue && token.OAuthRefreshExpiresUtc.Value < timeProvider.GetUtcNow().UtcDateTime)
            return OAuthTokenIssueResult.Invalid("invalid_grant", "Refresh token is expired.");

        token.IsDisabled = true;
        token.Refresh = null;
        await tokenRepository.SaveAsync(token, o => o.ImmediateConsistency());

        return OAuthTokenIssueResult.Success(await CreateTokenAsync(token.UserId!, token.OAuthClientId!, token.OAuthResource!, token.Scopes));
    }

    public async Task<bool> RevokeAsync(string? tokenValue)
    {
        if (String.IsNullOrWhiteSpace(tokenValue))
            return false;

        var token = await tokenRepository.GetByIdAsync(tokenValue, o => o.ImmediateConsistency());
        if (token is null)
        {
            var results = await tokenRepository.GetByRefreshTokenAsync(tokenValue, o => o.ImmediateConsistency());
            token = results.Documents.FirstOrDefault();
        }

        if (token is null || token.OAuthType != OAuthTokenType.Access)
            return false;

        token.IsDisabled = true;
        token.Refresh = null;
        await tokenRepository.SaveAsync(token, o => o.ImmediateConsistency());
        return true;
    }

    private async Task<OAuthTokenResponse> CreateTokenAsync(string userId, string clientId, string resource, IReadOnlyCollection<string> scopes)
    {
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var accessToken = StringExtensions.GetNewToken();
        var refreshToken = scopes.Contains(AuthorizationRoles.OfflineAccess, StringComparer.Ordinal) ? StringExtensions.GetNewToken() : null;
        var token = new Token
        {
            Id = accessToken,
            UserId = userId,
            Type = TokenType.Access,
            OAuthType = OAuthTokenType.Access,
            OAuthClientId = clientId,
            OAuthResource = resource,
            Refresh = refreshToken,
            Scopes = scopes.ToHashSet(StringComparer.Ordinal),
            CreatedUtc = utcNow,
            UpdatedUtc = utcNow,
            ExpiresUtc = utcNow.Add(options.AccessTokenLifetime),
            OAuthRefreshExpiresUtc = refreshToken is null ? null : utcNow.Add(options.RefreshTokenLifetime),
            CreatedBy = userId,
            Notes = $"OAuth client: {clientId}"
        };

        await tokenRepository.AddAsync(token, o => o.ImmediateConsistency());
        return new OAuthTokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresIn = (int)options.AccessTokenLifetime.TotalSeconds,
            Scope = String.Join(' ', scopes),
            Resource = resource
        };
    }

    private static bool ValidateCodeVerifier(string challenge, string verifier)
    {
        return String.Equals(challenge, CreateCodeChallenge(verifier), StringComparison.Ordinal);
    }

    public static string CreateCodeChallenge(string verifier)
    {
        var bytes = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        return Base64UrlEncode(bytes);
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string GetAuthorizationCodeCacheKey(string code) => AuthorizationCodeCachePrefix + code;
}

public static class OAuthGrantTypes
{
    public const string AuthorizationCode = "authorization_code";
    public const string RefreshToken = "refresh_token";
}

public record OAuthAuthorizeRequest
{
    public required string ClientId { get; init; }
    public required string RedirectUri { get; init; }
    public string? Scope { get; init; }
    public string? State { get; init; }
    public required string CodeChallenge { get; init; }
    public required string CodeChallengeMethod { get; init; }
    public required string Resource { get; init; }
}

public record OAuthTokenRequest
{
    public required string GrantType { get; init; }
    public string? Code { get; init; }
    public string? RedirectUri { get; init; }
    public string? ClientId { get; init; }
    public string? CodeVerifier { get; init; }
    public string? RefreshToken { get; init; }
    public string? Resource { get; init; }
}

public record OAuthAuthorizationCode
{
    public required string ClientId { get; init; }
    public required string RedirectUri { get; init; }
    public required string UserId { get; init; }
    public required string CodeChallenge { get; init; }
    public required string Resource { get; init; }
    public IReadOnlyCollection<string> Scopes { get; set; } = [];
    public DateTime CreatedUtc { get; init; }
}

public record OAuthValidationResult(bool IsValid, OAuthClientOptions? Client, IReadOnlyCollection<string> Scopes, string? Error, string? ErrorDescription)
{
    public static OAuthValidationResult Valid(OAuthClientOptions client, IReadOnlyCollection<string> scopes) => new(true, client, scopes, null, null);
    public static OAuthValidationResult Invalid(string error, string description) => new(false, null, [], error, description);
}

public record OAuthTokenIssueResult(bool IsSuccess, OAuthTokenResponse? Response, string? Error, string? ErrorDescription)
{
    public static OAuthTokenIssueResult Success(OAuthTokenResponse response) => new(true, response, null, null);
    public static OAuthTokenIssueResult Invalid(string error, string description) => new(false, null, error, description);
}

public record OAuthTokenResponse
{
    [JsonPropertyName("access_token")]
    public required string AccessToken { get; init; }

    [JsonPropertyName("token_type")]
    public string TokenType { get; init; } = "Bearer";

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; init; }

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; init; }

    [JsonPropertyName("scope")]
    public string? Scope { get; init; }

    [JsonPropertyName("resource")]
    public string? Resource { get; init; }
}
