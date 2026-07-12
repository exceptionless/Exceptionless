using Foundatio.Serializer;

namespace Exceptionless.Web.Hubs;

/// <summary>
/// Represents a single SSE client connection. Owns a write loop that serializes
/// all sends through a bounded dedup queue, preventing concurrent writes to the
/// underlying HttpResponse stream.
///
/// Design: delivery is best-effort. If a slow client fills the bounded queue, the
/// connection is aborted so reconnect handling performs a full cache resynchronization.
///
/// Deduplication: messages with the same serialized payload are coalesced — if an
/// identical message is already queued, the newer duplicate is skipped. This reduces
/// redundant client refreshes during burst scenarios (e.g., rapid entity updates).
/// </summary>
public sealed class SseConnection : IAsyncDisposable
{
    private static readonly byte[] KeepAliveBytes = ": keepalive\n\n"u8.ToArray();
    private readonly HttpResponse _response;
    private readonly ITextSerializer _serializer;
    private readonly DedupQueue _queue;
    private readonly CancellationTokenSource _cts;
    private readonly CancellationToken _connectionAborted;
    private readonly Task _writeLoop;
    private readonly ILogger _logger;
    private long _droppedMessages;
    private long _dedupedMessages;
    private int _disposeState;

    public string ConnectionId { get; }
    public CancellationToken ConnectionAborted => _connectionAborted;

    /// <summary>Number of messages dropped due to backpressure (queue full).</summary>
    public long DroppedMessages => Interlocked.Read(ref _droppedMessages);

    /// <summary>Number of messages skipped due to deduplication.</summary>
    public long DedupedMessages => Interlocked.Read(ref _dedupedMessages);

    public SseConnection(string connectionId, HttpResponse response, ITextSerializer serializer, CancellationToken requestAborted, ILogger logger, int capacity = 64)
    {
        ConnectionId = connectionId;
        _response = response;
        _serializer = serializer;
        _logger = logger;
        _queue = new DedupQueue(capacity);

        _cts = CancellationTokenSource.CreateLinkedTokenSource(requestAborted);
        _connectionAborted = _cts.Token;
        _writeLoop = Task.Run(() => WriteLoopAsync(_cts.Token));
    }

    /// <summary>
    /// Enqueue a message to be written. Returns false if the connection is closed.
    /// If an identical message (same serialized payload) is already queued, the new
    /// one is skipped (deduped) and this returns true.
    /// </summary>
    public bool TryWrite(object message, bool canDrop = true)
    {
        if (_cts.IsCancellationRequested)
            return false;

        string data = _serializer.SerializeToString(message);
        var result = _queue.TryEnqueue(new SseEvent { Data = data, DedupeKey = canDrop ? data : null });

        if (result == EnqueueResult.Deduped)
        {
            Interlocked.Increment(ref _dedupedMessages);
            return true;
        }

        if (result is EnqueueResult.Full)
        {
            Interlocked.Increment(ref _droppedMessages);
            Abort();
            return false;
        }

        if (result is EnqueueResult.Closed)
            return false;

        return true;
    }

    /// <summary>
    /// Send a keep-alive comment to prevent proxy/LB timeouts.
    /// Keep-alives bypass dedup and are skipped when the data queue is full.
    /// </summary>
    public bool TryWriteKeepAlive()
    {
        if (_cts.IsCancellationRequested)
            return false;

        return _queue.TryEnqueue(SseEvent.KeepAlive) is not EnqueueResult.Closed;
    }

    /// <summary>
    /// Abort the connection. The write loop will complete and the middleware will clean up.
    /// </summary>
    public void Abort()
    {
        try { _cts.Cancel(); }
        catch (ObjectDisposedException ex)
        {
            _logger.LogDebug(ex, "SSE cancellation token source was already disposed for {ConnectionId}", ConnectionId);
        }

        _queue.Complete();
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
            return;
        Abort();
        using (_queue)
        using (_cts)
        {
            try
            {
                await _writeLoop.ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
        }
    }

    private async Task WriteLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var evt = await _queue.DequeueAsync(ct);
                if (evt is null)
                    break; // Queue completed

                var bytes = evt.Value.IsKeepAlive
                    ? KeepAliveBytes
                    : System.Text.Encoding.UTF8.GetBytes($"data: {evt.Value.Data}\n\n");

                await _response.Body.WriteAsync(bytes, ct);
                await _response.Body.FlushAsync(ct);
            }
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
        catch (IOException) { }
        finally
        {
            // Always signal ConnectionAborted so the middleware's Task.Delay unblocks
            // and process-local registration and lease cleanup happens reliably.
            _queue.Complete();
            if (!_cts.IsCancellationRequested)
            {
                try
                {
                    _cts.Cancel();
                }
                catch (ObjectDisposedException ex)
                {
                    _logger.LogDebug(ex, "SSE cancellation token source was already disposed for {ConnectionId}", ConnectionId);
                }
            }
        }
    }

    internal readonly record struct SseEvent
    {
        public string? Data { get; init; }

        /// <summary>
        /// Key used for deduplication. If null, no dedup is applied (e.g., keep-alive).
        /// For data messages, this is the serialized payload — identical payloads trigger
        /// the same client-side cache invalidation, so coalescing is safe.
        /// </summary>
        public string? DedupeKey { get; init; }
        public bool IsKeepAlive { get; init; }
        public static SseEvent KeepAlive => new() { IsKeepAlive = true };
    }

    internal enum EnqueueResult
    {
        Enqueued,
        Deduped,
        Full,
        Closed
    }

    /// <summary>
    /// Bounded FIFO queue with deduplication. Thread-safe for multiple writers and a single reader.
    /// When full, rejects new items so the owner can abort and force a reconnect resynchronization.
    /// If an item with the same DedupeKey is already queued, the new item is skipped.
    /// </summary>
    internal sealed class DedupQueue : IDisposable
    {
        private readonly object _lock = new();
        private readonly LinkedList<SseEvent> _list = new();
        private readonly Dictionary<string, LinkedListNode<SseEvent>> _index = new();
        private readonly SemaphoreSlim _signal = new(0);
        private readonly int _capacity;
        private bool _completed;

        public DedupQueue(int capacity)
        {
            _capacity = capacity;
        }

        public EnqueueResult TryEnqueue(SseEvent evt)
        {
            lock (_lock)
            {
                if (_completed)
                    return EnqueueResult.Closed;

                // Dedup check: if same key is already queued, skip
                if (evt.DedupeKey is not null && _index.ContainsKey(evt.DedupeKey))
                    return EnqueueResult.Deduped;

                if (_list.Count >= _capacity)
                    return EnqueueResult.Full;

                var node = _list.AddLast(evt);
                if (evt.DedupeKey is not null)
                    _index[evt.DedupeKey] = node;

                _signal.Release();
                return EnqueueResult.Enqueued;
            }
        }

        public async Task<SseEvent?> DequeueAsync(CancellationToken ct)
        {
            await _signal.WaitAsync(ct);

            lock (_lock)
            {
                if (_list.Count == 0)
                    return null; // Completed

                var node = _list.First!;
                RemoveNode(node);
                return node.Value;
            }
        }

        public void Complete()
        {
            lock (_lock)
            {
                if (_completed)
                    return;
                _completed = true;
                _signal.Release(); // Wake up the reader so it sees null
            }
        }

        public void Dispose()
        {
            _signal.Dispose();
        }

        private void RemoveNode(LinkedListNode<SseEvent> node)
        {
            _list.Remove(node);
            if (node.Value.DedupeKey is not null)
                _index.Remove(node.Value.DedupeKey);
        }
    }
}
