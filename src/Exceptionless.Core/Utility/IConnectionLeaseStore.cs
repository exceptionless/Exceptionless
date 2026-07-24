namespace Exceptionless.Core.Utility;

public interface IConnectionLeaseStore
{
    Task<bool> TryAcquireAsync(string userId, string connectionId, int maxConnections, TimeSpan leaseDuration);
    Task<bool> RenewAsync(string userId, string connectionId, TimeSpan leaseDuration);
    Task ReleaseAsync(string userId, string connectionId);
}

public sealed class ConnectionLeaseStore(TimeProvider timeProvider) : IConnectionLeaseStore
{
    private readonly Dictionary<string, Dictionary<string, DateTimeOffset>> _leases = [];
    private readonly object _lock = new();

    public Task<bool> TryAcquireAsync(string userId, string connectionId, int maxConnections, TimeSpan leaseDuration)
    {
        if (maxConnections <= 0)
            return Task.FromResult(false);

        lock (_lock)
        {
            var now = timeProvider.GetUtcNow();
            if (!_leases.TryGetValue(userId, out var userLeases))
            {
                userLeases = [];
                _leases[userId] = userLeases;
            }

            RemoveExpired(userLeases, now);
            if (!userLeases.ContainsKey(connectionId) && userLeases.Count >= maxConnections)
                return Task.FromResult(false);

            userLeases[connectionId] = now + leaseDuration;
            return Task.FromResult(true);
        }
    }

    public Task<bool> RenewAsync(string userId, string connectionId, TimeSpan leaseDuration)
    {
        lock (_lock)
        {
            var now = timeProvider.GetUtcNow();
            if (!_leases.TryGetValue(userId, out var userLeases))
                return Task.FromResult(false);

            RemoveExpired(userLeases, now);
            if (!userLeases.ContainsKey(connectionId))
                return Task.FromResult(false);

            userLeases[connectionId] = now + leaseDuration;
            return Task.FromResult(true);
        }
    }

    public Task ReleaseAsync(string userId, string connectionId)
    {
        lock (_lock)
        {
            if (_leases.TryGetValue(userId, out var userLeases))
            {
                userLeases.Remove(connectionId);
                if (userLeases.Count is 0)
                    _leases.Remove(userId);
            }
        }

        return Task.CompletedTask;
    }

    private static void RemoveExpired(Dictionary<string, DateTimeOffset> leases, DateTimeOffset now)
    {
        foreach (string connectionId in leases.Where(pair => pair.Value <= now).Select(pair => pair.Key).ToArray())
            leases.Remove(connectionId);
    }
}

public sealed class ConnectionLeaseStoreException(string message, Exception innerException) : Exception(message, innerException);
