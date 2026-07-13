using System.Collections.Concurrent;

namespace Exceptionless.Core.Services;

public interface IIngestionQuotaStore
{
    Task<int> ReserveAsync(
        string organizationId,
        string reservationId,
        int requestedCount,
        int availableCount,
        TimeSpan expiresIn,
        CancellationToken cancellationToken = default);

    Task ReleaseAsync(
        string organizationId,
        string reservationId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Process-local quota reservations for development and tests. Distributed deployments replace
/// this registration with the Redis implementation from Exceptionless.Insulation.
/// </summary>
public sealed class InMemoryIngestionQuotaStore(TimeProvider timeProvider) : IIngestionQuotaStore
{
    private readonly ConcurrentDictionary<string, ReservationState> _organizations = new(StringComparer.Ordinal);

    public Task<int> ReserveAsync(
        string organizationId,
        string reservationId,
        int requestedCount,
        int availableCount,
        TimeSpan expiresIn,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(organizationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(reservationId);
        ArgumentOutOfRangeException.ThrowIfNegative(requestedCount);
        ArgumentOutOfRangeException.ThrowIfNegative(availableCount);
        if (expiresIn <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(expiresIn));

        if (requestedCount == 0)
            return Task.FromResult(0);

        var state = _organizations.GetOrAdd(organizationId, static _ => new ReservationState());
        lock (state.SyncRoot)
        {
            DateTimeOffset utcNow = timeProvider.GetUtcNow();
            RemoveExpired(state, utcNow);
            if (state.Reservations.TryGetValue(reservationId, out Reservation existing))
                return Task.FromResult(existing.Count);

            int admittedCount = (int)Math.Min(
                requestedCount,
                Math.Max(0L, (long)availableCount - state.ActiveCount));
            if (admittedCount == 0)
                return Task.FromResult(0);

            state.Reservations.Add(reservationId, new Reservation(admittedCount, utcNow.Add(expiresIn)));
            state.ActiveCount += admittedCount;
            return Task.FromResult(admittedCount);
        }
    }

    public Task ReleaseAsync(
        string organizationId,
        string reservationId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(organizationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(reservationId);

        if (!_organizations.TryGetValue(organizationId, out ReservationState? state))
            return Task.CompletedTask;

        lock (state.SyncRoot)
        {
            RemoveExpired(state, timeProvider.GetUtcNow());
            if (state.Reservations.Remove(reservationId, out Reservation reservation))
                state.ActiveCount -= reservation.Count;
        }

        return Task.CompletedTask;
    }

    private static void RemoveExpired(ReservationState state, DateTimeOffset utcNow)
    {
        string[] expiredReservationIds = state.Reservations
            .Where(pair => pair.Value.ExpiresUtc <= utcNow)
            .Select(pair => pair.Key)
            .ToArray();
        foreach (string reservationId in expiredReservationIds)
        {
            if (state.Reservations.Remove(reservationId, out Reservation reservation))
                state.ActiveCount -= reservation.Count;
        }
    }

    private sealed class ReservationState
    {
        public object SyncRoot { get; } = new();
        public Dictionary<string, Reservation> Reservations { get; } = new(StringComparer.Ordinal);
        public int ActiveCount { get; set; }
    }

    private readonly record struct Reservation(int Count, DateTimeOffset ExpiresUtc);
}
