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
    private readonly ConcurrentDictionary<string, ManagedConnection> _connections = new();
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
        return _connections.Values.Select(static connection => connection.Socket).ToArray();
    }

    public string GetConnectionId(WebSocket socket)
    {
        return _connections.FirstOrDefault(pair => pair.Value.Socket == socket).Key;
    }

    public string AddConnection(WebSocket socket)
    {
        string connectionId = Guid.NewGuid().ToString("N");
        _connections.TryAdd(connectionId, new ManagedConnection(socket));
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

        try
        {
            await connection.CloseAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
        {
            _logger.LogDebug(ex, "Websocket {ConnectionId} closed before manager shutdown completed", connectionId);
        }
        catch (ObjectDisposedException ex)
        {
            _logger.LogDebug(ex, "Websocket {ConnectionId} was already disposed during shutdown", connectionId);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error closing websocket {ConnectionId}", connectionId);
        }
        finally
        {
            AppDiagnostics.PushWebSocketConnectionsClosed.Add(1);
            AppDiagnostics.Gauge("push.connections.websocket.active", _connections.Count);
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

    private async Task SendKeepAliveAsync(string connectionId, ManagedConnection connection)
    {
        try
        {
            if (!await connection.SendAsync(KeepAliveMessage, WebSocketMessageType.Text, CancellationToken.None).ConfigureAwait(false))
                await RemoveConnectionAsync(connectionId).ConfigureAwait(false);
        }
        catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
        {
            await RemoveConnectionAsync(connectionId).ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            await RemoveConnectionAsync(connectionId).ConfigureAwait(false);
        }
        catch (WebSocketException ex)
        {
            _logger.LogDebug(ex, "Error sending websocket keepalive for {ConnectionId}", connectionId);
            await RemoveConnectionAsync(connectionId).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogDebug(ex, "Error sending websocket keepalive for {ConnectionId}", connectionId);
            await RemoveConnectionAsync(connectionId).ConfigureAwait(false);
        }
    }

    private async Task SendMessageAsync(string connectionId, ManagedConnection connection, object message)
    {
        try
        {
            string serializedMessage = _serializer.SerializeToString(message);
            byte[] bytes = Encoding.UTF8.GetBytes(serializedMessage);
            if (!await connection.SendAsync(new ArraySegment<byte>(bytes, 0, bytes.Length), WebSocketMessageType.Text, CancellationToken.None).ConfigureAwait(false))
                await RemoveConnectionAsync(connectionId).ConfigureAwait(false);
        }
        catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
        {
            await RemoveConnectionAsync(connectionId).ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            await RemoveConnectionAsync(connectionId).ConfigureAwait(false);
        }
        catch (WebSocketException ex)
        {
            _logger.LogDebug(ex, "Error sending websocket message for {ConnectionId}", connectionId);
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
    }

    private static bool CanSend(WebSocket socket)
    {
        return socket.State is WebSocketState.Open;
    }

    private static bool CanClose(WebSocket socket)
    {
        return socket.State is WebSocketState.Open or WebSocketState.CloseReceived;
    }

    private sealed class ManagedConnection
    {
        private readonly SemaphoreSlim _sendLock = new(1, 1);

        public ManagedConnection(WebSocket socket)
        {
            Socket = socket;
        }

        public WebSocket Socket { get; }

        public async Task<bool> CloseAsync(CancellationToken cancellationToken)
        {
            await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (!CanClose(Socket))
                    return false;

                await Socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by manager", cancellationToken).ConfigureAwait(false);
                return true;
            }
            finally
            {
                _sendLock.Release();
            }
        }

        public async Task<bool> SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, CancellationToken cancellationToken)
        {
            await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (!CanSend(Socket))
                    return false;

                await Socket.SendAsync(buffer, messageType, true, cancellationToken).ConfigureAwait(false);
                return true;
            }
            finally
            {
                _sendLock.Release();
            }
        }
    }
}
