using System.Net.WebSockets;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Utility;

namespace Exceptionless.Web.Hubs;

/// <summary>
/// Temporary WebSocket endpoint compatibility for the Angular rollout. Keep this in place
/// until all clients are on SSE and websocket active connections stay at zero.
/// </summary>
public sealed class WebSocketPushMiddleware
{
    private static readonly PathString PushEndpoint = new("/api/v2/push");
    private readonly ILogger _logger;
    private readonly WebSocketConnectionManager _connectionManager;
    private readonly IConnectionLeaseStore _leaseStore;
    private readonly PushConnectionRegistry _connectionRegistry;
    private readonly TimeProvider _timeProvider;
    private readonly IHostApplicationLifetime _applicationLifetime;
    private readonly RequestDelegate _next;

    public WebSocketPushMiddleware(RequestDelegate next, WebSocketConnectionManager connectionManager, IConnectionLeaseStore leaseStore, PushConnectionRegistry connectionRegistry, TimeProvider timeProvider, IHostApplicationLifetime applicationLifetime, ILogger<WebSocketPushMiddleware> logger)
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
        if (!context.Request.Path.StartsWithSegments(PushEndpoint, StringComparison.Ordinal)
            || !context.WebSockets.IsWebSocketRequest)
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
            _logger.LogWarning("User {UserId} exceeded max websocket push connections ({Max})", userId, _connectionManager.MaxConnectionsPerUser);
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

            WebSocket? socket = null;

            try
            {
                socket = await context.WebSockets.AcceptWebSocketAsync();
                _connectionManager.AddConnection(connectionId, socket);
                _logger.LogTrace("WebSocket push connected {ConnectionId}", connectionId);
                await ReceiveUntilCloseAsync(socket, connectionLifetime.Token).ConfigureAwait(false);
            }
            catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
            {
                _logger.LogDebug(ex, "WebSocket push closed prematurely for {ConnectionId}", connectionId);
            }
            catch (OperationCanceledException ex)
            {
                _logger.LogDebug(ex, "WebSocket push canceled for {ConnectionId}", connectionId);
            }
            finally
            {
                _logger.LogTrace("WebSocket push disconnected {ConnectionId}", connectionId);
                try
                {
                    await _connectionManager.RemoveConnectionAsync(connectionId).ConfigureAwait(false);
                }
                finally
                {
                    socket?.Dispose();
                    _connectionRegistry.Unregister(connectionId);
                }
            }
        }
    }

    private static async Task ReceiveUntilCloseAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];

        while (socket.State is WebSocketState.Open)
        {
            WebSocketReceiveResult result;
            do
            {
                result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken).ConfigureAwait(false);
                if (result.MessageType is WebSocketMessageType.Close)
                    return;
            } while (!result.EndOfMessage);
        }
    }
}
