using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Configuration;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Foundatio.Caching;
using Foundatio.Lock;
using Foundatio.Repositories;
using Foundatio.Repositories.Utility;

namespace Exceptionless.Core.Services;

public class OAuthService(OAuthServerOptions options, ICacheClient cacheClient, ILockProvider lockProvider, IOAuthApplicationRepository oauthApplicationRepository, IOAuthClientMetadataService oauthClientMetadataService, IOAuthTokenRepository oauthTokenRepository, IUserRepository userRepository, TimeProvider timeProvider)
{
    public const string CodeChallengeMethod = "S256";
    public const int ClientIdLength = 32;
    public const int PkceCodeChallengeLength = 43;
    public const int PkceCodeVerifierMinLength = 43;
    public const int PkceCodeVerifierMaxLength = 128;
    public const int OAuthTokenLength = 64;
    public static readonly OAuthResourceDefinition McpResource = new("/mcp",
    [
        AuthorizationRoles.McpRead,
        AuthorizationRoles.ProjectsRead,
        AuthorizationRoles.StacksRead,
        AuthorizationRoles.StacksWrite,
        AuthorizationRoles.EventsRead,
        AuthorizationRoles.OfflineAccess
    ],
    [
        AuthorizationRoles.McpRead,
        AuthorizationRoles.OfflineAccess
    ]);

    public static readonly OAuthResourceDefinition RestApiResource = new("/api/v2",
    [
        AuthorizationRoles.ProjectsRead,
        AuthorizationRoles.StacksRead,
        AuthorizationRoles.StacksWrite,
        AuthorizationRoles.EventsRead
    ], []);

    public static readonly IReadOnlyCollection<OAuthResourceDefinition> ProtectedResources =
    [
        McpResource,
        RestApiResource
    ];

    public static readonly IReadOnlyCollection<string> SupportedScopes =
    [
        AuthorizationRoles.McpRead,
        AuthorizationRoles.ProjectsRead,
        AuthorizationRoles.StacksRead,
        AuthorizationRoles.StacksWrite,
        AuthorizationRoles.EventsRead,
        AuthorizationRoles.OfflineAccess
    ];

    public static readonly IReadOnlyCollection<string> DefaultScopes =
    [
        AuthorizationRoles.McpRead,
        AuthorizationRoles.ProjectsRead,
        AuthorizationRoles.StacksRead,
        AuthorizationRoles.EventsRead,
        AuthorizationRoles.OfflineAccess
    ];

    private const string AuthorizationCodeCachePrefix = "oauth:code:";
    private const string RefreshTokenLockPrefix = "oauth:refresh:";
    private const string AccessTokenClientValidityCachePrefix = "oauth:client-valid:";
    private const int OAuthGrantFamilyPageLimit = 1000;
    private static readonly TimeSpan AccessTokenClientValidityCacheLifetime = TimeSpan.FromSeconds(30);
    private const string ClientMetadataNotes = "Discovered from OAuth client metadata document.";
    private const string DynamicClientRegistrationNotes = "Registered through OAuth dynamic client registration.";
    private static readonly Regex CodeChallengeRegex = new("^[A-Za-z0-9_-]{43}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex CodeVerifierRegex = new("^[A-Za-z0-9._~-]{43,128}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public bool ClientIdMetadataDocumentSupported => options.EnableClientIdMetadataDocuments;

    public async Task<OAuthClientRegistrationResult> RegisterClientAsync(OAuthClientRegistrationRequest request)
    {
        string tokenEndpointAuthMethod = String.IsNullOrWhiteSpace(request.TokenEndpointAuthMethod) ? "none" : request.TokenEndpointAuthMethod.Trim();
        if (!String.Equals(tokenEndpointAuthMethod, "none", StringComparison.Ordinal))
            return OAuthClientRegistrationResult.Invalid("invalid_client_metadata", "Only public OAuth clients using token_endpoint_auth_method 'none' are supported.");

        if (request.GrantTypes is { Length: > 0 } && !request.GrantTypes.All(g => g is OAuthGrantTypes.AuthorizationCode or OAuthGrantTypes.RefreshToken))
            return OAuthClientRegistrationResult.Invalid("invalid_client_metadata", "Only authorization_code and refresh_token grant types are supported.");

        if (request.GrantTypes is { Length: > 0 } && !request.GrantTypes.Contains(OAuthGrantTypes.AuthorizationCode, StringComparer.Ordinal))
            return OAuthClientRegistrationResult.Invalid("invalid_client_metadata", "The authorization_code grant type is required.");

        if (request.ResponseTypes is { Length: > 0 } && !request.ResponseTypes.SequenceEqual(["code"]))
            return OAuthClientRegistrationResult.Invalid("invalid_client_metadata", "Only the code response type is supported.");

        if (request.RedirectUris is null || request.RedirectUris.Length == 0)
            return OAuthClientRegistrationResult.Invalid("invalid_redirect_uri", "At least one redirect_uri is required.");

        string[] redirectUris = request.RedirectUris
            .Where(uri => !String.IsNullOrWhiteSpace(uri))
            .Select(uri => uri.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (redirectUris.Length == 0 || redirectUris.Length > 20 || redirectUris.Any(uri => !OAuthApplication.IsValidRedirectUri(uri)))
            return OAuthClientRegistrationResult.Invalid("invalid_redirect_uri", "Redirect URIs must be absolute HTTPS URIs or loopback HTTP URIs without fragments.");

        var scopes = NormalizeScopes(request.Scope);
        if (scopes.Count == 0)
            scopes = DefaultScopes;

        if (scopes.Any(s => !SupportedScopes.Contains(s, StringComparer.Ordinal)))
            return OAuthClientRegistrationResult.Invalid("invalid_client_metadata", "One or more scopes are not supported.");

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        string clientId = await CreateUniqueClientIdAsync();
        var application = new OAuthApplication
        {
            Id = ObjectId.GenerateNewId().ToString(),
            ClientId = clientId,
            Name = NormalizeClientName(request.ClientName, clientId),
            RedirectUris = redirectUris,
            Scopes = scopes.ToArray(),
            Notes = DynamicClientRegistrationNotes,
            CreatedByUserId = OAuthApplication.SystemUserId,
            UpdatedByUserId = OAuthApplication.SystemUserId,
            CreatedUtc = utcNow,
            UpdatedUtc = utcNow
        };

        await oauthApplicationRepository.AddAsync(application, o => o.ImmediateConsistency());
        await ClearAccessTokenClientValidityCacheAsync(application.ClientId);
        return OAuthClientRegistrationResult.Success(new OAuthClientRegistrationResponse
        {
            ClientId = application.ClientId,
            ClientName = application.Name,
            RedirectUris = application.RedirectUris,
            GrantTypes = [OAuthGrantTypes.AuthorizationCode, OAuthGrantTypes.RefreshToken],
            ResponseTypes = ["code"],
            Scope = String.Join(' ', application.Scopes),
            TokenEndpointAuthMethod = "none",
            ClientIdIssuedAt = new DateTimeOffset(application.CreatedUtc).ToUnixTimeSeconds()
        });
    }

    public async Task<OAuthClientOptions?> GetClientAsync(string clientId, bool allowClientMetadataDocument = false)
    {
        if (String.IsNullOrWhiteSpace(clientId))
            return null;

        clientId = clientId.Trim();
        var application = await oauthApplicationRepository.GetByClientIdAsync(clientId);
        if (application is not null)
        {
            if (application.IsDisabled)
                return null;

            if (allowClientMetadataDocument)
                application = await RefreshObservedApplicationAsync(application);

            return MapClient(application);
        }

        if (!allowClientMetadataDocument || !options.EnableClientIdMetadataDocuments || !OAuthClientMetadataService.TryCreateClientMetadataDocumentUri(clientId, out _))
            return null;

        return await GetClientFromMetadataDocumentAsync(clientId);
    }

    public async Task<bool> IsAccessTokenClientValidAsync(string? clientId)
    {
        if (String.IsNullOrWhiteSpace(clientId))
            return false;

        clientId = clientId.Trim();
        string cacheKey = GetAccessTokenClientValidityCacheKey(clientId);
        bool? cached = await cacheClient.GetAsync<bool?>(cacheKey, null);
        if (cached.HasValue)
            return cached.Value;

        var application = await oauthApplicationRepository.GetByClientIdAsync(clientId);
        bool isValid = application is { IsDisabled: false };
        await cacheClient.SetAsync(cacheKey, isValid, AccessTokenClientValidityCacheLifetime);
        return isValid;
    }

    public Task ClearAccessTokenClientValidityCacheAsync(string? clientId)
    {
        return String.IsNullOrWhiteSpace(clientId)
            ? Task.CompletedTask
            : cacheClient.RemoveAsync(GetAccessTokenClientValidityCacheKey(clientId.Trim()));
    }

    private async Task<OAuthClientOptions?> GetClientFromMetadataDocumentAsync(string clientId)
    {
        var metadata = await oauthClientMetadataService.GetClientMetadataAsync(clientId);
        if (metadata is null || !TryCreateObservedApplication(clientId, metadata, out var application))
            return null;

        await oauthApplicationRepository.AddAsync(application, o => o.ImmediateConsistency());
        await ClearAccessTokenClientValidityCacheAsync(application.ClientId);
        return MapClient(application);
    }

    private async Task<OAuthApplication> RefreshObservedApplicationAsync(OAuthApplication application)
    {
        if (!String.Equals(application.CreatedByUserId, OAuthApplication.SystemUserId, StringComparison.Ordinal)
            || !String.Equals(application.Notes, ClientMetadataNotes, StringComparison.Ordinal)
            || !options.EnableClientIdMetadataDocuments
            || !OAuthClientMetadataService.TryCreateClientMetadataDocumentUri(application.ClientId, out _))
            return application;

        var metadata = await oauthClientMetadataService.GetClientMetadataAsync(application.ClientId);
        if (metadata is null || !TryCreateObservedApplication(application.ClientId, metadata, out var observedApplication))
            return application;

        bool changed = !String.Equals(application.Name, observedApplication.Name, StringComparison.Ordinal)
            || !HasSameValues(application.RedirectUris, observedApplication.RedirectUris)
            || !HasSameValues(application.Scopes, observedApplication.Scopes);

        if (!changed)
            return application;

        application.Name = observedApplication.Name;
        application.RedirectUris = observedApplication.RedirectUris;
        application.Scopes = observedApplication.Scopes;
        application.UpdatedByUserId = OAuthApplication.SystemUserId;
        application.UpdatedUtc = timeProvider.GetUtcNow().UtcDateTime;

        await oauthApplicationRepository.SaveAsync(application, o => o.ImmediateConsistency());
        await ClearAccessTokenClientValidityCacheAsync(application.ClientId);
        return application;
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

        string[] redirectUris = metadata.RedirectUris?
            .Where(OAuthApplication.IsValidRedirectUri)
            .Distinct(StringComparer.Ordinal)
            .Take(20)
            .ToArray() ?? [];

        if (redirectUris.Length == 0)
            return false;

        var metadataScopes = NormalizeScopes(metadata.Scope);
        string[] scopes = metadataScopes.Count > 0
            ? metadataScopes.Where(s => SupportedScopes.Contains(s, StringComparer.Ordinal)).Distinct(StringComparer.Ordinal).ToArray()
            : DefaultScopes.ToArray();

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
        string name = String.IsNullOrWhiteSpace(clientName) ? "OAuth Application" : clientName.Trim();
        return name.Length <= 200 ? name : name[..200];
    }

    private static bool HasSameValues(IReadOnlyCollection<string> first, IReadOnlyCollection<string> second)
    {
        return first.Count == second.Count && first.Order(StringComparer.Ordinal).SequenceEqual(second.Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }

    private async Task<string> CreateUniqueClientIdAsync()
    {
        while (true)
        {
            string clientId = $"dcr_{StringExtensions.GetRandomString(ClientIdLength)}";
            if (await oauthApplicationRepository.GetByClientIdAsync(clientId) is null)
                return clientId;
        }
    }

    public IReadOnlyCollection<string> GetAllowedScopes(OAuthClientOptions client)
    {
        return client.Scopes;
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

    public async Task<OAuthValidationResult> ValidateAuthorizationRequestAsync(OAuthAuthorizeRequest request, string expectedResource, OAuthResourceDefinition resourceDefinition)
    {
        if (String.IsNullOrWhiteSpace(request.ClientId))
            return OAuthValidationResult.Invalid("invalid_request", "Missing client_id.");

        var client = await GetClientAsync(request.ClientId, allowClientMetadataDocument: true);
        if (client is null)
            return OAuthValidationResult.Invalid("invalid_client", "Unknown OAuth client.");

        if (!String.Equals(request.ResponseType, "code", StringComparison.Ordinal))
            return OAuthValidationResult.Invalid("unsupported_response_type", "Only the code response type is supported.");

        if (!IsRedirectUriAllowed(request.RedirectUri, client.RedirectUris))
            return OAuthValidationResult.Invalid("invalid_request", "Invalid redirect_uri.");

        if (!IsValidCodeChallenge(request.CodeChallenge) || !String.Equals(request.CodeChallengeMethod, CodeChallengeMethod, StringComparison.Ordinal))
            return OAuthValidationResult.Invalid("invalid_request", "PKCE S256 is required.");

        if (!IsExpectedResource(request.Resource, expectedResource))
            return OAuthValidationResult.Invalid("invalid_target", "The requested resource is not supported.");

        var requestedScopes = NormalizeScopes(request.Scope);
        if (requestedScopes.Count == 0)
            return OAuthValidationResult.Invalid("invalid_scope", "At least one scope is required.");

        var allowedScopes = GetAllowedScopes(client);
        if (requestedScopes.Any(s => !allowedScopes.Contains(s, StringComparer.Ordinal)))
            return OAuthValidationResult.Invalid("invalid_scope", "One or more scopes are not allowed for this client.");

        if (resourceDefinition.RequiredScopes.Any(s => !requestedScopes.Contains(s, StringComparer.Ordinal)))
            return OAuthValidationResult.Invalid("invalid_scope", "One or more required resource scopes are missing.");

        var requestedResourceScopes = requestedScopes.Where(s => !String.Equals(s, AuthorizationRoles.OfflineAccess, StringComparison.Ordinal)).ToArray();
        if (requestedResourceScopes.Length == 0)
            return OAuthValidationResult.Invalid("invalid_scope", "At least one resource scope is required.");

        if (requestedResourceScopes.Any(s => !resourceDefinition.Scopes.Contains(s, StringComparer.Ordinal)))
            return OAuthValidationResult.Invalid("invalid_scope", "One or more scopes are not supported by the requested resource.");

        return OAuthValidationResult.Valid(client, requestedScopes);
    }

    public async Task<string> CreateAuthorizationCodeAsync(OAuthAuthorizeRequest request, string userId, IReadOnlyCollection<string> organizationIds)
    {
        string code = StringExtensions.GetNewToken();
        var authorizationCode = new OAuthAuthorizationCode
        {
            ClientId = request.ClientId,
            RedirectUri = request.RedirectUri,
            UserId = userId,
            CodeChallenge = request.CodeChallenge,
            Resource = request.Resource ?? throw new InvalidOperationException("OAuth resource must be validated before creating an authorization code."),
            Scopes = NormalizeScopes(request.Scope),
            OrganizationIds = organizationIds,
            CreatedUtc = timeProvider.GetUtcNow().UtcDateTime
        };
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

        if (!IsValidCodeVerifier(request.CodeVerifier))
            return OAuthTokenIssueResult.Invalid("invalid_grant", "Invalid PKCE verifier.");

        string cacheKey = GetAuthorizationCodeCacheKey(request.Code);
        var codeResult = await cacheClient.GetAsync<OAuthAuthorizationCode>(cacheKey);
        if (!codeResult.HasValue)
            return OAuthTokenIssueResult.Invalid("invalid_grant", "Authorization code is invalid or expired.");

        await cacheClient.RemoveAsync(cacheKey);
        var code = codeResult.Value;
        if (!String.Equals(code.ClientId, request.ClientId, StringComparison.Ordinal) || !String.Equals(code.RedirectUri, request.RedirectUri, StringComparison.Ordinal) || !String.Equals(code.Resource, request.Resource, StringComparison.Ordinal))
            return OAuthTokenIssueResult.Invalid("invalid_grant", "Authorization code does not match the token request.");

        if (!ValidateCodeVerifier(code.CodeChallenge, request.CodeVerifier))
            return OAuthTokenIssueResult.Invalid("invalid_grant", "Invalid PKCE verifier.");

        return OAuthTokenIssueResult.Success(await CreateTokenAsync(code.UserId, code.ClientId, code.Resource, code.Scopes, code.OrganizationIds));
    }

    public async Task<OAuthTokenIssueResult> RefreshAsync(OAuthTokenRequest request)
    {
        if (String.IsNullOrWhiteSpace(request.RefreshToken) || String.IsNullOrWhiteSpace(request.ClientId))
            return OAuthTokenIssueResult.Invalid("invalid_request", "Missing refresh_token or client_id.");

        var client = await GetClientAsync(request.ClientId);
        if (client is null)
            return OAuthTokenIssueResult.Invalid("invalid_client", "Unknown OAuth client.");

        await using var refreshTokenLock = await lockProvider.TryAcquireAsync(GetRefreshTokenLockKey(request.RefreshToken), TimeSpan.FromSeconds(30), CancellationToken.None);
        if (refreshTokenLock is null)
            return OAuthTokenIssueResult.Invalid("invalid_grant", "Refresh token is invalid.");

        var results = await oauthTokenRepository.GetByRefreshTokenHashAsync(CreateTokenHash(request.RefreshToken), o => o.ImmediateConsistency());
        var token = results.Documents.FirstOrDefault();
        if (token is null || !String.Equals(token.ClientId, request.ClientId, StringComparison.Ordinal))
            return OAuthTokenIssueResult.Invalid("invalid_grant", "Refresh token is invalid.");

        if (token.IsDisabled || token.IsSuspended)
        {
            await RevokeOAuthGrantFamilyAsync(token);
            return OAuthTokenIssueResult.Invalid("invalid_grant", "Refresh token is invalid.");
        }

        if (token.OrganizationIds.Count == 0)
            return OAuthTokenIssueResult.Invalid("invalid_grant", "Refresh token is invalid.");

        if (token.RefreshExpiresUtc.HasValue && token.RefreshExpiresUtc.Value < timeProvider.GetUtcNow().UtcDateTime)
            return OAuthTokenIssueResult.Invalid("invalid_grant", "Refresh token is expired.");

        var scopeValidation = ValidateRefreshScopes(token, client);
        if (!scopeValidation.IsValid)
        {
            await RevokeOAuthGrantFamilyAsync(token);
            return OAuthTokenIssueResult.Invalid("invalid_grant", "Refresh token is invalid.");
        }

        var user = await userRepository.GetByIdAsync(token.UserId, o => o.ImmediateConsistency());
        if (user is null || !user.IsActive)
        {
            await RevokeOAuthGrantFamilyAsync(token);
            return OAuthTokenIssueResult.Invalid("invalid_grant", "Refresh token is invalid.");
        }

        var activeOrganizationIds = user.GetActiveOAuthOrganizationIds(token);
        if (activeOrganizationIds.Count == 0)
        {
            await RevokeOAuthGrantFamilyAsync(token);
            return OAuthTokenIssueResult.Invalid("invalid_grant", "Refresh token is invalid.");
        }

        await DisableTokenAsync(token, clearRefresh: false);
        return OAuthTokenIssueResult.Success(await CreateTokenAsync(token.UserId, token.ClientId, token.Resource, scopeValidation.Scopes, activeOrganizationIds, token.GrantId));
    }

    public async Task<bool> RevokeAsync(string? tokenValue, string? clientId)
    {
        if (String.IsNullOrWhiteSpace(tokenValue) || String.IsNullOrWhiteSpace(clientId))
            return false;

        clientId = clientId.Trim();
        string tokenHash = CreateTokenHash(tokenValue);
        var accessTokenResults = await oauthTokenRepository.GetByAccessTokenHashAsync(tokenHash, o => o.ImmediateConsistency());
        var token = accessTokenResults.Documents.FirstOrDefault();
        bool isRefreshToken = false;

        if (token is null)
        {
            var refreshTokenResults = await oauthTokenRepository.GetByRefreshTokenHashAsync(tokenHash, o => o.ImmediateConsistency());
            token = refreshTokenResults.Documents.FirstOrDefault();
            isRefreshToken = token is not null;
        }

        if (token is null || !String.Equals(token.ClientId, clientId, StringComparison.Ordinal))
            return false;

        if (isRefreshToken)
            await RevokeOAuthGrantFamilyAsync(token);
        else
            await DisableTokenAsync(token);

        return true;
    }

    private async Task RevokeOAuthGrantFamilyAsync(OAuthToken token)
    {
        if (String.IsNullOrWhiteSpace(token.GrantId))
        {
            await DisableTokenAsync(token);
            return;
        }

        bool disabledAny = false;
        var results = await oauthTokenRepository.GetByGrantIdForUpdateAsync(token.GrantId, o => o.ImmediateConsistency().SearchAfterPaging().PageLimit(OAuthGrantFamilyPageLimit));
        do
        {
            foreach (var familyToken in results.Documents.Where(t => String.Equals(t.GrantId, token.GrantId, StringComparison.Ordinal)))
            {
                disabledAny = true;
                await DisableTokenAsync(familyToken);
            }
        } while (await results.NextPageAsync());

        if (!disabledAny)
            await DisableTokenAsync(token);
    }

    private static RefreshScopeValidationResult ValidateRefreshScopes(OAuthToken token, OAuthClientOptions client)
    {
        if (!TryGetProtectedResourceByResourceUri(token.Resource, out var resourceDefinition))
            return RefreshScopeValidationResult.Invalid();

        var allowedScopes = client.Scopes.ToHashSet(StringComparer.Ordinal);
        var refreshedScopes = token.Scopes.Where(allowedScopes.Contains).ToArray();
        if (token.Scopes.Contains(AuthorizationRoles.OfflineAccess, StringComparer.Ordinal) && !refreshedScopes.Contains(AuthorizationRoles.OfflineAccess, StringComparer.Ordinal))
            return RefreshScopeValidationResult.Invalid();

        if (resourceDefinition.RequiredScopes.Any(s => !refreshedScopes.Contains(s, StringComparer.Ordinal)))
            return RefreshScopeValidationResult.Invalid();

        var resourceScopes = refreshedScopes.Where(s => !String.Equals(s, AuthorizationRoles.OfflineAccess, StringComparison.Ordinal)).ToArray();
        if (resourceScopes.Length == 0 || resourceScopes.Any(s => !resourceDefinition.Scopes.Contains(s, StringComparer.Ordinal)))
            return RefreshScopeValidationResult.Invalid();

        return RefreshScopeValidationResult.Valid(refreshedScopes);
    }

    private Task DisableTokenAsync(OAuthToken token, bool clearRefresh = true)
    {
        token.IsDisabled = true;
        if (clearRefresh)
            token.RefreshTokenHash = null;

        token.UpdatedUtc = timeProvider.GetUtcNow().UtcDateTime;
        return oauthTokenRepository.SaveAsync(token, o => o.ImmediateConsistency());
    }

    private async Task<OAuthTokenResponse> CreateTokenAsync(string userId, string clientId, string resource, IReadOnlyCollection<string> scopes, IReadOnlyCollection<string> organizationIds, string? grantId = null)
    {
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var accessToken = CreateOAuthToken();
        var refreshToken = scopes.Contains(AuthorizationRoles.OfflineAccess, StringComparer.Ordinal) ? CreateOAuthToken() : null;
        var token = new OAuthToken
        {
            Id = ObjectId.GenerateNewId().ToString(),
            UserId = userId,
            ClientId = clientId,
            GrantId = String.IsNullOrWhiteSpace(grantId) ? StringExtensions.GetNewToken() : grantId,
            Resource = resource,
            AccessTokenHash = CreateTokenHash(accessToken),
            RefreshTokenHash = refreshToken is null ? null : CreateTokenHash(refreshToken),
            Scopes = scopes.ToHashSet(StringComparer.Ordinal),
            OrganizationIds = organizationIds.ToHashSet(StringComparer.Ordinal),
            CreatedUtc = utcNow,
            UpdatedUtc = utcNow,
            ExpiresUtc = utcNow.Add(options.AccessTokenLifetime),
            RefreshExpiresUtc = refreshToken is null ? null : utcNow.Add(options.RefreshTokenLifetime),
            CreatedBy = userId
        };

        await oauthTokenRepository.AddAsync(token, o => o.ImmediateConsistency());
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

    private static bool IsValidCodeChallenge(string? challenge)
    {
        return !String.IsNullOrWhiteSpace(challenge) && challenge.Length == PkceCodeChallengeLength && CodeChallengeRegex.IsMatch(challenge);
    }

    private static bool IsValidCodeVerifier(string? verifier)
    {
        return !String.IsNullOrWhiteSpace(verifier)
            && verifier.Length >= PkceCodeVerifierMinLength
            && verifier.Length <= PkceCodeVerifierMaxLength
            && CodeVerifierRegex.IsMatch(verifier);
    }

    public static string CreateResourceUri(string origin, OAuthResourceDefinition resourceDefinition)
    {
        return $"{origin.TrimEnd('/')}{resourceDefinition.Path}";
    }

    public static bool TryGetProtectedResource(string? resource, string origin, out OAuthResourceDefinition resourceDefinition)
    {
        foreach (var candidate in ProtectedResources)
        {
            if (IsExpectedResource(resource, CreateResourceUri(origin, candidate)))
            {
                resourceDefinition = candidate;
                return true;
            }
        }

        resourceDefinition = null!;
        return false;
    }

    private static bool TryGetProtectedResourceByResourceUri(string? resource, out OAuthResourceDefinition resourceDefinition)
    {
        if (!String.IsNullOrWhiteSpace(resource) && Uri.TryCreate(resource, UriKind.Absolute, out var resourceUri) && String.IsNullOrEmpty(resourceUri.Query) && String.IsNullOrEmpty(resourceUri.Fragment))
        {
            foreach (var candidate in ProtectedResources)
            {
                if (String.Equals(resourceUri.AbsolutePath.TrimEnd('/'), candidate.Path.TrimEnd('/'), StringComparison.Ordinal))
                {
                    resourceDefinition = candidate;
                    return true;
                }
            }
        }

        resourceDefinition = null!;
        return false;
    }

    public static bool IsExpectedResource(string? resource, string expectedResource)
    {
        if (String.IsNullOrWhiteSpace(resource) || !Uri.TryCreate(resource, UriKind.Absolute, out var resourceUri) || !Uri.TryCreate(expectedResource, UriKind.Absolute, out var expectedResourceUri))
            return false;

        if (!String.IsNullOrEmpty(resourceUri.Query) || !String.IsNullOrEmpty(resourceUri.Fragment))
            return false;

        int resourcePort = resourceUri.IsDefaultPort ? GetDefaultPort(resourceUri.Scheme) : resourceUri.Port;
        int expectedResourcePort = expectedResourceUri.IsDefaultPort ? GetDefaultPort(expectedResourceUri.Scheme) : expectedResourceUri.Port;
        return String.Equals(resourceUri.Scheme, expectedResourceUri.Scheme, StringComparison.OrdinalIgnoreCase)
            && String.Equals(resourceUri.Host, expectedResourceUri.Host, StringComparison.OrdinalIgnoreCase)
            && resourcePort == expectedResourcePort
            && String.Equals(resourceUri.AbsolutePath.TrimEnd('/'), expectedResourceUri.AbsolutePath.TrimEnd('/'), StringComparison.Ordinal);
    }

    private static int GetDefaultPort(string scheme)
    {
        return String.Equals(scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ? 443 : 80;
    }

    private static bool IsRedirectUriAllowed(string? redirectUri, IReadOnlyCollection<string> allowedRedirectUris)
    {
        if (String.IsNullOrWhiteSpace(redirectUri))
            return false;

        if (allowedRedirectUris.Contains(redirectUri, StringComparer.Ordinal))
            return true;

        if (!Uri.TryCreate(redirectUri, UriKind.Absolute, out var requestedUri))
            return false;

        foreach (string allowedRedirectUri in allowedRedirectUris)
        {
            if (!Uri.TryCreate(allowedRedirectUri, UriKind.Absolute, out var registeredUri))
                continue;

            if (!IsLoopbackHttpRedirectUri(registeredUri) || !IsLoopbackHttpRedirectUri(requestedUri))
                continue;

            if (!registeredUri.IsDefaultPort && registeredUri.Port != requestedUri.Port)
                continue;

            if (AreEquivalentLoopbackHosts(registeredUri, requestedUri)
                && String.Equals(registeredUri.AbsolutePath, requestedUri.AbsolutePath, StringComparison.Ordinal)
                && String.Equals(registeredUri.Query, requestedUri.Query, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static bool IsLoopbackHttpRedirectUri(Uri uri)
    {
        return String.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && (uri.IsLoopback || String.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase));
    }

    private static bool AreEquivalentLoopbackHosts(Uri registeredUri, Uri requestedUri)
    {
        if (registeredUri.IsLoopback && requestedUri.IsLoopback)
            return true;

        return String.Equals(registeredUri.Host, "localhost", StringComparison.OrdinalIgnoreCase)
            && String.Equals(requestedUri.Host, "localhost", StringComparison.OrdinalIgnoreCase);
    }

    public static string CreateCodeChallenge(string verifier)
    {
        byte[] bytes = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        return Base64UrlEncode(bytes);
    }

    public static bool IsOAuthTokenFormat(string? token)
    {
        return token?.Length == OAuthTokenLength;
    }

    public static string CreateTokenHash(string token)
    {
        if (String.IsNullOrWhiteSpace(token))
            throw new ArgumentException("Token cannot be empty.", nameof(token));

        return Base64UrlEncode(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    private static string CreateOAuthToken() => StringExtensions.GetRandomString(OAuthTokenLength);

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string GetAuthorizationCodeCacheKey(string code) => AuthorizationCodeCachePrefix + code;
    private static string GetRefreshTokenLockKey(string refreshToken) => RefreshTokenLockPrefix + CreateTokenHash(refreshToken);
    private static string GetAccessTokenClientValidityCacheKey(string clientId) => AccessTokenClientValidityCachePrefix + Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(clientId))).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

public static class OAuthGrantTypes
{
    public const string AuthorizationCode = "authorization_code";
    public const string RefreshToken = "refresh_token";
}

public record OAuthAuthorizeRequest
{
    public required string ClientId { get; init; }
    public required string ResponseType { get; init; }
    public required string RedirectUri { get; init; }
    public string? Scope { get; init; }
    public string? State { get; init; }
    public required string CodeChallenge { get; init; }
    public required string CodeChallengeMethod { get; init; }
    public string? Resource { get; init; }
    public IReadOnlyCollection<string> OrganizationIds { get; init; } = [];
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

public record OAuthClientRegistrationRequest
{
    [JsonPropertyName("redirect_uris")]
    public string[]? RedirectUris { get; init; }

    [JsonPropertyName("client_name")]
    public string? ClientName { get; init; }

    [JsonPropertyName("scope")]
    public string? Scope { get; init; }

    [JsonPropertyName("grant_types")]
    public string[]? GrantTypes { get; init; }

    [JsonPropertyName("response_types")]
    public string[]? ResponseTypes { get; init; }

    [JsonPropertyName("token_endpoint_auth_method")]
    public string? TokenEndpointAuthMethod { get; init; }
}

public record OAuthClientRegistrationResult(bool IsSuccess, OAuthClientRegistrationResponse? Response, string? Error, string? ErrorDescription)
{
    public static OAuthClientRegistrationResult Success(OAuthClientRegistrationResponse response) => new(true, response, null, null);
    public static OAuthClientRegistrationResult Invalid(string error, string description) => new(false, null, error, description);
}

public record OAuthClientRegistrationResponse
{
    [JsonPropertyName("client_id")]
    public required string ClientId { get; init; }

    [JsonPropertyName("client_name")]
    public required string ClientName { get; init; }

    [JsonPropertyName("redirect_uris")]
    public required IReadOnlyCollection<string> RedirectUris { get; init; }

    [JsonPropertyName("grant_types")]
    public required IReadOnlyCollection<string> GrantTypes { get; init; }

    [JsonPropertyName("response_types")]
    public required IReadOnlyCollection<string> ResponseTypes { get; init; }

    [JsonPropertyName("scope")]
    public required string Scope { get; init; }

    [JsonPropertyName("token_endpoint_auth_method")]
    public required string TokenEndpointAuthMethod { get; init; }

    [JsonPropertyName("client_id_issued_at")]
    public required long ClientIdIssuedAt { get; init; }
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
    public IReadOnlyCollection<string> OrganizationIds { get; init; } = [];
}


public sealed record OAuthResourceDefinition(string Path, IReadOnlyCollection<string> Scopes, IReadOnlyCollection<string> RequiredScopes);

internal sealed record RefreshScopeValidationResult(bool IsValid, IReadOnlyCollection<string> Scopes)
{
    public static RefreshScopeValidationResult Valid(IReadOnlyCollection<string> scopes) => new(true, scopes);
    public static RefreshScopeValidationResult Invalid() => new(false, []);
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
