using System.Net.WebSockets;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Utility;

namespace Exceptionless.Web.Hubs;

public sealed class WebSocketPushMiddleware
{
    private static readonly PathString PushEndpoint = new("/api/v2/push");
    private readonly ILogger _logger;
    private readonly WebSocketConnectionManager _connectionManager;
    private readonly IConnectionMapping _connectionMapping;
    private readonly RequestDelegate _next;

    public WebSocketPushMiddleware(RequestDelegate next, WebSocketConnectionManager connectionManager, IConnectionMapping connectionMapping, ILogger<WebSocketPushMiddleware> logger)
    {
        _next = next;
        _connectionManager = connectionManager;
        _connectionMapping = connectionMapping;
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

        var existingConnections = await _connectionMapping.GetUserIdConnectionsAsync(userId);
        if (existingConnections.Count >= _connectionManager.MaxConnectionsPerUser)
        {
            _logger.LogWarning("User {UserId} exceeded max websocket push connections ({Max})", userId, _connectionManager.MaxConnectionsPerUser);
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            return;
        }

        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        string connectionId = _connectionManager.AddConnection(socket);
        await OnConnected(context, connectionId).ConfigureAwait(false);

        try
        {
            await ReceiveUntilCloseAsync(socket, context.RequestAborted).ConfigureAwait(false);
        }
        catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely) { }
        catch (OperationCanceledException) { }
        finally
        {
            await OnDisconnected(context, connectionId).ConfigureAwait(false);
            await _connectionManager.RemoveConnectionAsync(connectionId).ConfigureAwait(false);
        }
    }

    private async Task OnConnected(HttpContext context, string connectionId)
    {
        _logger.LogTrace("WebSocket push connected {ConnectionId}", connectionId);

        foreach (string organizationId in context.User.GetOrganizationIds())
            await _connectionMapping.GroupAddAsync(organizationId, connectionId).ConfigureAwait(false);

        string? userId = context.User.GetUserId();
        if (!String.IsNullOrEmpty(userId))
            await _connectionMapping.UserIdAddAsync(userId, connectionId).ConfigureAwait(false);
    }

    private async Task OnDisconnected(HttpContext context, string connectionId)
    {
        _logger.LogTrace("WebSocket push disconnected {ConnectionId}", connectionId);

        foreach (string organizationId in context.User.GetOrganizationIds())
            await _connectionMapping.GroupRemoveAsync(organizationId, connectionId).ConfigureAwait(false);

        string? userId = context.User.GetUserId();
        if (!String.IsNullOrEmpty(userId))
            await _connectionMapping.UserIdRemoveAsync(userId, connectionId).ConfigureAwait(false);
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
