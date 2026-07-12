using Exceptionless.Core.Extensions;
using Exceptionless.Core.Utility;

namespace Exceptionless.Web.Hubs;

/// <summary>
/// Handles SSE connections at /api/v2/push. Replaces MessageBusBrokerMiddleware (WebSocket).
/// Accepts authenticated GET requests, sets SSE response headers, registers the connection
/// in the process-local ownership registry, and holds the response open until disconnect.
/// </summary>
public class SseMiddleware
{
    private static readonly PathString _sseEndpoint = new("/api/v2/push");
    private readonly ILogger _logger;
    private readonly SseConnectionManager _connectionManager;
    private readonly IConnectionLeaseStore _leaseStore;
    private readonly PushConnectionRegistry _connectionRegistry;
    private readonly TimeProvider _timeProvider;
    private readonly IHostApplicationLifetime _applicationLifetime;
    private readonly RequestDelegate _next;

    public SseMiddleware(RequestDelegate next, SseConnectionManager connectionManager, IConnectionLeaseStore leaseStore, PushConnectionRegistry connectionRegistry, TimeProvider timeProvider, IHostApplicationLifetime applicationLifetime, ILogger<SseMiddleware> logger)
    {
        _next = next;
        _connectionManager = connectionManager;
        _leaseStore = leaseStore;
        _connectionRegistry = connectionRegistry;
        _timeProvider = timeProvider;
        _applicationLifetime = applicationLifetime;
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

        string connectionId = Guid.NewGuid().ToString("N");
        PushConnectionLease? lease;
        try
        {
            lease = await PushConnectionLease.TryAcquireAsync(_leaseStore, _timeProvider, _logger, userId, connectionId, _connectionManager.MaxConnectionsPerUser).ConfigureAwait(false);
        }
        catch (ConnectionLeaseStoreException ex)
        {
            _logger.LogError(ex, "Push lease store is unavailable");
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return;
        }

        if (lease is null)
        {
            _logger.LogWarning("User {UserId} exceeded max SSE connections ({Max})", userId, _connectionManager.MaxConnectionsPerUser);
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            return;
        }

        await using (lease)
        using (var connectionLifetime = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted, lease.LeaseLost, _applicationLifetime.ApplicationStopping))
        {
            if (!_connectionRegistry.TryRegister(connectionId, userId, context.User.GetLoggedInUsersTokenId(), context.User.GetOrganizationIds()))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            SseConnection? connection = null;

            try
            {
                // Set SSE response headers
                context.Response.Headers.ContentType = "text/event-stream";
                context.Response.Headers.CacheControl = "no-cache, no-store";
                context.Response.Headers["X-Accel-Buffering"] = "no"; // nginx

                // Disable response buffering
                var bufferingFeature = context.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpResponseBodyFeature>();
                bufferingFeature?.DisableBuffering();

                connection = _connectionManager.AddConnection(connectionId, context.Response, connectionLifetime.Token);
                _logger.LogTrace("SSE connected {ConnectionId}", connectionId);

                // Send initial connected event
                connection.TryWrite(new { type = "Connected", message = new { connection_id = connectionId } });

                // Hold the response open until the client disconnects or the connection is aborted
                await Task.Delay(Timeout.Infinite, connection.ConnectionAborted).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
            finally
            {
                _logger.LogTrace("SSE disconnected {ConnectionId}", connectionId);
                if (connection is not null)
                    await _connectionManager.RemoveConnectionAsync(connectionId).ConfigureAwait(false);
                _connectionRegistry.Unregister(connectionId);
            }
        }
    }
}
