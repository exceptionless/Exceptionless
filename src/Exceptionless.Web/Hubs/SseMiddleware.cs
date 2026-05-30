using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;

namespace Exceptionless.Web.Hubs;

/// <summary>
/// Handles SSE connections at /api/v2/push. Replaces MessageBusBrokerMiddleware (WebSocket).
/// Accepts authenticated GET requests, sets SSE response headers, registers the connection
/// with IConnectionMapping, and holds the response open until the client disconnects.
/// </summary>
public class SseMiddleware
{
    private static readonly PathString _sseEndpoint = new("/api/v2/push");
    private readonly ILogger _logger;
    private readonly SseConnectionManager _connectionManager;
    private readonly IConnectionMapping _connectionMapping;
    private readonly IUserRepository _userRepository;
    private readonly RequestDelegate _next;

    public SseMiddleware(RequestDelegate next, SseConnectionManager connectionManager, IConnectionMapping connectionMapping, IUserRepository userRepository, ILogger<SseMiddleware> logger)
    {
        _next = next;
        _connectionManager = connectionManager;
        _connectionMapping = connectionMapping;
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments(_sseEndpoint, StringComparison.Ordinal)
            || !HttpMethods.IsGet(context.Request.Method)
            || context.WebSockets.IsWebSocketRequest)
        {
            await _next(context);
            return;
        }

        if (!context.User.IsAuthenticated())
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        string? userId = context.User.GetUserId();
        if (String.IsNullOrEmpty(userId))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        // Enforce per-user connection limit
        var existingConnections = await _connectionMapping.GetUserIdConnectionsAsync(userId);
        if (existingConnections.Count >= _connectionManager.MaxConnectionsPerUser)
        {
            _logger.LogWarning("User {UserId} exceeded max SSE connections ({Max})", userId, _connectionManager.MaxConnectionsPerUser);
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            return;
        }

        // Set SSE response headers
        context.Response.Headers.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache, no-store";
        context.Response.Headers["X-Accel-Buffering"] = "no"; // nginx

        // Disable response buffering
        var bufferingFeature = context.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpResponseBodyFeature>();
        bufferingFeature?.DisableBuffering();

        string connectionId = Guid.NewGuid().ToString("N");
        SseConnection? connection = null;

        try
        {
            connection = _connectionManager.AddConnection(connectionId, context.Response, context.RequestAborted);
            await OnConnected(context, connectionId).ConfigureAwait(false);

            // Send initial connected event
            connection.TryWrite(new { type = "Connected", message = new { connection_id = connectionId } });

            // Hold the response open until the client disconnects or the connection is aborted
            await Task.Delay(Timeout.Infinite, connection.ConnectionAborted).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogDebug(ex, "SSE request ended for {ConnectionId}", connectionId);
        }
        finally
        {
            if (connection is not null)
            {
                try
                {
                    await OnDisconnected(context, connectionId).ConfigureAwait(false);
                }
                finally
                {
                    await _connectionManager.RemoveConnectionAsync(connectionId).ConfigureAwait(false);
                }
            }
        }
    }

    private async Task OnConnected(HttpContext context, string connectionId)
    {
        _logger.LogTrace("SSE connected {ConnectionId}", connectionId);
        foreach (string organizationId in context.User.GetOrganizationIds())
        {
            await _connectionMapping.GroupAddAsync(organizationId, connectionId).ConfigureAwait(false);
            await _connectionMapping.ConnectionGroupAddAsync(connectionId, organizationId).ConfigureAwait(false);
        }

        string? userId = context.User.GetUserId();
        if (!String.IsNullOrEmpty(userId))
            await _connectionMapping.UserIdAddAsync(userId, connectionId).ConfigureAwait(false);
    }

    private async Task OnDisconnected(HttpContext context, string connectionId)
    {
        _logger.LogTrace("SSE disconnected {ConnectionId}", connectionId);

        try
        {
            foreach (string organizationId in await PushDisconnectCleanup.GetOrganizationIdsAsync(context.User, connectionId, _connectionMapping, () => _userRepository.GetByIdAsync(context.User.GetUserId()!), _logger).ConfigureAwait(false))
            {
                await _connectionMapping.GroupRemoveAsync(organizationId, connectionId).ConfigureAwait(false);
                await _connectionMapping.ConnectionGroupRemoveAsync(connectionId, organizationId).ConfigureAwait(false);
            }

            string? userId = context.User.GetUserId();
            if (!String.IsNullOrEmpty(userId))
                await _connectionMapping.UserIdRemoveAsync(userId, connectionId).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogDebug(ex, "SSE disconnect was canceled for {ConnectionId}", connectionId);
        }
        catch (ObjectDisposedException ex)
        {
            _logger.LogDebug(ex, "SSE disconnect raced with disposal for {ConnectionId}", connectionId);
        }
    }

}
