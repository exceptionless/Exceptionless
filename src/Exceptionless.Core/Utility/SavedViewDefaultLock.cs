using Foundatio.Lock;

namespace Exceptionless.Core.Utility;

public static class SavedViewDefaultLock
{
    public static readonly TimeSpan Duration = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan RenewalInterval = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    public static string GetOrganizationKey(string organizationId) => $"saved-view-defaults:{organizationId}";

    public static string GetUserKey(string userId) => $"saved-view-defaults:user:{userId}";

    public static SavedViewDefaultLockRenewal Renew(ILock @lock) => new(@lock);
}

public sealed class SavedViewDefaultLockRenewal : IAsyncDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly Task _renewalTask;

    internal SavedViewDefaultLockRenewal(ILock @lock)
    {
        _renewalTask = RenewAsync(@lock, _cancellationTokenSource.Token);
    }

    public async Task ThrowIfFailedAsync()
    {
        if (_renewalTask.IsCompleted)
            await _renewalTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _cancellationTokenSource.CancelAsync();
        try
        {
            await _renewalTask;
        }
        catch (OperationCanceledException) when (_cancellationTokenSource.IsCancellationRequested)
        {
        }
        finally
        {
            _cancellationTokenSource.Dispose();
        }
    }

    private static async Task RenewAsync(ILock @lock, CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(SavedViewDefaultLock.RenewalInterval);
        while (await timer.WaitForNextTickAsync(cancellationToken))
            await @lock.RenewAsync(SavedViewDefaultLock.Duration);
    }
}
