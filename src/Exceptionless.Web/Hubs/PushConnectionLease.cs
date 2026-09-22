using Exceptionless.Core.Utility;

namespace Exceptionless.Web.Hubs;

public sealed class PushConnectionLease : IAsyncDisposable
{
    internal static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(1);
    internal static readonly TimeSpan RenewalInterval = TimeSpan.FromSeconds(15);
    private readonly CancellationTokenSource _leaseLost = new();
    private readonly IConnectionLeaseStore _store;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _renewalTask;
    private readonly string _userId;
    private readonly string _connectionId;
    private int _disposeState;

    public CancellationToken LeaseLost => _leaseLost.Token;

    private PushConnectionLease(IConnectionLeaseStore store, TimeProvider timeProvider, ILogger logger, string userId, string connectionId)
    {
        _store = store;
        _timeProvider = timeProvider;
        _logger = logger;
        _userId = userId;
        _connectionId = connectionId;
        _renewalTask = RenewAsync();
    }

    public static async Task<PushConnectionLease?> TryAcquireAsync(IConnectionLeaseStore store, TimeProvider timeProvider, ILogger logger, string userId, string connectionId, int maxConnections)
    {
        return await store.TryAcquireAsync(userId, connectionId, maxConnections, LeaseDuration).ConfigureAwait(false)
            ? new PushConnectionLease(store, timeProvider, logger, userId, connectionId)
            : null;
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
            return;

        await _stop.CancelAsync().ConfigureAwait(false);
        await _renewalTask.ConfigureAwait(false);
        try
        {
            await _store.ReleaseAsync(_userId, _connectionId).ConfigureAwait(false);
        }
        catch (ConnectionLeaseStoreException ex)
        {
            // The lease expires without cleanup; a release failure must not mask request teardown.
            _logger.LogWarning(ex, "Unable to release push lease {ConnectionId}", _connectionId);
        }
        finally
        {
            _stop.Dispose();
            _leaseLost.Dispose();
        }
    }

    private async Task RenewAsync()
    {
        using var timer = new PeriodicTimer(RenewalInterval, _timeProvider);
        DateTimeOffset lastSuccessfulRenewal = _timeProvider.GetUtcNow();

        try
        {
            while (await timer.WaitForNextTickAsync(_stop.Token).ConfigureAwait(false))
            {
                try
                {
                    if (await _store.RenewAsync(_userId, _connectionId, LeaseDuration).ConfigureAwait(false))
                    {
                        lastSuccessfulRenewal = _timeProvider.GetUtcNow();
                        continue;
                    }

                    _logger.LogWarning("Push lease was lost for {ConnectionId}", _connectionId);
                    await _leaseLost.CancelAsync().ConfigureAwait(false);
                    return;
                }
                catch (ConnectionLeaseStoreException ex)
                {
                    _logger.LogWarning(ex, "Unable to renew push lease {ConnectionId}", _connectionId);
                    if (_timeProvider.GetUtcNow() - lastSuccessfulRenewal >= LeaseDuration)
                    {
                        await _leaseLost.CancelAsync().ConfigureAwait(false);
                        return;
                    }
                }
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested)
        {
        }
    }
}
