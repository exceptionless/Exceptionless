using System.Net.WebSockets;
using System.Text;

namespace Exceptionless.Tests.Hubs;

internal sealed class TestWebSocket : WebSocket
{
    private readonly bool _blockReceive;
    private readonly bool _blockSend;
    private readonly bool _blockClose;
    private readonly TaskCompletionSource _receiving = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _sending = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _releaseSend = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private WebSocketState _state;
    private int _closeCount;
    private int _closeOutputCount;

    public TestWebSocket(WebSocketState state = WebSocketState.Open, bool blockReceive = false, bool blockSend = false, bool blockClose = false)
    {
        _state = state;
        _blockReceive = blockReceive;
        _blockSend = blockSend;
        _blockClose = blockClose;
    }

    public int CloseCount => _closeCount;
    public int CloseOutputCount => _closeOutputCount;
    public WebSocketCloseStatus? RequestedCloseStatus { get; private set; }
    public string? RequestedCloseStatusDescription { get; private set; }
    public List<string> SentMessages { get; } = [];
    public override WebSocketCloseStatus? CloseStatus { get; } = WebSocketCloseStatus.NormalClosure;
    public override string? CloseStatusDescription { get; } = "Closed";
    public override string? SubProtocol { get; } = null;
    public override WebSocketState State => _state;

    public override void Abort()
    {
        _state = WebSocketState.Aborted;
    }

    public override async Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _closeCount);
        RequestedCloseStatus = closeStatus;
        RequestedCloseStatusDescription = statusDescription;
        if (_blockClose)
            await Task.Delay(Timeout.Infinite, cancellationToken);
        _state = WebSocketState.Closed;
    }

    public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _closeOutputCount);
        RequestedCloseStatus = closeStatus;
        RequestedCloseStatusDescription = statusDescription;
        _state = WebSocketState.CloseSent;
        return Task.CompletedTask;
    }

    public override void Dispose() { }

    public override async Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
    {
        _receiving.TrySetResult();
        if (_blockReceive)
            await Task.Delay(Timeout.Infinite, cancellationToken);

        return new WebSocketReceiveResult(0, WebSocketMessageType.Text, true);
    }

    public override async Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
    {
        _sending.TrySetResult();
        if (_blockSend)
            await _releaseSend.Task.WaitAsync(cancellationToken);

        SentMessages.Add(Encoding.UTF8.GetString(buffer.Array!, buffer.Offset, buffer.Count));
    }

    public Task WaitUntilReceivingAsync() => _receiving.Task;
    public Task WaitUntilSendingAsync() => _sending.Task;
    public void ReleaseSend() => _releaseSend.TrySetResult();
}
