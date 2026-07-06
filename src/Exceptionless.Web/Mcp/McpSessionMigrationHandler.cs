using System.Security.Claims;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Foundatio.Caching;
using Foundatio.Serializer;
using Microsoft.Extensions.Options;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Protocol;

namespace Exceptionless.Web.Mcp;

public sealed class McpSessionMigrationHandler(
    ICacheClient cacheClient,
    ITextSerializer serializer,
    IOptions<HttpServerTransportOptions> transportOptions,
    TimeProvider timeProvider,
    ILogger<McpSessionMigrationHandler> logger) : ISessionMigrationHandler
{
    private const string CacheKeyPrefix = "mcp:session:";
    private const string UserClientId = "user";
    private static readonly TimeSpan LifetimeBuffer = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan DefaultIdleTimeout = TimeSpan.FromHours(2);

    public async ValueTask OnSessionInitializedAsync(HttpContext context, string sessionId, InitializeRequestParams initializeParams, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var sessionIdentity = GetSessionIdentity(context);
        if (sessionIdentity is null)
        {
            logger.LogWarning("Skipping MCP session migration persistence for unauthenticated session {SessionIdHash}", HashSessionId(sessionId));
            return;
        }

        var state = new McpSessionMigrationState
        {
            UserId = sessionIdentity.UserId,
            ClientId = sessionIdentity.ClientId,
            Resource = sessionIdentity.Resource,
            InitializeParams = initializeParams,
            CreatedUtc = timeProvider.GetUtcNow().UtcDateTime
        };

        string serializedState = serializer.SerializeToString(state);
        await cacheClient.SetAsync(GetCacheKey(sessionId), serializedState, GetCacheLifetime());
    }

    public async ValueTask<InitializeRequestParams?> AllowSessionMigrationAsync(HttpContext context, string sessionId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var sessionIdentity = GetSessionIdentity(context);
        if (sessionIdentity is null)
            return null;

        string cacheKey = GetCacheKey(sessionId);
        string? serializedState = await cacheClient.GetAsync<string?>(cacheKey, null);
        if (String.IsNullOrEmpty(serializedState))
            return null;

        McpSessionMigrationState? state;
        try
        {
            state = serializer.Deserialize<McpSessionMigrationState>(serializedState);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to deserialize MCP session migration state for session {SessionIdHash}", HashSessionId(sessionId));
            await cacheClient.RemoveAsync(cacheKey);
            return null;
        }

        if (state is null)
        {
            await cacheClient.RemoveAsync(cacheKey);
            return null;
        }

        if (!Matches(state, sessionIdentity))
        {
            logger.LogWarning("Rejected MCP session migration for session {SessionIdHash} because the authenticated caller changed", HashSessionId(sessionId));
            return null;
        }

        await cacheClient.SetExpirationAsync(cacheKey, GetCacheLifetime());
        return state.InitializeParams;
    }

    private TimeSpan GetCacheLifetime()
    {
        TimeSpan idleTimeout = transportOptions.Value.IdleTimeout;
        if (idleTimeout <= TimeSpan.Zero)
            idleTimeout = DefaultIdleTimeout;

        return idleTimeout + LifetimeBuffer;
    }

    private static bool Matches(McpSessionMigrationState state, McpSessionIdentity sessionIdentity)
    {
        return String.Equals(state.UserId, sessionIdentity.UserId, StringComparison.Ordinal)
            && String.Equals(state.ClientId, sessionIdentity.ClientId, StringComparison.Ordinal)
            && String.Equals(state.Resource, sessionIdentity.Resource, StringComparison.Ordinal);
    }

    private static McpSessionIdentity? GetSessionIdentity(HttpContext context)
    {
        var user = context.User;
        if (!user.IsAuthenticated() || !user.IsInRole(AuthorizationRoles.McpRead))
            return null;

        string? userId = user.GetClaimValue(ClaimTypes.NameIdentifier);
        if (String.IsNullOrWhiteSpace(userId))
            return null;

        string clientId = user.GetClaimValue(IdentityUtils.OAuthClientIdClaim) ?? UserClientId;
        string resource = user.GetClaimValue(IdentityUtils.OAuthResourceClaim) ?? context.Request.Path.ToString();
        return String.IsNullOrWhiteSpace(resource)
            ? null
            : new McpSessionIdentity(userId, clientId, resource);
    }

    private static string GetCacheKey(string sessionId)
    {
        return String.Concat(CacheKeyPrefix, sessionId.ToSHA1());
    }

    private static string HashSessionId(string sessionId)
    {
        return sessionId.ToSHA1();
    }

    private sealed record McpSessionIdentity(string UserId, string ClientId, string Resource);
}

public sealed class McpSessionMigrationState
{
    public required string UserId { get; init; }

    public required string ClientId { get; init; }

    public required string Resource { get; init; }

    public required InitializeRequestParams InitializeParams { get; init; }

    public DateTime CreatedUtc { get; init; }
}
