using System.Net.WebSockets;
using System.Reflection;
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
        using var manager = CreateManager();
        var socket = new TestWebSocket();

        string connectionId = manager.AddWebSocket(socket);

        Assert.False(String.IsNullOrEmpty(connectionId));
        Assert.Same(socket, manager.GetWebSocketById(connectionId));
        Assert.Equal(connectionId, manager.GetConnectionId(socket));
        Assert.Same(socket, Assert.Single(manager.GetAll()));
    }

    [Fact]
    public async Task RemoveWebSocketAsync_ExistingConnection_RemovesAndClosesSocket()
    {
        using var manager = CreateManager();
        var socket = new TestWebSocket();
        string connectionId = manager.AddWebSocket(socket);

        await manager.RemoveWebSocketAsync(connectionId);

        Assert.Null(manager.GetWebSocketById(connectionId));
        Assert.Empty(manager.GetAll());
        Assert.Equal(1, socket.CloseCount);
        Assert.Equal(WebSocketState.Closed, socket.State);
    }

    [Fact]
    public async Task RemoveWebSocketAsync_ClosedSocket_RemovesWithoutClosingAgain()
    {
        using var manager = CreateManager();
        var socket = new TestWebSocket(WebSocketState.Closed);
        string connectionId = manager.AddWebSocket(socket);

        await manager.RemoveWebSocketAsync(connectionId);

        Assert.Null(manager.GetWebSocketById(connectionId));
        Assert.Empty(manager.GetAll());
        Assert.Equal(0, socket.CloseCount);
    }

    [Fact]
    public async Task RemoveWebSocketAsync_UnknownConnection_DoesNothing()
    {
        using var manager = CreateManager();

        await manager.RemoveWebSocketAsync("missing");

        Assert.Empty(manager.GetAll());
    }

    [Fact]
    public async Task SendMessageToAllAsync_ClosedSockets_DoesNotSend()
    {
        using var manager = CreateManager();
        var socket = new TestWebSocket(WebSocketState.Closed);
        manager.AddWebSocket(socket);

        await manager.SendMessageToAllAsync(new { type = "test" });

        Assert.Empty(socket.SentMessages);
    }

    [Fact]
    public async Task SendMessage_ConcurrentKeepAlive_DoesNotOverlapSocketSends()
    {
        using var manager = CreateManager();
        var socket = new ConcurrentSendDetectingWebSocket();
        string connectionId = manager.AddWebSocket(socket);

        Assert.True(manager.SendMessage(connectionId, new { type = "test" }));
        await socket.FirstSendStarted.WaitAsync(TestContext.Current.CancellationToken);

        var sendKeepAlive = typeof(WebSocketConnectionManager).GetMethod("SendKeepAlive", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(sendKeepAlive);
        sendKeepAlive!.Invoke(manager, [null]);

        await Task.Delay(100, TestContext.Current.CancellationToken);
        Assert.Equal(0, socket.ConcurrentSendAttempts);

        socket.ReleaseFirstSend();
        await Task.Delay(100, TestContext.Current.CancellationToken);

        Assert.Equal(0, socket.ConcurrentSendAttempts);
        Assert.Equal(2, socket.SentMessages.Count);
        Assert.Contains("\"type\":\"test\"", socket.SentMessages[0]);
        Assert.Equal("{}", socket.SentMessages[1]);
    }

    private WebSocketConnectionManager CreateManager()
    {
        var options = new AppOptions { EnablePush = false };
        return new WebSocketConnectionManager(options, GetService<ITextSerializer>(), Log);
    }

    private sealed class ConcurrentSendDetectingWebSocket : WebSocket
    {
        private readonly TaskCompletionSource<bool> _firstSendStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _releaseFirstSend = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _activeSendCount;
        private int _concurrentSendAttempts;
        private int _sendCount;
        private WebSocketState _state = WebSocketState.Open;

        public Task FirstSendStarted => _firstSendStarted.Task;
        public int ConcurrentSendAttempts => _concurrentSendAttempts;
        public List<string> SentMessages { get; } = [];
        public override WebSocketCloseStatus? CloseStatus => WebSocketCloseStatus.NormalClosure;
        public override string? CloseStatusDescription => "Closed";
        public override string? SubProtocol => null;
        public override WebSocketState State => _state;

        public void ReleaseFirstSend()
        {
            _releaseFirstSend.TrySetResult(true);
        }

        public override void Abort()
        {
            _state = WebSocketState.Aborted;
        }

        public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
        {
            _state = WebSocketState.Closed;
            _releaseFirstSend.TrySetResult(true);
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

        public override async Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _activeSendCount) != 1)
            {
                Interlocked.Decrement(ref _activeSendCount);
                Interlocked.Increment(ref _concurrentSendAttempts);
                throw new InvalidOperationException("Concurrent sends are not allowed");
            }

            try
            {
                SentMessages.Add(Encoding.UTF8.GetString(buffer.Array!, buffer.Offset, buffer.Count));
                if (Interlocked.Increment(ref _sendCount) == 1)
                {
                    _firstSendStarted.TrySetResult(true);
                    await _releaseFirstSend.Task.WaitAsync(cancellationToken);
                }
            }
            finally
            {
                Interlocked.Decrement(ref _activeSendCount);
            }
        }
    }
}
