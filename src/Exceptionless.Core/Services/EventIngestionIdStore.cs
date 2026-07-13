using System.Security.Cryptography;
using System.Text;
using Foundatio.Caching;

namespace Exceptionless.Core.Services;

public interface IEventIngestionIdStore
{
    Task<IReadOnlyDictionary<string, EventIngestionId>> GetOrAddAsync(
        string projectId,
        IReadOnlyCollection<EventIngestionIdCandidate> candidates,
        TimeSpan expiresIn,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, EventIngestionId>> GetAsync(
        string projectId,
        IReadOnlyCollection<string> clientIds,
        CancellationToken cancellationToken = default);
}

public readonly record struct EventIngestionId(
    string EventId,
    DateTimeOffset EventDate,
    DateTime CreatedUtc);

public readonly record struct EventIngestionIdCandidate(
    string ClientId,
    EventIngestionId Identity);

/// <summary>
/// Claims date-routable event identifiers in the distributed cache. Each client identifier gets
/// an independently expiring key so storage stays bounded by the configured idempotency window.
/// </summary>
public sealed class EventIngestionIdStore(ICacheClient cache) : IEventIngestionIdStore
{
    public async Task<IReadOnlyDictionary<string, EventIngestionId>> GetOrAddAsync(
        string projectId,
        IReadOnlyCollection<EventIngestionIdCandidate> candidates,
        TimeSpan expiresIn,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);
        if (expiresIn <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(expiresIn));
        if (candidates.Count == 0)
            return new Dictionary<string, EventIngestionId>(StringComparer.Ordinal);

        cancellationToken.ThrowIfCancellationRequested();
        var distinctCandidates = new Dictionary<string, EventIngestionIdCandidate>(StringComparer.Ordinal);
        foreach (EventIngestionIdCandidate candidate in candidates)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(candidate.ClientId);
            distinctCandidates.TryAdd(candidate.ClientId, candidate);
        }

        var keysByClientId = distinctCandidates.Keys.ToDictionary(
            clientId => clientId,
            clientId => GetCacheKey(projectId, clientId),
            StringComparer.Ordinal);
        var cached = await cache.GetAllAsync<EventIngestionId>(keysByClientId.Values);
        cancellationToken.ThrowIfCancellationRequested();

        var results = new Dictionary<string, EventIngestionId>(distinctCandidates.Count, StringComparer.Ordinal);
        var missing = new List<EventIngestionIdCandidate>();
        foreach ((string clientId, EventIngestionIdCandidate candidate) in distinctCandidates)
        {
            if (cached.TryGetValue(keysByClientId[clientId], out CacheValue<EventIngestionId>? value) && value is { HasValue: true })
                results.Add(clientId, value.Value);
            else
                missing.Add(candidate);
        }

        if (missing.Count == 0)
            return results;

        // Start all conditional writes before awaiting so Redis can pipeline a whole microbatch.
        Task<bool>[] claims = missing
            .Select(candidate => cache.AddAsync(
                keysByClientId[candidate.ClientId],
                candidate.Identity,
                expiresIn))
            .ToArray();
        bool[] claimed = await Task.WhenAll(claims);
        cancellationToken.ThrowIfCancellationRequested();

        var lostClientIds = new List<string>();
        for (int i = 0; i < missing.Count; i++)
        {
            if (claimed[i])
                results.Add(missing[i].ClientId, missing[i].Identity);
            else
                lostClientIds.Add(missing[i].ClientId);
        }

        if (lostClientIds.Count == 0)
            return results;

        var winners = await cache.GetAllAsync<EventIngestionId>(lostClientIds.Select(clientId => keysByClientId[clientId]));
        cancellationToken.ThrowIfCancellationRequested();
        foreach (string clientId in lostClientIds)
        {
            string key = keysByClientId[clientId];
            if (!winners.TryGetValue(key, out CacheValue<EventIngestionId>? winner) || winner is not { HasValue: true })
                throw new InvalidOperationException("The event ingestion identity claim could not be resolved.");

            results.Add(clientId, winner.Value);
        }

        return results;
    }

    public async Task<IReadOnlyDictionary<string, EventIngestionId>> GetAsync(
        string projectId,
        IReadOnlyCollection<string> clientIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);
        if (clientIds.Count == 0)
            return new Dictionary<string, EventIngestionId>(StringComparer.Ordinal);

        cancellationToken.ThrowIfCancellationRequested();
        string[] distinctClientIds = clientIds.Distinct(StringComparer.Ordinal).ToArray();
        var keysByClientId = distinctClientIds.ToDictionary(
            clientId => clientId,
            clientId => GetCacheKey(projectId, clientId),
            StringComparer.Ordinal);
        var cached = await cache.GetAllAsync<EventIngestionId>(keysByClientId.Values);
        cancellationToken.ThrowIfCancellationRequested();

        var results = new Dictionary<string, EventIngestionId>(cached.Count, StringComparer.Ordinal);
        foreach ((string clientId, string key) in keysByClientId)
        {
            if (cached.TryGetValue(key, out CacheValue<EventIngestionId>? value) && value is { HasValue: true })
                results.Add(clientId, value.Value);
        }

        return results;
    }

    internal static string GetCacheKey(string projectId, string clientId)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(clientId));
        return String.Concat("ingest-v3:id:v2:{", projectId, "}:", Convert.ToHexStringLower(hash));
    }
}
