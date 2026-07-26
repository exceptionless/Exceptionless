using System.Security.Cryptography;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Exceptionless.Core.Configuration;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories;
using Foundatio.Caching;
using Foundatio.Lock;
using Foundatio.Repositories;
using Foundatio.Repositories.Utility;

namespace Exceptionless.Core.Services;

public sealed class OAuthDeviceService(
    OAuthServerOptions options,
    ICacheClient cacheClient,
    ILockProvider lockProvider,
    OAuthService oauthService,
    IUserRepository userRepository,
    TimeProvider timeProvider)
{
    private const string DeviceCodeCachePrefix = "oauth:device:";
    private const string DeviceUserCodeCachePrefix = "oauth:user-code:";
    private const string DeviceCodeLockPrefix = "oauth:device-lock:";
    private const int DeviceUserCodeLength = 8;
    private const int DeviceUserCodeGroupLength = 4;
    private const string DeviceUserCodeCharacters = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private static readonly TimeSpan DeviceCodeLockTimeout = TimeSpan.FromSeconds(30);
    private static readonly Regex DeviceUserCodeRegex = new("^[A-Z2-9]{8}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public async Task<OAuthDeviceAuthorizationIssueResult> CreateAuthorizationAsync(
        OAuthDeviceAuthorizationRequest request,
        string expectedResource,
        OAuthResourceDefinition resourceDefinition,
        string verificationUri)
    {
        if (String.IsNullOrWhiteSpace(request.ClientId))
        {
            return OAuthDeviceAuthorizationIssueResult.Invalid("invalid_request", "Missing client_id.");
        }

        var client = await oauthService.GetClientAsync(request.ClientId, allowClientMetadataDocument: true);
        if (client is null)
        {
            return OAuthDeviceAuthorizationIssueResult.Invalid("invalid_client", "Unknown OAuth client.");
        }

        if (!client.GrantTypes.Contains(OAuthGrantTypes.DeviceCode, StringComparer.Ordinal))
        {
            return OAuthDeviceAuthorizationIssueResult.Invalid("unauthorized_client", "The client is not allowed to use the device_code grant type.");
        }

        if (!OAuthService.IsExpectedResource(request.Resource, expectedResource))
        {
            return OAuthDeviceAuthorizationIssueResult.Invalid("invalid_target", "The requested resource is not supported.");
        }

        string? requestedScope = String.IsNullOrWhiteSpace(request.Scope)
            ? String.Join(' ', oauthService.GetDefaultScopes(client, resourceDefinition))
            : request.Scope;

        var validation = oauthService.ValidateRequestedScopes(client, requestedScope, resourceDefinition);
        if (!validation.IsValid)
        {
            return OAuthDeviceAuthorizationIssueResult.Invalid(validation.Error!, validation.ErrorDescription!);
        }

        if (options.DeviceCodeLifetime <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("OAuth device code lifetime must be positive.");
        }

        string deviceCode = OAuthService.CreateOAuthToken();
        string deviceCodeHash = OAuthService.CreateTokenHash(deviceCode);
        string userCode = await ReserveUniqueUserCodeAsync(deviceCodeHash, options.DeviceCodeLifetime);
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        int pollingIntervalSeconds = Math.Max(1, (int)options.DeviceCodePollingInterval.TotalSeconds);
        var authorization = new OAuthDeviceAuthorization
        {
            ClientId = client.ClientId,
            Resource = expectedResource,
            Scopes = validation.Scopes,
            UserCode = FormatUserCode(userCode),
            UserCodeNormalized = userCode,
            Status = OAuthDeviceAuthorizationStatus.Pending,
            PollingIntervalSeconds = pollingIntervalSeconds,
            CreatedUtc = utcNow,
            UpdatedUtc = utcNow,
            ExpiresUtc = utcNow.Add(options.DeviceCodeLifetime)
        };

        try
        {
            bool stored = await cacheClient.SetAsync(GetDeviceCodeCacheKey(deviceCodeHash), authorization, options.DeviceCodeLifetime);
            if (!stored)
            {
                throw new InvalidOperationException("Unable to store OAuth device authorization.");
            }
        }
        catch
        {
            await cacheClient.RemoveIfEqualAsync(GetDeviceUserCodeCacheKey(userCode), deviceCodeHash);
            throw;
        }

        return OAuthDeviceAuthorizationIssueResult.Success(new OAuthDeviceAuthorizationResponse
        {
            DeviceCode = deviceCode,
            UserCode = authorization.UserCode,
            VerificationUri = verificationUri,
            VerificationUriComplete = $"{verificationUri}?user_code={Uri.EscapeDataString(authorization.UserCode)}",
            ExpiresIn = (int)options.DeviceCodeLifetime.TotalSeconds,
            Interval = pollingIntervalSeconds
        });
    }

    public async Task<OAuthDeviceConsentResult> GetConsentAsync(string? userCode)
    {
        var lookup = await GetAuthorizationByUserCodeAsync(userCode);
        if (!lookup.IsSuccess)
        {
            return OAuthDeviceConsentResult.Invalid(lookup.Error!, lookup.ErrorDescription!);
        }

        var authorization = lookup.Authorization!;
        if (authorization.Status != OAuthDeviceAuthorizationStatus.Pending)
        {
            return OAuthDeviceConsentResult.Invalid("expired_token", "Device authorization is no longer pending.");
        }

        var client = await oauthService.GetClientAsync(authorization.ClientId);
        if (client is null)
        {
            return OAuthDeviceConsentResult.Invalid("invalid_client", "Unknown OAuth client.");
        }

        if (!client.GrantTypes.Contains(OAuthGrantTypes.DeviceCode, StringComparer.Ordinal))
        {
            return OAuthDeviceConsentResult.Invalid("unauthorized_client", "The client is not allowed to use the device_code grant type.");
        }

        if (!OAuthService.TryGetProtectedResourceByResourceUri(authorization.Resource, out var resourceDefinition))
        {
            return OAuthDeviceConsentResult.Invalid("invalid_target", "The requested resource is not supported.");
        }

        return OAuthDeviceConsentResult.Valid(client, authorization, resourceDefinition);
    }

    public async Task<OAuthDeviceAuthorizationResult> ApproveAuthorizationAsync(OAuthDeviceApprovalRequest request, string userId)
    {
        var lookup = await GetAuthorizationByUserCodeAsync(request.UserCode);
        if (!lookup.IsSuccess)
        {
            return OAuthDeviceAuthorizationResult.Invalid(lookup.Error!, lookup.ErrorDescription!);
        }

        await using var deviceCodeLock = await lockProvider.TryAcquireAsync(GetDeviceCodeLockKey(lookup.DeviceCodeHash!), DeviceCodeLockTimeout, CancellationToken.None);
        if (deviceCodeLock is null)
        {
            return OAuthDeviceAuthorizationResult.Invalid("temporarily_unavailable", "Device authorization is being processed.");
        }

        lookup = await GetAuthorizationByDeviceCodeHashAsync(lookup.DeviceCodeHash!);
        if (!lookup.IsSuccess)
        {
            return OAuthDeviceAuthorizationResult.Invalid(lookup.Error!, lookup.ErrorDescription!);
        }

        var authorization = lookup.Authorization!;
        if (authorization.Status != OAuthDeviceAuthorizationStatus.Pending)
        {
            return OAuthDeviceAuthorizationResult.Invalid("expired_token", "Device authorization is no longer pending.");
        }

        var client = await oauthService.GetClientAsync(authorization.ClientId);
        if (client is null)
        {
            return OAuthDeviceAuthorizationResult.Invalid("invalid_client", "Unknown OAuth client.");
        }

        if (!client.GrantTypes.Contains(OAuthGrantTypes.DeviceCode, StringComparer.Ordinal))
        {
            return OAuthDeviceAuthorizationResult.Invalid("unauthorized_client", "The client is not allowed to use the device_code grant type.");
        }

        if (!OAuthService.TryGetProtectedResourceByResourceUri(authorization.Resource, out var resourceDefinition))
        {
            return OAuthDeviceAuthorizationResult.Invalid("invalid_target", "The requested resource is not supported.");
        }

        var requestedScopes = oauthService.NormalizeScopes(request.Scope);
        if (requestedScopes.Count == 0)
        {
            return OAuthDeviceAuthorizationResult.Invalid("invalid_scope", "At least one scope is required.");
        }

        if (requestedScopes.Any(scope => !authorization.Scopes.Contains(scope, StringComparer.Ordinal)))
        {
            return OAuthDeviceAuthorizationResult.Invalid("invalid_scope", "One or more scopes were not requested by the device.");
        }

        var scopeValidation = oauthService.ValidateRequestedScopes(client, String.Join(' ', requestedScopes), resourceDefinition);
        if (!scopeValidation.IsValid)
        {
            return OAuthDeviceAuthorizationResult.Invalid(scopeValidation.Error!, scopeValidation.ErrorDescription!);
        }

        authorization.Status = OAuthDeviceAuthorizationStatus.Approved;
        authorization.UserId = userId;
        authorization.Scopes = scopeValidation.Scopes;
        authorization.OrganizationIds = request.OrganizationIds.ToArray();
        authorization.UpdatedUtc = timeProvider.GetUtcNow().UtcDateTime;
        await SetAuthorizationAsync(lookup.DeviceCodeHash!, authorization);
        return OAuthDeviceAuthorizationResult.Success();
    }

    public async Task<OAuthDeviceAuthorizationResult> DenyAuthorizationAsync(string? userCode)
    {
        var lookup = await GetAuthorizationByUserCodeAsync(userCode);
        if (!lookup.IsSuccess)
        {
            return OAuthDeviceAuthorizationResult.Invalid(lookup.Error!, lookup.ErrorDescription!);
        }

        await using var deviceCodeLock = await lockProvider.TryAcquireAsync(GetDeviceCodeLockKey(lookup.DeviceCodeHash!), DeviceCodeLockTimeout, CancellationToken.None);
        if (deviceCodeLock is null)
        {
            return OAuthDeviceAuthorizationResult.Invalid("temporarily_unavailable", "Device authorization is being processed.");
        }

        lookup = await GetAuthorizationByDeviceCodeHashAsync(lookup.DeviceCodeHash!);
        if (!lookup.IsSuccess)
        {
            return OAuthDeviceAuthorizationResult.Invalid(lookup.Error!, lookup.ErrorDescription!);
        }

        var authorization = lookup.Authorization!;
        if (authorization.Status != OAuthDeviceAuthorizationStatus.Pending)
        {
            return OAuthDeviceAuthorizationResult.Invalid("expired_token", "Device authorization is no longer pending.");
        }

        authorization.Status = OAuthDeviceAuthorizationStatus.Denied;
        authorization.UpdatedUtc = timeProvider.GetUtcNow().UtcDateTime;
        await SetAuthorizationAsync(lookup.DeviceCodeHash!, authorization);
        return OAuthDeviceAuthorizationResult.Success();
    }

    public async Task<OAuthTokenIssueResult> ExchangeCodeAsync(OAuthTokenRequest request)
    {
        if (!String.Equals(request.GrantType, OAuthGrantTypes.DeviceCode, StringComparison.Ordinal))
        {
            return OAuthTokenIssueResult.Invalid("unsupported_grant_type", "Unsupported grant_type.");
        }

        if (String.IsNullOrWhiteSpace(request.ClientId) || String.IsNullOrWhiteSpace(request.DeviceCode))
        {
            return OAuthTokenIssueResult.Invalid("invalid_request", "Missing client_id or device_code.");
        }

        string deviceCodeHash = OAuthService.CreateTokenHash(request.DeviceCode);
        await using var deviceCodeLock = await lockProvider.TryAcquireAsync(GetDeviceCodeLockKey(deviceCodeHash), DeviceCodeLockTimeout, CancellationToken.None);
        if (deviceCodeLock is null)
        {
            return OAuthTokenIssueResult.Invalid("authorization_pending", "Device authorization is being processed.");
        }

        var lookup = await GetAuthorizationByDeviceCodeHashAsync(deviceCodeHash);
        if (!lookup.IsSuccess)
        {
            return OAuthTokenIssueResult.Invalid(lookup.Error!, lookup.ErrorDescription!);
        }

        var authorization = lookup.Authorization!;
        if (!String.Equals(authorization.ClientId, request.ClientId, StringComparison.Ordinal))
        {
            return OAuthTokenIssueResult.Invalid("invalid_grant", "Device code does not match the token request.");
        }

        var client = await oauthService.GetClientAsync(request.ClientId);
        if (client is null)
        {
            await RemoveAuthorizationAsync(deviceCodeHash, authorization);
            return OAuthTokenIssueResult.Invalid("invalid_client", "Unknown OAuth client.");
        }

        if (!client.GrantTypes.Contains(OAuthGrantTypes.DeviceCode, StringComparer.Ordinal))
        {
            await RemoveAuthorizationAsync(deviceCodeHash, authorization);
            return OAuthTokenIssueResult.Invalid("unauthorized_client", "The client is not allowed to use the device_code grant type.");
        }

        if (authorization.Status == OAuthDeviceAuthorizationStatus.Denied)
        {
            await RemoveAuthorizationAsync(deviceCodeHash, authorization);
            return OAuthTokenIssueResult.Invalid("access_denied", "The device authorization request was denied.");
        }

        if (authorization.Status == OAuthDeviceAuthorizationStatus.Approved)
        {
            return await ExchangeApprovedAuthorizationAsync(deviceCodeHash, authorization, client);
        }

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        int pollingIntervalSeconds = Math.Max(1, authorization.PollingIntervalSeconds);
        if (authorization.LastPolledUtc.HasValue && utcNow - authorization.LastPolledUtc.Value < TimeSpan.FromSeconds(pollingIntervalSeconds))
        {
            authorization.PollingIntervalSeconds = pollingIntervalSeconds + 5;
            authorization.LastPolledUtc = utcNow;
            authorization.UpdatedUtc = utcNow;
            await SetAuthorizationAsync(deviceCodeHash, authorization);
            return OAuthTokenIssueResult.Invalid("slow_down", "Poll interval exceeded.");
        }

        authorization.LastPolledUtc = utcNow;
        authorization.UpdatedUtc = utcNow;
        await SetAuthorizationAsync(deviceCodeHash, authorization);
        return OAuthTokenIssueResult.Invalid("authorization_pending", "Device authorization is pending.");
    }

    private async Task<OAuthTokenIssueResult> ExchangeApprovedAuthorizationAsync(
        string deviceCodeHash,
        OAuthDeviceAuthorization authorization,
        OAuthClientOptions client)
    {
        if (String.IsNullOrWhiteSpace(authorization.UserId) || authorization.OrganizationIds.Count == 0)
        {
            await RemoveAuthorizationAsync(deviceCodeHash, authorization);
            return OAuthTokenIssueResult.Invalid("invalid_grant", "Device authorization is invalid.");
        }

        if (!OAuthService.TryGetProtectedResourceByResourceUri(authorization.Resource, out var resourceDefinition))
        {
            await RemoveAuthorizationAsync(deviceCodeHash, authorization);
            return OAuthTokenIssueResult.Invalid("invalid_grant", "Device authorization is invalid.");
        }

        var allowedScopes = client.Scopes.ToHashSet(StringComparer.Ordinal);
        var currentScopes = authorization.Scopes.Where(allowedScopes.Contains).ToArray();
        var scopeValidation = oauthService.ValidateRequestedScopes(client, String.Join(' ', currentScopes), resourceDefinition);
        if (!scopeValidation.IsValid)
        {
            await RemoveAuthorizationAsync(deviceCodeHash, authorization);
            return OAuthTokenIssueResult.Invalid("invalid_grant", "Device authorization is invalid.");
        }

        var user = await userRepository.GetByIdAsync(authorization.UserId, o => o.ImmediateConsistency());
        if (user is null || !user.IsActive)
        {
            await RemoveAuthorizationAsync(deviceCodeHash, authorization);
            return OAuthTokenIssueResult.Invalid("invalid_grant", "Device authorization is invalid.");
        }

        var userOrganizationIds = user.OrganizationIds.ToHashSet(StringComparer.Ordinal);
        var activeOrganizationIds = authorization.OrganizationIds.Where(userOrganizationIds.Contains).Distinct(StringComparer.Ordinal).ToArray();
        if (activeOrganizationIds.Length == 0)
        {
            await RemoveAuthorizationAsync(deviceCodeHash, authorization);
            return OAuthTokenIssueResult.Invalid("invalid_grant", "Device authorization is invalid.");
        }

        await RemoveAuthorizationAsync(deviceCodeHash, authorization);
        var token = await oauthService.CreateTokenAsync(
            authorization.UserId,
            authorization.ClientId,
            authorization.Resource,
            scopeValidation.Scopes,
            activeOrganizationIds);
        return OAuthTokenIssueResult.Success(token);
    }

    private async Task SetAuthorizationAsync(string deviceCodeHash, OAuthDeviceAuthorization authorization)
    {
        var lifetime = authorization.ExpiresUtc - timeProvider.GetUtcNow().UtcDateTime;
        if (lifetime <= TimeSpan.Zero || !await cacheClient.SetAsync(GetDeviceCodeCacheKey(deviceCodeHash), authorization, lifetime))
        {
            await RemoveAuthorizationAsync(deviceCodeHash, authorization);
        }
    }

    private Task RemoveAuthorizationAsync(string deviceCodeHash, OAuthDeviceAuthorization authorization)
    {
        return Task.WhenAll(
            cacheClient.RemoveAsync(GetDeviceCodeCacheKey(deviceCodeHash)),
            cacheClient.RemoveIfEqualAsync(GetDeviceUserCodeCacheKey(authorization.UserCodeNormalized), deviceCodeHash));
    }

    private async Task<OAuthDeviceAuthorizationLookupResult> GetAuthorizationByUserCodeAsync(string? userCode)
    {
        string? normalizedUserCode = NormalizeUserCode(userCode);
        if (normalizedUserCode is null)
        {
            return OAuthDeviceAuthorizationLookupResult.Invalid("expired_token", "Device authorization is invalid or expired.");
        }

        string userCodeCacheKey = GetDeviceUserCodeCacheKey(normalizedUserCode);
        var deviceCodeResult = await cacheClient.GetAsync<string>(userCodeCacheKey);
        if (!deviceCodeResult.HasValue)
        {
            return OAuthDeviceAuthorizationLookupResult.Invalid("expired_token", "Device authorization is invalid or expired.");
        }

        var lookup = await GetAuthorizationByDeviceCodeHashAsync(deviceCodeResult.Value);
        if (!lookup.IsSuccess)
        {
            await cacheClient.RemoveAsync(userCodeCacheKey);
        }

        return lookup;
    }

    private async Task<OAuthDeviceAuthorizationLookupResult> GetAuthorizationByDeviceCodeHashAsync(string deviceCodeHash)
    {
        var authorizationResult = await cacheClient.GetAsync<OAuthDeviceAuthorization>(GetDeviceCodeCacheKey(deviceCodeHash));
        if (!authorizationResult.HasValue)
        {
            return OAuthDeviceAuthorizationLookupResult.Invalid("expired_token", "Device authorization is invalid or expired.");
        }

        var authorization = authorizationResult.Value;
        if (authorization.ExpiresUtc <= timeProvider.GetUtcNow().UtcDateTime)
        {
            await RemoveAuthorizationAsync(deviceCodeHash, authorization);
            return OAuthDeviceAuthorizationLookupResult.Invalid("expired_token", "Device authorization is invalid or expired.");
        }

        return OAuthDeviceAuthorizationLookupResult.Success(deviceCodeHash, authorization);
    }

    private async Task<string> ReserveUniqueUserCodeAsync(string deviceCodeHash, TimeSpan lifetime)
    {
        for (int attempt = 0; attempt < 10; attempt++)
        {
            string userCode = CreateUserCode();
            if (await cacheClient.AddAsync(GetDeviceUserCodeCacheKey(userCode), deviceCodeHash, lifetime))
            {
                return userCode;
            }
        }

        throw new InvalidOperationException("Unable to create a unique OAuth device user code.");
    }

    private static string CreateUserCode()
    {
        Span<char> code = stackalloc char[DeviceUserCodeLength];
        for (int i = 0; i < code.Length; i++)
        {
            code[i] = DeviceUserCodeCharacters[RandomNumberGenerator.GetInt32(DeviceUserCodeCharacters.Length)];
        }

        return new string(code);
    }

    private static string FormatUserCode(string userCode)
    {
        return userCode.Insert(DeviceUserCodeGroupLength, "-");
    }

    private static string? NormalizeUserCode(string? userCode)
    {
        if (String.IsNullOrWhiteSpace(userCode))
        {
            return null;
        }

        var normalized = new string(userCode
            .Where(c => c != '-' && !Char.IsWhiteSpace(c))
            .Select(Char.ToUpperInvariant)
            .ToArray());

        return DeviceUserCodeRegex.IsMatch(normalized) ? normalized : null;
    }

    private static string GetDeviceCodeCacheKey(string deviceCodeHash)
    {
        return DeviceCodeCachePrefix + deviceCodeHash;
    }

    private static string GetDeviceUserCodeCacheKey(string userCode)
    {
        return DeviceUserCodeCachePrefix + OAuthService.CreateTokenHash(userCode);
    }

    private static string GetDeviceCodeLockKey(string deviceCodeHash)
    {
        return DeviceCodeLockPrefix + deviceCodeHash;
    }
}

public record OAuthDeviceAuthorizationRequest
{
    public required string ClientId { get; init; }
    public string? Scope { get; init; }
    public string? Resource { get; init; }
}

public record OAuthDeviceApprovalRequest
{
    public required string UserCode { get; init; }
    public required string Scope { get; init; }
    public IReadOnlyCollection<string> OrganizationIds { get; init; } = [];
}

public record OAuthDeviceAuthorizationResponse
{
    [JsonPropertyName("device_code")]
    public required string DeviceCode { get; init; }

    [JsonPropertyName("user_code")]
    public required string UserCode { get; init; }

    [JsonPropertyName("verification_uri")]
    public required string VerificationUri { get; init; }

    [JsonPropertyName("verification_uri_complete")]
    public required string VerificationUriComplete { get; init; }

    [JsonPropertyName("expires_in")]
    public required int ExpiresIn { get; init; }

    [JsonPropertyName("interval")]
    public required int Interval { get; init; }
}

public class OAuthDeviceAuthorization
{
    public required string ClientId { get; init; }
    public required string Resource { get; init; }
    public required string UserCode { get; init; }
    public required string UserCodeNormalized { get; init; }
    public OAuthDeviceAuthorizationStatus Status { get; set; }
    public IReadOnlyCollection<string> Scopes { get; set; } = [];
    public string? UserId { get; set; }
    public IReadOnlyCollection<string> OrganizationIds { get; set; } = [];
    public int PollingIntervalSeconds { get; set; }
    public DateTime? LastPolledUtc { get; set; }
    public DateTime CreatedUtc { get; init; }
    public DateTime UpdatedUtc { get; set; }
    public DateTime ExpiresUtc { get; init; }
}

public enum OAuthDeviceAuthorizationStatus
{
    Pending = 0,
    Approved = 1,
    Denied = 2
}

internal sealed record OAuthDeviceAuthorizationLookupResult(
    bool IsSuccess,
    string? DeviceCodeHash,
    OAuthDeviceAuthorization? Authorization,
    string? Error,
    string? ErrorDescription)
{
    public static OAuthDeviceAuthorizationLookupResult Success(string deviceCodeHash, OAuthDeviceAuthorization authorization)
    {
        return new(true, deviceCodeHash, authorization, null, null);
    }

    public static OAuthDeviceAuthorizationLookupResult Invalid(string error, string description)
    {
        return new(false, null, null, error, description);
    }
}

public sealed record OAuthDeviceAuthorizationIssueResult(
    bool IsSuccess,
    OAuthDeviceAuthorizationResponse? Response,
    string? Error,
    string? ErrorDescription)
{
    public static OAuthDeviceAuthorizationIssueResult Success(OAuthDeviceAuthorizationResponse response)
    {
        return new(true, response, null, null);
    }

    public static OAuthDeviceAuthorizationIssueResult Invalid(string error, string description)
    {
        return new(false, null, error, description);
    }
}

public sealed record OAuthDeviceConsentResult(
    bool IsSuccess,
    OAuthClientOptions? Client,
    OAuthDeviceAuthorization? Authorization,
    OAuthResourceDefinition? ResourceDefinition,
    string? Error,
    string? ErrorDescription)
{
    public static OAuthDeviceConsentResult Valid(
        OAuthClientOptions client,
        OAuthDeviceAuthorization authorization,
        OAuthResourceDefinition resourceDefinition)
    {
        return new(true, client, authorization, resourceDefinition, null, null);
    }

    public static OAuthDeviceConsentResult Invalid(string error, string description)
    {
        return new(false, null, null, null, error, description);
    }
}

public sealed record OAuthDeviceAuthorizationResult(bool IsSuccess, string? Error, string? ErrorDescription)
{
    public static OAuthDeviceAuthorizationResult Success()
    {
        return new(true, null, null);
    }

    public static OAuthDeviceAuthorizationResult Invalid(string error, string description)
    {
        return new(false, error, description);
    }
}
