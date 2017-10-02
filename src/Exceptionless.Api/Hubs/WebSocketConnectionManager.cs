using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Exceptionless.Api.Hubs {
    public class WebSocketConnectionManager : IDisposable {
        private static readonly ArraySegment<byte> KeepAliveMessage = new ArraySegment<byte>(Encoding.ASCII.GetBytes("{}"), 0, 2);
        private readonly ConcurrentDictionary<string, WebSocket> _connections = new ConcurrentDictionary<string, WebSocket>();
        private readonly Timer _timer;
        private readonly JsonSerializerSettings _serializerSettings;
        private readonly ILogger _logger;

        public WebSocketConnectionManager(JsonSerializerSettings serializerSettings, ILogger<WebSocketConnectionManager> logger) {
            _serializerSettings = serializerSettings;
            _logger = logger;
            _timer = new Timer(KeepAlive, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
        }

        private void KeepAlive(object state) {
            var sockets = GetAll();
            var openSockets = sockets.Where(s => s.State == WebSocketState.Open).ToArray();
            if (_logger.IsEnabled(LogLevel.Trace))
                _logger.LogTrace("Sending websocket keep alive to {OpenSocketsCount} open connections of {SocketCount} total connections", openSockets.Length, sockets.Count);
            foreach (var socket in openSockets) {
                try {
                    socket.SendAsync(buffer: KeepAliveMessage,
                        messageType: WebSocketMessageType.Text,
                        endOfMessage: true,
                        cancellationToken: CancellationToken.None).GetAwaiter().GetResult();
                } catch (WebSocketException ex) {
                    if (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely) {
                        try {
                            // NOTE: This will not remove it from the ConnectionMappings.
                            RemoveWebSocketAsync(socket).GetAwaiter().GetResult();
                        } catch { }
                    } else {
                        _logger.LogError(ex, "Error sending keep alive socket message: {Message}", ex.Message);
                    }
                }
            }
        }

        public WebSocket GetWebSocketById(string connectionId) {
            return _connections.TryGetValue(connectionId, out WebSocket socket) ? socket : null;
        }

        public ICollection<WebSocket> GetAll() {
            return _connections.Values;
        }

        public string GetConnectionId(WebSocket socket) {
            return _connections.FirstOrDefault(p => p.Value == socket).Key;
        }

        public string AddWebSocket(WebSocket socket) {
            string connectionId = Guid.NewGuid().ToString("N");
            _connections.TryAdd(connectionId, socket);
            return connectionId;
        }

        public async Task RemoveWebSocketAsync(WebSocket socket) {
            string id = GetConnectionId(socket);
            if (!String.IsNullOrEmpty(id) && !_connections.TryRemove(id, out var _))
                return;

            try {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by manager", CancellationToken.None);
            } catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely) { }
        }

        public async Task RemoveWebSocketAsync(string id) {
            if (!_connections.TryRemove(id, out var socket))
                return;

            try {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by manager", CancellationToken.None);
            } catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely) { }
        }

        public async Task SendMessageAsync(WebSocket socket, object message) {
            if (socket == null || socket.State != WebSocketState.Open)
                return;

            var serializedMessage = JsonConvert.SerializeObject(message, _serializerSettings);
            try {
                await socket.SendAsync(buffer: new ArraySegment<byte>(Encoding.ASCII.GetBytes(serializedMessage), 0, serializedMessage.Length),
                    messageType: WebSocketMessageType.Text,
                    endOfMessage: true,
                    cancellationToken: CancellationToken.None);
            } catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely) { }
        }

        public Task SendMessageAsync(string connectionId, object message) {
            return SendMessageAsync(GetWebSocketById(connectionId), message);
        }

        public Task SendMessageAsync(IEnumerable<string> connectionIds, object message) {
            return Task.WhenAll(connectionIds.Select(id => SendMessageAsync(GetWebSocketById(id), message)));
        }

        public async Task SendMessageToAllAsync(object message, bool throwOnError = true) {
            foreach (var socket in GetAll()) {
                if (socket.State != WebSocketState.Open)
                    continue;

                try {
                    await SendMessageAsync(socket, message);
                } catch (Exception) {
                    if (throwOnError)
                        throw;
                }
            }
        }

        public void Dispose() {
            _timer?.Dispose();
        }
    }
}