using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using Exceptionless.Core;
using Foundatio.Serializer;

namespace Exceptionless.Web.Hubs;

/// <summary>
/// Temporary WebSocket compatibility layer for the Angular rollout. Remove once the
/// SSE rollout is complete and the websocket active-connection gauge remains at zero.
/// </summary>
public sealed class WebSocketConnectionManager : IDisposable
{
    private static readonly ArraySegment<byte> KeepAliveMessage = new(Encoding.ASCII.GetBytes("{}"), 0, 2);
    private readonly ConcurrentDictionary<string, ManagedWebSocket> _connections = new();
    private readonly Timer? _timer;
    private readonly ITextSerializer _serializer;
    private readonly ILogger _logger;

    public int MaxConnectionsPerUser { get; init; } = 10;
    public int ConnectionCount => _connections.Count;

    public WebSocketConnectionManager(AppOptions options, ITextSerializer serializer, ILoggerFactory loggerFactory)
    {
        _serializer = serializer;
        _logger = loggerFactory.CreateLogger<WebSocketConnectionManager>();

        if (!options.EnablePush)
            return;

        _timer = new Timer(SendKeepAlive, null, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(15));
    }

    private void SendKeepAlive(object? state)
    {
        if (_connections.IsEmpty)
            return;

        foreach (var (connectionId, connection) in _connections)
        {
            if (!CanSend(connection.Socket))
            {
                _ = RemoveConnectionAsync(connectionId);
                continue;
            }

            _ = SendKeepAliveAsync(connectionId, connection);
        }
    }

    public WebSocket? GetConnectionById(string connectionId)
    {
        return _connections.TryGetValue(connectionId, out var connection) ? connection.Socket : null;
    }

    public WebSocket? GetWebSocketById(string connectionId)
    {
        return GetConnectionById(connectionId);
    }

    public ICollection<WebSocket> GetAll()
    {
        return _connections.Values.Select(connection => connection.Socket).ToArray();
    }

    public string GetConnectionId(WebSocket socket)
    {
        return _connections.FirstOrDefault(pair => pair.Value.Socket == socket).Key;
    }

    public string AddConnection(WebSocket socket)
    {
        string connectionId = Guid.NewGuid().ToString("N");
        return AddConnection(connectionId, socket);
    }

    public string AddConnection(string connectionId, WebSocket socket)
    {
        if (!_connections.TryAdd(connectionId, new ManagedWebSocket(socket)))
            throw new InvalidOperationException($"A websocket connection with id '{connectionId}' is already registered.");

        AppDiagnostics.PushWebSocketConnectionsOpened.Add(1);
        AppDiagnostics.Gauge("push.connections.websocket.active", _connections.Count);
        return connectionId;
    }

    public string AddWebSocket(WebSocket socket)
    {
        return AddConnection(socket);
    }

    public async Task RemoveConnectionAsync(string connectionId)
    {
        if (!_connections.TryRemove(connectionId, out var connection))
            return;

        var socket = connection.Socket;

        if (!CanClose(socket))
        {
            AppDiagnostics.PushWebSocketConnectionsClosed.Add(1);
            AppDiagnostics.Gauge("push.connections.websocket.active", _connections.Count);
            return;
        }

        await connection.SendLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by manager", CancellationToken.None).ConfigureAwait(false);
        }
        catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
        {
            _logger.LogDebug(ex, "WebSocket closed prematurely for {ConnectionId}", connectionId);
        }
        catch (ObjectDisposedException ex)
        {
            _logger.LogDebug(ex, "WebSocket was already disposed for {ConnectionId}", connectionId);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error closing websocket {ConnectionId}", connectionId);
        }
        finally
        {
            AppDiagnostics.PushWebSocketConnectionsClosed.Add(1);
            AppDiagnostics.Gauge("push.connections.websocket.active", _connections.Count);
            connection.SendLock.Release();
        }
    }

    public Task RemoveWebSocketAsync(string connectionId)
    {
        return RemoveConnectionAsync(connectionId);
    }

    public bool SendMessage(string connectionId, object message)
    {
        if (!_connections.TryGetValue(connectionId, out var connection))
            return false;

        if (!CanSend(connection.Socket))
        {
            _ = RemoveConnectionAsync(connectionId);
            return false;
        }

        _ = SendMessageAsync(connectionId, connection, message);
        return true;
    }

    public Task SendMessageAsync(string connectionId, object message)
    {
        SendMessage(connectionId, message);
        return Task.CompletedTask;
    }

    public void SendMessage(IEnumerable<string> connectionIds, object message)
    {
        foreach (var connectionId in connectionIds)
            SendMessage(connectionId, message);
    }

    public Task SendMessageAsync(IEnumerable<string> connectionIds, object message)
    {
        SendMessage(connectionIds, message);
        return Task.CompletedTask;
    }

    public void SendMessageToAll(object message)
    {
        foreach (var (connectionId, connection) in _connections)
        {
            if (!CanSend(connection.Socket))
            {
                _ = RemoveConnectionAsync(connectionId);
                continue;
            }

            _ = SendMessageAsync(connectionId, connection, message);
        }
    }

    public Task SendMessageToAllAsync(object message, bool throwOnError = true)
    {
        SendMessageToAll(message);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }

    private Task SendKeepAliveAsync(string connectionId, ManagedWebSocket connection)
    {
        return SendAsync(connectionId, connection, KeepAliveMessage, "keepalive");
    }

    private async Task SendAsync(string connectionId, ManagedWebSocket connection, ArraySegment<byte> bytes, string operation)
    {
        await connection.SendLock.WaitAsync().ConfigureAwait(false);
        bool removeConnection = false;
        try
        {
            if (CanSend(connection.Socket))
                await connection.Socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);
        }
        catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
        {
            removeConnection = true;
        }
        catch (ObjectDisposedException)
        {
            removeConnection = true;
        }
        catch (WebSocketException ex)
        {
            _logger.LogDebug(ex, "Error sending websocket {Operation} for {ConnectionId}", operation, connectionId);
        }
        finally
        {
            connection.SendLock.Release();
        }

        if (removeConnection)
            await RemoveConnectionAsync(connectionId).ConfigureAwait(false);
    }

    private Task SendMessageAsync(string connectionId, ManagedWebSocket connection, object message)
    {
        try
        {
            string serializedMessage = _serializer.SerializeToString(message);
            byte[] bytes = Encoding.UTF8.GetBytes(serializedMessage);
            return SendAsync(connectionId, connection, new ArraySegment<byte>(bytes), "message");
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogDebug(ex, "Error sending websocket message for {ConnectionId}", connectionId);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogDebug(ex, "Error sending websocket message for {ConnectionId}", connectionId);
        }
        catch (NotSupportedException ex)
        {
            _logger.LogDebug(ex, "Error sending websocket message for {ConnectionId}", connectionId);
        }
        catch (EncoderFallbackException ex)
        {
            _logger.LogDebug(ex, "Error sending websocket message for {ConnectionId}", connectionId);
        }

        return Task.CompletedTask;
    }

    private static bool CanSend(WebSocket socket)
    {
        return socket.State is WebSocketState.Open;
    }

    private static bool CanClose(WebSocket socket)
    {
        return socket.State is WebSocketState.Open or WebSocketState.CloseReceived;
    }

    private sealed record ManagedWebSocket(WebSocket Socket)
    {
        public SemaphoreSlim SendLock { get; } = new(1, 1);
    }
}
