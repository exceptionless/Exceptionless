using System.Collections.Concurrent;

namespace Exceptionless.Core.Utility;

public interface IConnectionMapping
{
    Task AddAsync(string key, string connectionId);
    Task<ICollection<string>> GetConnectionsAsync(string key);
    Task<int> GetConnectionCountAsync(string key);
    Task RemoveAsync(string key, string connectionId);
}

public class ConnectionMapping : IConnectionMapping
{
    private readonly ConcurrentDictionary<string, ConnectionSet> _connections = new();

    internal int TrackedKeyCount => _connections.Count;

    public Task AddAsync(string key, string connectionId)
    {
        if (key is null)
            return Task.CompletedTask;

        while (true)
        {
            var connections = _connections.GetOrAdd(key, _ => new ConnectionSet());

            lock (connections.SyncRoot)
            {
                if (connections.IsDetachedFromMap)
                    continue;

                connections.ConnectionIds.Add(connectionId);
                return Task.CompletedTask;
            }
        }
    }

    public Task<ICollection<string>> GetConnectionsAsync(string key)
    {
        if (key is null)
            return Task.FromResult<ICollection<string>>([]);

        if (!_connections.TryGetValue(key, out var connections))
            return Task.FromResult<ICollection<string>>([]);

        lock (connections.SyncRoot)
        {
            if (connections.IsDetachedFromMap)
                return Task.FromResult<ICollection<string>>([]);

            return Task.FromResult<ICollection<string>>([.. connections.ConnectionIds]);
        }
    }

    public Task<int> GetConnectionCountAsync(string key)
    {
        if (key is null)
            return Task.FromResult(0);

        if (!_connections.TryGetValue(key, out var connections))
            return Task.FromResult(0);

        lock (connections.SyncRoot)
        {
            return Task.FromResult(connections.IsDetachedFromMap ? 0 : connections.ConnectionIds.Count);
        }
    }

    public Task RemoveAsync(string key, string connectionId)
    {
        if (key is null)
            return Task.CompletedTask;

        if (!_connections.TryGetValue(key, out var connections))
            return Task.CompletedTask;

        lock (connections.SyncRoot)
        {
            if (connections.IsDetachedFromMap)
                return Task.CompletedTask;

            if (!connections.ConnectionIds.Remove(connectionId))
                return Task.CompletedTask;

            if (connections.ConnectionIds.Count is not 0)
                return Task.CompletedTask;

            connections.IsDetachedFromMap = true;
            _connections.TryRemove(key, out _);
            return Task.CompletedTask;
        }
    }

    private sealed class ConnectionSet
    {
        public object SyncRoot { get; } = new();
        public HashSet<string> ConnectionIds { get; } = [];
        public bool IsDetachedFromMap { get; set; }
    }
}

public static class ConnectionMappingExtensions
{
    public const string UserIdPrefix = "u-";
    public const string GroupPrefix = "g-";
    public const string ConnectionGroupPrefix = "cg-";

    public static Task GroupAddAsync(this IConnectionMapping map, string group, string connectionId)
    {
        return map.AddAsync(GroupPrefix + group, connectionId);
    }

    public static Task GroupRemoveAsync(this IConnectionMapping map, string group, string connectionId)
    {
        return map.RemoveAsync(GroupPrefix + group, connectionId);
    }

    public static Task<ICollection<string>> GetGroupConnectionsAsync(this IConnectionMapping map, string group)
    {
        return map.GetConnectionsAsync(GroupPrefix + group);
    }

    public static Task<int> GetGroupConnectionCountAsync(this IConnectionMapping map, string group)
    {
        return map.GetConnectionCountAsync(GroupPrefix + group);
    }

    public static Task ConnectionGroupAddAsync(this IConnectionMapping map, string connectionId, string group)
    {
        return map.AddAsync(ConnectionGroupPrefix + connectionId, group);
    }

    public static Task ConnectionGroupRemoveAsync(this IConnectionMapping map, string connectionId, string group)
    {
        return map.RemoveAsync(ConnectionGroupPrefix + connectionId, group);
    }

    public static Task<ICollection<string>> GetConnectionGroupsAsync(this IConnectionMapping map, string connectionId)
    {
        return map.GetConnectionsAsync(ConnectionGroupPrefix + connectionId);
    }

    public static Task UserIdAddAsync(this IConnectionMapping map, string userId, string connectionId)
    {
        return map.AddAsync(UserIdPrefix + userId, connectionId);
    }

    public static Task UserIdRemoveAsync(this IConnectionMapping map, string userId, string connectionId)
    {
        return map.RemoveAsync(UserIdPrefix + userId, connectionId);
    }

    public static Task<ICollection<string>> GetUserIdConnectionsAsync(this IConnectionMapping map, string userId)
    {
        return map.GetConnectionsAsync(UserIdPrefix + userId);
    }

    public static Task<int> GetUserIdConnectionCountAsync(this IConnectionMapping map, string userId)
    {
        return map.GetConnectionCountAsync(UserIdPrefix + userId);
    }

    /// <summary>
    /// Reserves a connection slot before accepting a long-lived push connection.
    /// Adding before counting prevents concurrent requests from bypassing the per-user limit.
    /// </summary>
    public static async Task<bool> TryReserveUserConnectionAsync(this IConnectionMapping map, string userId, string connectionId, int maxConnections)
    {
        if (maxConnections <= 0)
            return false;

        await map.UserIdAddAsync(userId, connectionId);
        try
        {
            if (await map.GetUserIdConnectionCountAsync(userId) <= maxConnections)
                return true;
        }
        catch
        {
            await map.UserIdRemoveAsync(userId, connectionId);
            throw;
        }

        await map.UserIdRemoveAsync(userId, connectionId);
        return false;
    }
}
