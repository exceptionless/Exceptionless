using System.Net.WebSockets;
using System.Text;
using Exceptionless.Core;
using Exceptionless.Web.Hubs;
using Foundatio.Serializer;
using Xunit;

namespace Exceptionless.Tests.Hubs;

public sealed class WebSocketConnectionManagerTests : TestWithServices
{
    public WebSocketConnectionManagerTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void AddWebSocket_NewSocket_CanLookupAndEnumerateConnection()
    {
        // Arrange
        using var manager = CreateManager();
        var socket = new TestWebSocket();

        // Act
        string connectionId = manager.AddWebSocket(socket);

        // Assert
        Assert.False(String.IsNullOrEmpty(connectionId));
        Assert.Same(socket, manager.GetWebSocketById(connectionId));
        Assert.Equal(connectionId, manager.GetConnectionId(socket));
        Assert.Same(socket, Assert.Single(manager.GetAll()));
    }

    [Fact]
    public async Task RemoveWebSocketAsync_ExistingConnection_RemovesAndClosesSocket()
    {
        // Arrange
        using var manager = CreateManager();
        var socket = new TestWebSocket();
        string connectionId = manager.AddWebSocket(socket);

        // Act
        await manager.RemoveWebSocketAsync(connectionId);

        // Assert
        Assert.Null(manager.GetWebSocketById(connectionId));
        Assert.Empty(manager.GetAll());
        Assert.Equal(1, socket.CloseCount);
        Assert.Equal(WebSocketState.Closed, socket.State);
    }

    [Fact]
    public async Task RemoveWebSocketAsync_ClosedSocket_RemovesWithoutClosingAgain()
    {
        // Arrange
        using var manager = CreateManager();
        var socket = new TestWebSocket(WebSocketState.Closed);
        string connectionId = manager.AddWebSocket(socket);

        // Act
        await manager.RemoveWebSocketAsync(connectionId);

        // Assert
        Assert.Null(manager.GetWebSocketById(connectionId));
        Assert.Empty(manager.GetAll());
        Assert.Equal(0, socket.CloseCount);
    }

    [Fact]
    public async Task RemoveWebSocketAsync_UnknownConnection_DoesNothing()
    {
        // Arrange
        using var manager = CreateManager();

        // Act
        await manager.RemoveWebSocketAsync("missing");

        // Assert
        Assert.Empty(manager.GetAll());
    }

    [Fact]
    public async Task SendMessageToAllAsync_ClosedSockets_DoesNotSend()
    {
        // Arrange
        using var manager = CreateManager();
        var socket = new TestWebSocket(WebSocketState.Closed);
        manager.AddWebSocket(socket);

        // Act
        await manager.SendMessageToAllAsync(new { type = "test" });

        // Assert
        Assert.Empty(socket.SentMessages);
    }

    private WebSocketConnectionManager CreateManager()
    {
        var options = new AppOptions { EnableWebSockets = false };
        return new WebSocketConnectionManager(options, GetService<ITextSerializer>(), Log);
    }

    private sealed class TestWebSocket : WebSocket
    {
        private WebSocketState _state;

        public TestWebSocket(WebSocketState state = WebSocketState.Open)
        {
            _state = state;
        }

        public int CloseCount { get; private set; }
        public List<string> SentMessages { get; } = [];
        public override WebSocketCloseStatus? CloseStatus { get; } = WebSocketCloseStatus.NormalClosure;
        public override string? CloseStatusDescription { get; } = "Closed";
        public override string? SubProtocol { get; } = null;
        public override WebSocketState State => _state;

        public override void Abort()
        {
            _state = WebSocketState.Aborted;
        }

        public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
        {
            CloseCount++;
            _state = WebSocketState.Closed;
            return Task.CompletedTask;
        }

        public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
        {
            _state = WebSocketState.CloseSent;
            return Task.CompletedTask;
        }

        public override void Dispose() { }

        public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            return Task.FromResult(new WebSocketReceiveResult(0, WebSocketMessageType.Text, true));
        }

        public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        {
            SentMessages.Add(Encoding.ASCII.GetString(buffer.Array!, buffer.Offset, buffer.Count));
            return Task.CompletedTask;
        }
    }
}
