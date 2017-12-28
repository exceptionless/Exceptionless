using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Utility;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Api.Hubs {
    public class MessageBusBrokerMiddleware {
        private readonly ILogger _logger;
        private readonly WebSocketConnectionManager _connectionManager;
        private readonly IConnectionMapping _connectionMapping;
        private readonly RequestDelegate _next;

        public MessageBusBrokerMiddleware(RequestDelegate next, WebSocketConnectionManager connectionManager, IConnectionMapping connectionMapping, ILogger<MessageBusBrokerMiddleware> logger) {
            _next = next;
            _connectionManager = connectionManager;
            _connectionMapping = connectionMapping;
            _logger = logger;
        }

        public async Task Invoke(HttpContext context) {
            if (!context.WebSockets.IsWebSocketRequest || !context.User.Identity.IsAuthenticated) {
                await _next(context);
                return;
            }

            using(var socket = await context.WebSockets.AcceptWebSocketAsync()) {
                string connectionId = _connectionManager.AddWebSocket(socket);
                await OnConnected(context, socket, connectionId);
                bool disconnected = false;

                try {
                    await ReceiveAsync(socket, async (result, data) => {
                        switch (result.MessageType) {
                            case WebSocketMessageType.Text:
                                if (_logger.IsEnabled(LogLevel.Trace))
                                    _logger.LogTrace("WebSocket got message {ConnectionId}", connectionId);
                                // ignore incoming messages
                                return;
                            case WebSocketMessageType.Close:
                                await OnDisconnected(context, socket, connectionId);
                                await _connectionManager.RemoveWebSocketAsync(connectionId);
                                disconnected = true;
                                return;
                        }
                    });
                } catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely) { }

                // This will be hit when the connection is lost.
                if (!disconnected) {
                    await OnDisconnected(context, socket, connectionId);
                    await _connectionManager.RemoveWebSocketAsync(connectionId);
                }
            }
        }

        private async Task OnConnected(HttpContext context, WebSocket socket, string connectionId) {
            if (_logger.IsEnabled(LogLevel.Trace))
                _logger.LogTrace("WebSocket connected {ConnectionId} ({State})", connectionId, socket?.State);

            try {
                foreach (string organizationId in context.User.GetOrganizationIds())
                    await _connectionMapping.GroupAddAsync(organizationId, connectionId);

                await _connectionMapping.UserIdAddAsync(context.User.GetUserId(), connectionId);
            } catch (Exception ex) {
                _logger.LogError(ex, "OnConnected Error: {Message}", ex.Message);
                throw;
            }
        }

        private async Task OnDisconnected(HttpContext context, WebSocket socket, string connectionId) {
            if (_logger.IsEnabled(LogLevel.Trace))
                _logger.LogTrace("WebSocket disconnected {ConnectionId} ({State})", connectionId, socket?.State);

            try {
                foreach (string organizationId in context.User.GetOrganizationIds())
                    await _connectionMapping.GroupRemoveAsync(organizationId, connectionId);

                await _connectionMapping.UserIdRemoveAsync(context.User.GetUserId(), connectionId);
            } catch (Exception ex) {
                _logger.LogError(ex, "OnDisconnected Error: {Message}", ex.Message);
                throw;
            }
        }

        private async Task ReceiveAsync(WebSocket socket, Action<WebSocketReceiveResult, string> handleMessage) {
            var buffer = new ArraySegment<byte>(new byte[1024 * 4]);
            var result = await socket.ReceiveAsync(buffer, CancellationToken.None);
            LogFrame(result, buffer.Array);

            while (!result.CloseStatus.HasValue) {
                string data;

                using (var ms = new MemoryStream()) {
                    do {
                        result = await socket.ReceiveAsync(buffer, CancellationToken.None);
                        LogFrame(result, buffer.Array);

                        await ms.WriteAsync(buffer.Array, buffer.Offset, result.Count);
                    } while (!result.EndOfMessage);

                    ms.Seek(0, SeekOrigin.Begin);

                    using (var reader = new StreamReader(ms, Encoding.UTF8))
                        data = await reader.ReadToEndAsync();
                }

                handleMessage(result, data);
            }
        }

        private void LogFrame(WebSocketReceiveResult frame, byte[] buffer) {
            if (!_logger.IsEnabled(LogLevel.Debug))
                return;

            if (frame.CloseStatus.HasValue) {
                _logger.LogDebug("Close: {CloseStatus} {CloseStatusDescription}", frame.CloseStatus.Value, frame.CloseStatusDescription);
            } else {
                string content = "<<binary>>";
                if (frame.MessageType == WebSocketMessageType.Text)
                    content = Encoding.UTF8.GetString(buffer, 0, frame.Count);

                _logger.LogDebug("Received Frame {MessageType}: length={FrameCount}, end={FrameEndOfMessage}: {Content}", frame.MessageType, frame.Count, frame.EndOfMessage, content);
            }

        }
    }
}