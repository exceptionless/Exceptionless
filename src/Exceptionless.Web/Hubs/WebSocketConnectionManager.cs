using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using Exceptionless.Core;
using Foundatio.Serializer;

namespace Exceptionless.Web.Hubs;

public sealed class WebSocketConnectionManager : IDisposable
{
    private static readonly ArraySegment<byte> KeepAliveMessage = new(Encoding.ASCII.GetBytes("{}"), 0, 2);
    private readonly ConcurrentDictionary<string, WebSocket> _connections = new();
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

        foreach (var (connectionId, socket) in _connections)
        {
            if (!CanSend(socket))
            {
                _ = RemoveConnectionAsync(connectionId);
                continue;
            }

            _ = SendKeepAliveAsync(connectionId, socket);
        }
    }

    public WebSocket? GetConnectionById(string connectionId)
    {
        return _connections.TryGetValue(connectionId, out var socket) ? socket : null;
    }

    public ICollection<WebSocket> GetAll()
    {
        return _connections.Values;
    }

    public string AddConnection(WebSocket socket)
    {
        string connectionId = Guid.NewGuid().ToString("N");
        _connections.TryAdd(connectionId, socket);
        AppDiagnostics.PushWebSocketConnectionsOpened.Add(1);
        AppDiagnostics.Gauge("push.connections.websocket.active", _connections.Count);
        return connectionId;
    }

    public async Task RemoveConnectionAsync(string connectionId)
    {
        if (!_connections.TryRemove(connectionId, out var socket))
            return;

        if (!CanSend(socket))
        {
            AppDiagnostics.PushWebSocketConnectionsClosed.Add(1);
            AppDiagnostics.Gauge("push.connections.websocket.active", _connections.Count);
            return;
        }

        try
        {
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by manager", CancellationToken.None).ConfigureAwait(false);
        }
        catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely) { }
        catch (ObjectDisposedException) { }
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

    public bool SendMessage(string connectionId, object message)
    {
        if (!_connections.TryGetValue(connectionId, out var socket))
            return false;

        if (!CanSend(socket))
        {
            _ = RemoveConnectionAsync(connectionId);
            return false;
        }

        _ = SendMessageAsync(connectionId, socket, message);
        return true;
    }

    public void SendMessage(IEnumerable<string> connectionIds, object message)
    {
        foreach (var connectionId in connectionIds)
            SendMessage(connectionId, message);
    }

    public void SendMessageToAll(object message)
    {
        foreach (var (connectionId, socket) in _connections)
        {
            if (!CanSend(socket))
            {
                _ = RemoveConnectionAsync(connectionId);
                continue;
            }

            _ = SendMessageAsync(connectionId, socket, message);
        }
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }

    private async Task SendKeepAliveAsync(string connectionId, WebSocket socket)
    {
        try
        {
            await socket.SendAsync(KeepAliveMessage, WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);
        }
        catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
        {
            await RemoveConnectionAsync(connectionId).ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            await RemoveConnectionAsync(connectionId).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error sending websocket keepalive for {ConnectionId}", connectionId);
        }
    }

    private async Task SendMessageAsync(string connectionId, WebSocket socket, object message)
    {
        try
        {
            string serializedMessage = _serializer.SerializeToString(message);
            byte[] bytes = Encoding.UTF8.GetBytes(serializedMessage);
            await socket.SendAsync(new ArraySegment<byte>(bytes, 0, bytes.Length), WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);
        }
        catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
        {
            await RemoveConnectionAsync(connectionId).ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            await RemoveConnectionAsync(connectionId).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error sending websocket message for {ConnectionId}", connectionId);
        }
    }

    private static bool CanSend(WebSocket socket)
    {
        return socket.State is WebSocketState.Open;
    }
}
