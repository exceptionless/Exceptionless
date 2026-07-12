using System.Collections.Concurrent;
using Exceptionless.Core;
using Foundatio.Serializer;

namespace Exceptionless.Web.Hubs;

/// <summary>
/// Manages active SSE connections. Replaces WebSocketConnectionManager.
/// Sends keep-alive comments every 15 seconds to prevent proxy/LB disconnects.
/// Proactively prunes dead connections during keep-alive sweeps.
/// </summary>
public sealed class SseConnectionManager : IDisposable, IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, SseConnection> _connections = new();
    private readonly ConcurrentDictionary<string, Lazy<Task>> _pendingDisposals = new();
    private readonly Timer? _timer;
    private readonly ITextSerializer _serializer;
    private readonly ILogger _logger;

    /// <summary>
    /// Maximum number of concurrent connections per user to prevent resource exhaustion.
    /// </summary>
    public int MaxConnectionsPerUser { get; init; } = 10;

    public SseConnectionManager(AppOptions options, ITextSerializer serializer, ILoggerFactory loggerFactory)
    {
        _serializer = serializer;
        _logger = loggerFactory.CreateLogger<SseConnectionManager>();

        if (!options.EnablePush)
            return;

        _timer = new Timer(SendKeepAlive, null, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(15));
    }

    private void SendKeepAlive(object? state)
    {
        if (_connections.IsEmpty)
            return;

        int sent = 0;
        int pruned = 0;

        foreach (var (connectionId, connection) in _connections)
        {
            if (connection.ConnectionAborted.IsCancellationRequested)
            {
                TryRemove(connectionId);
                pruned++;
                continue;
            }

            if (!connection.TryWriteKeepAlive())
            {
                // Write failed — connection is dead, prune it
                TryRemove(connectionId);
                pruned++;
            }
            else
            {
                sent++;
            }
        }

        if (_logger.IsEnabled(LogLevel.Trace))
            _logger.LogTrace("SSE keep-alive: sent={SentCount}, pruned={PrunedCount}, active={ActiveCount}", sent, pruned, _connections.Count);
    }

    public SseConnection? GetConnectionById(string connectionId)
    {
        return _connections.TryGetValue(connectionId, out var connection) ? connection : null;
    }

    public ICollection<SseConnection> GetAll()
    {
        return _connections.Values;
    }

    public int ConnectionCount => _connections.Count;

    public SseConnection AddConnection(string connectionId, HttpResponse response, CancellationToken requestAborted)
    {
        var connection = new SseConnection(connectionId, response, _serializer, requestAborted, _logger);
        if (!_connections.TryAdd(connectionId, connection))
        {
            connection.DisposeAsync().AsTask().GetAwaiter().GetResult();
            throw new InvalidOperationException($"An SSE connection with id '{connectionId}' is already registered.");
        }
        AppDiagnostics.PushSseConnectionsOpened.Add(1);
        AppDiagnostics.Gauge("push.connections.sse.active", _connections.Count);
        return connection;
    }

    public async Task RemoveConnectionAsync(string connectionId)
    {
        if (_connections.TryRemove(connectionId, out var connection))
        {
            await DisposeConnectionAsync(connectionId, connection).ConfigureAwait(false);
            return;
        }

        if (_pendingDisposals.TryGetValue(connectionId, out var pendingDisposal))
            await pendingDisposal.Value.ConfigureAwait(false);
    }

    private void TryRemove(string connectionId)
    {
        if (_connections.TryRemove(connectionId, out var connection))
            _ = ObserveDisposeAsync(connectionId, DisposeConnectionAsync(connectionId, connection));
    }

    public bool SendMessage(string connectionId, object message, bool canDrop = true)
    {
        if (!_connections.TryGetValue(connectionId, out var connection))
            return false;

        if (connection.ConnectionAborted.IsCancellationRequested)
        {
            TryRemove(connectionId);
            return false;
        }

        if (connection.TryWrite(message, canDrop))
            return true;

        TryRemove(connectionId);
        return false;
    }

    public void SendMessage(IEnumerable<string> connectionIds, object message, bool canDrop = true)
    {
        foreach (string connectionId in connectionIds)
            SendMessage(connectionId, message, canDrop);
    }

    public void SendMessageToAll(object message, bool canDrop = true)
    {
        foreach (var (connectionId, connection) in _connections)
        {
            if (connection.ConnectionAborted.IsCancellationRequested)
            {
                TryRemove(connectionId);
                continue;
            }

            if (!connection.TryWrite(message, canDrop))
                TryRemove(connectionId);
        }
    }

    public void Dispose()
    {
        // Synchronous disposal: used by test hosts and non-async disposal paths.
        // For production host shutdown, the DI container will prefer DisposeAsync().
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        _timer?.Dispose();

        var disposeTasks = new List<Task>();

        foreach (var (connectionId, connection) in _connections)
        {
            if (_connections.TryRemove(connectionId, out var activeConnection))
                disposeTasks.Add(DisposeConnectionAsync(connectionId, activeConnection));
        }

        foreach (var pendingDisposal in _pendingDisposals.Values)
            disposeTasks.Add(pendingDisposal.Value);

        if (disposeTasks.Count > 0)
            await Task.WhenAll(disposeTasks).ConfigureAwait(false);
    }

    private Task DisposeConnectionAsync(string connectionId, SseConnection connection)
    {
        var pendingDisposal = _pendingDisposals.GetOrAdd(connectionId, _ => new Lazy<Task>(() => DisposeConnectionCoreAsync(connectionId, connection)));
        return pendingDisposal.Value;
    }

    private async Task DisposeConnectionCoreAsync(string connectionId, SseConnection connection)
    {
        try
        {
            connection.Abort();
            await connection.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            _pendingDisposals.TryRemove(connectionId, out _);
            AppDiagnostics.PushSseConnectionsClosed.Add(1);
            AppDiagnostics.Gauge("push.connections.sse.active", _connections.Count);
        }
    }

    private async Task ObserveDisposeAsync(string connectionId, Task disposeTask)
    {
        try
        {
            await disposeTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
        catch (InvalidOperationException ex)
        {
            _logger.LogDebug(ex, "SSE connection cleanup failed for {ConnectionId}", connectionId);
        }
    }
}
