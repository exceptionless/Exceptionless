using System.Net.WebSockets;
using System.Security.Claims;
using System.Diagnostics.Metrics;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Utility;
using Exceptionless.Web.Hubs;
using Foundatio.Serializer;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Exceptionless.Tests.Hubs;

public sealed class SseLifecycleTests : TestWithServices
{
    public SseLifecycleTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public async Task Invoke_MessageDuringRegistration_WritesOnlyAfterSseHeadersAreSet()
    {
        using var manager = CreateManager();
        using var requestAborted = new CancellationTokenSource();
        var context = new DefaultHttpContext
        {
            RequestAborted = requestAborted.Token,
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "user")], IdentityUtils.UserAuthenticationType))
        };
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/api/v2/push";
        using var body = new HeaderCapturingStream(context.Response);
        context.Response.Body = body;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Name == "ex.push.connections.sse.opened")
                meterListener.EnableMeasurementEvents(instrument);
        };
        int sent = 0;
        listener.SetMeasurementEventCallback<int>((_, _, _, _) =>
        {
            if (manager.ConnectionCount == 0 || Interlocked.Exchange(ref sent, 1) != 0)
                return;

            manager.SendMessageToAll(new { type = "during-registration" });
            // Keep registration paused so an eager writer exposes the header race.
            SpinWait.SpinUntil(() => body.FirstHeaders.Task.IsCompleted, TimeSpan.FromMilliseconds(100));
        });
        listener.Start();
        var middleware = new SseMiddleware(_ => Task.CompletedTask, manager, new RecordingLeaseStore(),
            new PushConnectionRegistry(TimeProvider), TimeProvider, new TestHostApplicationLifetime(), GetService<ILogger<SseMiddleware>>());

        Task invokeTask = middleware.Invoke(context);
        try
        {
            var headers = await body.FirstHeaders.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.Equal(("text/event-stream", "no"), headers);
            Assert.Equal(1, sent);
        }
        finally
        {
            await requestAborted.CancelAsync();
            await invokeTask.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task Invoke_ReleasesLeaseBeforeBlockedResponseCleanup()
    {
        using var requestAborted = new CancellationTokenSource();
        using var responseBody = new BlockingResponseStream();
        var leaseStore = new RecordingLeaseStore();
        using var manager = CreateManager();
        var registry = new PushConnectionRegistry(TimeProvider);
        var context = new DefaultHttpContext
        {
            RequestAborted = requestAborted.Token,
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "lease-user")],
            IdentityUtils.UserAuthenticationType))
        };
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/api/v2/push";
        context.Response.Body = responseBody;

        var middleware = new SseMiddleware(
            _ => Task.CompletedTask,
            manager,
            leaseStore,
            registry,
            TimeProvider,
            new TestHostApplicationLifetime(),
            GetService<ILogger<SseMiddleware>>());

        Task invokeTask = middleware.Invoke(context);
        await responseBody.WriteStarted.Task.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        await requestAborted.CancelAsync();
        try
        {
            await leaseStore.ReleaseCalled.Task.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        }
        finally
        {
            responseBody.Release();
            await invokeTask.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        }

        Assert.True(leaseStore.ReleaseCalled.Task.IsCompletedSuccessfully);
    }

    private SseConnectionManager CreateManager()
    {
        return new SseConnectionManager(new AppOptions { EnablePush = false }, GetService<ITextSerializer>(), Log);
    }

    private sealed class HeaderCapturingStream(HttpResponse response) : MemoryStream
    {
        public TaskCompletionSource<(string ContentType, string Buffering)> FirstHeaders { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            FirstHeaders.TrySetResult((response.Headers.ContentType.ToString(), response.Headers["X-Accel-Buffering"].ToString()));
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingLeaseStore : IConnectionLeaseStore
    {
        public TaskCompletionSource ReleaseCalled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<bool> TryAcquireAsync(string userId, string connectionId, int maxConnections, TimeSpan leaseDuration)
        {
            return Task.FromResult(true);
        }

        public Task<bool> RenewAsync(string userId, string connectionId, TimeSpan leaseDuration)
        {
            return Task.FromResult(true);
        }

        public Task ReleaseAsync(string userId, string connectionId)
        {
            ReleaseCalled.TrySetResult();
            return Task.CompletedTask;
        }
    }

    private sealed class BlockingResponseStream : Stream
    {
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource WriteStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => 0;
        public override long Position { get; set; }

        public void Release() => _release.TrySetResult();

        public override void Flush() { }
        public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            WriteStarted.TrySetResult();
            await _release.Task.ConfigureAwait(false);
        }
    }

    private sealed class TestHostApplicationLifetime : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => CancellationToken.None;
        public CancellationToken ApplicationStopped => CancellationToken.None;
        public void StopApplication() { }
    }
}

public sealed class WebSocketLifecycleTests : TestWithServices
{
    public WebSocketLifecycleTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public async Task RemoveConnectionAsync_UnresponsiveClose_AbortsWithinDeadline()
    {
        using var manager = CreateManager();
        using var socket = new TestWebSocket(blockClose: true);
        string connectionId = manager.AddConnection(socket);

        await manager.RemoveConnectionAsync(connectionId).WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Equal(WebSocketState.Aborted, socket.State);
        Assert.Null(manager.GetConnectionById(connectionId));
    }

    [Fact]
    public async Task RemoveConnectionAsync_CancelsBlockedSendAndCompletes()
    {
        using var manager = CreateManager();
        var socket = new TestWebSocket(blockSend: true);
        string connectionId = manager.AddConnection(socket);

        Assert.True(manager.SendMessage(connectionId, new { type = "blocked" }));
        await socket.WaitUntilSendingAsync().WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        Task removeTask = manager.RemoveConnectionAsync(connectionId);
        bool completed = false;
        try
        {
            await removeTask.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
            completed = true;
        }
        finally
        {
            socket.ReleaseSend();
            await removeTask;
        }

        Assert.True(completed, "Removing a connection must not wait indefinitely for an outstanding send.");
        Assert.Null(manager.GetConnectionById(connectionId));
        Assert.Equal(1, socket.CloseCount);
    }

    private WebSocketConnectionManager CreateManager()
    {
        return new WebSocketConnectionManager(new AppOptions { EnablePush = false }, GetService<ITextSerializer>(), Log);
    }
}
