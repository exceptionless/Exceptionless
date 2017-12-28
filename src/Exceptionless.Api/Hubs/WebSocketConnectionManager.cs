using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core;
using Foundatio.Queues;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Exceptionless.Api.Hubs {
    public class WebSocketConnectionManager : IDisposable {
        private static readonly ArraySegment<byte> _keepAliveMessage = new ArraySegment<byte>(Encoding.ASCII.GetBytes("{}"), 0, 2);
        private readonly ConcurrentDictionary<string, WebSocket> _connections = new ConcurrentDictionary<string, WebSocket>();
        private readonly TaskQueue _taskQueue; 
        private readonly Timer _timer;
        private readonly JsonSerializerSettings _serializerSettings;
        private readonly ILogger _logger;

        public WebSocketConnectionManager(JsonSerializerSettings serializerSettings, ILoggerFactory loggerFactory) {
            _serializerSettings = serializerSettings;
            _logger = loggerFactory.CreateLogger<WebSocketConnectionManager>();
            if (!Settings.Current.EnableWebSockets)
                return;

            _taskQueue = new TaskQueue(maxDegreeOfParallelism: 1, loggerFactory: loggerFactory); 
            _timer = new Timer(KeepAlive, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
        }

        private void KeepAlive(object state) {
            if (_connections.IsEmpty && _connections.Count == 0) 
                return; 

            _taskQueue.Enqueue(async () => { 
                var sockets = GetAll();
                var openSockets = sockets.Where(s => s.State == WebSocketState.Open).ToArray();
                if (_logger.IsEnabled(LogLevel.Trace))
                    _logger.LogTrace("Sending web socket keep alive to {OpenSocketsCount} open connections of {SocketCount} total connections", openSockets.Length, sockets.Count);

                foreach (var socket in openSockets) {
                    try {
                        await socket.SendAsync(buffer: _keepAliveMessage,
                            messageType: WebSocketMessageType.Text,
                            endOfMessage: true,
                            cancellationToken: CancellationToken.None);
                    } catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely) {
                        // NOTE: This will not remove it from the ConnectionMappings.
                        await RemoveWebSocketAsync(socket);
                    } catch (Exception ex) {
                        _logger.LogError(ex, "Error sending keep alive socket message: {Message}", ex.Message);
                    } 
                }
            });
        }

        public WebSocket GetWebSocketById(string connectionId) {
            return _connections.TryGetValue(connectionId, out var socket) ? socket : null;
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

        private Task RemoveWebSocketAsync(WebSocket socket) {
            string id = GetConnectionId(socket);
            if (String.IsNullOrEmpty(id) || !_connections.TryRemove(id, out var _))
                return Task.CompletedTask;

            return CloseWebSocketAsync(socket);
        }

        public Task RemoveWebSocketAsync(string id) {
            if (!_connections.TryRemove(id, out var socket))
                return Task.CompletedTask;

            return CloseWebSocketAsync(socket);
        }

        private async Task CloseWebSocketAsync(WebSocket socket) {
            if (!CanSendWebSocketMessage(socket))
                return;

            try {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by manager", CancellationToken.None);
            } catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely) { } catch (Exception ex) {
                _logger.LogError(ex, "Error closing web socket: {Message}", ex.Message);
            }
        }

        private Task SendMessageAsync(WebSocket socket, object message) {
            if (!CanSendWebSocketMessage(socket))
                return Task.CompletedTask;

            string serializedMessage = JsonConvert.SerializeObject(message, _serializerSettings);
            _taskQueue.Enqueue(async () => { 
                if (!CanSendWebSocketMessage(socket)) 
                    return; 
 
                try {
                    await socket.SendAsync(buffer: new ArraySegment<byte>(Encoding.ASCII.GetBytes(serializedMessage), 0, serializedMessage.Length),
                        messageType: WebSocketMessageType.Text,
                        endOfMessage: true,
                        cancellationToken: CancellationToken.None);
                } catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely) {
                } catch (Exception ex) {
                    _logger.LogError(ex, "Error sending socket message: {Message}", ex.Message);
                }
            });
 
            return Task.CompletedTask;
        }

        public Task SendMessageAsync(string connectionId, object message) {
            return SendMessageAsync(GetWebSocketById(connectionId), message);
        }

        public Task SendMessageAsync(IEnumerable<string> connectionIds, object message) {
            return Task.WhenAll(connectionIds.Select(id => SendMessageAsync(GetWebSocketById(id), message)));
        }

        public async Task SendMessageToAllAsync(object message, bool throwOnError = true) {
            foreach (var socket in GetAll()) {
                if (!CanSendWebSocketMessage(socket))
                    continue;

                try {
                    await SendMessageAsync(socket, message);
                } catch (Exception) {
                    if (throwOnError)
                        throw;
                }
            }
        }

        private bool CanSendWebSocketMessage(WebSocket socket) {
            return socket != null && socket.State != WebSocketState.Aborted && socket.State != WebSocketState.Closed && socket.State != WebSocketState.CloseSent;
        } 

        public void Dispose() {
            _timer?.Dispose();
            _taskQueue?.Dispose();
        }
    }
}