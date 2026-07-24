namespace Exceptionless.Web.Hubs;

/// <summary>
/// Process-local ownership index. Redis pub/sub delivers every push message to every API
/// replica, so each replica only routes to and cleans up connections it actually owns.
/// </summary>
public sealed class PushConnectionRegistry(TimeProvider timeProvider)
{
    private static readonly TimeSpan RevocationTombstoneDuration = TimeSpan.FromMinutes(5);
    private readonly Dictionary<string, Registration> _connections = [];
    private readonly Dictionary<string, HashSet<string>> _groupConnections = [];
    private readonly Dictionary<string, DateTimeOffset> _revokedTokens = [];
    private readonly Dictionary<string, HashSet<string>> _tokenConnections = [];
    private readonly Dictionary<string, HashSet<string>> _userConnections = [];
    private readonly object _lock = new();

    public bool TryRegister(string connectionId, string userId, string? tokenId, IEnumerable<string> organizationIds)
    {
        lock (_lock)
        {
            RemoveExpiredRevocations();
            if (tokenId is not null && _revokedTokens.ContainsKey(tokenId))
                return false;

            var registration = new Registration(userId, tokenId, organizationIds);
            _connections.Add(connectionId, registration);
            AddToIndex(_userConnections, userId, connectionId);
            if (tokenId is not null)
                AddToIndex(_tokenConnections, tokenId, connectionId);
            foreach (string organizationId in registration.OrganizationIds)
                AddToIndex(_groupConnections, organizationId, connectionId);

            return true;
        }
    }

    public IReadOnlyCollection<string> RevokeToken(string tokenId)
    {
        lock (_lock)
        {
            RemoveExpiredRevocations();
            _revokedTokens[tokenId] = timeProvider.GetUtcNow() + RevocationTombstoneDuration;
            return GetIndexedConnections(_tokenConnections, tokenId);
        }
    }

    public IReadOnlyCollection<string> GetUserConnections(string userId)
    {
        lock (_lock)
            return GetIndexedConnections(_userConnections, userId);
    }

    public IReadOnlyCollection<string> GetGroupConnections(string organizationId)
    {
        lock (_lock)
            return GetIndexedConnections(_groupConnections, organizationId);
    }

    public IReadOnlyCollection<string> GetGroups(string connectionId)
    {
        lock (_lock)
            return _connections.TryGetValue(connectionId, out var registration) ? registration.OrganizationIds.ToArray() : [];
    }

    public void AddGroup(string connectionId, string organizationId)
    {
        lock (_lock)
        {
            if (_connections.TryGetValue(connectionId, out var registration) && registration.OrganizationIds.Add(organizationId))
                AddToIndex(_groupConnections, organizationId, connectionId);
        }
    }

    public void RemoveGroup(string connectionId, string organizationId)
    {
        lock (_lock)
        {
            if (_connections.TryGetValue(connectionId, out var registration) && registration.OrganizationIds.Remove(organizationId))
                RemoveFromIndex(_groupConnections, organizationId, connectionId);
        }
    }

    public void Unregister(string connectionId)
    {
        lock (_lock)
        {
            if (!_connections.Remove(connectionId, out var registration))
                return;

            RemoveFromIndex(_userConnections, registration.UserId, connectionId);
            if (registration.TokenId is not null)
                RemoveFromIndex(_tokenConnections, registration.TokenId, connectionId);
            foreach (string organizationId in registration.OrganizationIds)
                RemoveFromIndex(_groupConnections, organizationId, connectionId);
        }
    }

    private static void AddToIndex(Dictionary<string, HashSet<string>> index, string key, string connectionId)
    {
        if (!index.TryGetValue(key, out var connections))
        {
            connections = [];
            index[key] = connections;
        }

        connections.Add(connectionId);
    }

    private static string[] GetIndexedConnections(Dictionary<string, HashSet<string>> index, string key)
    {
        return index.TryGetValue(key, out var connections) ? connections.ToArray() : [];
    }

    private static void RemoveFromIndex(Dictionary<string, HashSet<string>> index, string key, string connectionId)
    {
        if (!index.TryGetValue(key, out var connections))
            return;

        connections.Remove(connectionId);
        if (connections.Count is 0)
            index.Remove(key);
    }

    private void RemoveExpiredRevocations()
    {
        var now = timeProvider.GetUtcNow();
        foreach (string tokenId in _revokedTokens.Where(pair => pair.Value <= now).Select(pair => pair.Key).ToArray())
            _revokedTokens.Remove(tokenId);
    }

    private sealed record Registration(string UserId, string? TokenId, IEnumerable<string> InitialOrganizationIds)
    {
        public HashSet<string> OrganizationIds { get; } = [.. InitialOrganizationIds];
    }
}
