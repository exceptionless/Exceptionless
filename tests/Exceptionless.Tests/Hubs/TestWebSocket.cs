using System.Net.WebSockets;
using System.Text;

namespace Exceptionless.Tests.Hubs;

internal sealed class TestWebSocket : WebSocket
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
