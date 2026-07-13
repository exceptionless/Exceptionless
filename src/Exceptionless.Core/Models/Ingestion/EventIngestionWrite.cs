using Exceptionless.Core.Models;

namespace Exceptionless.Core.Models.Ingestion;

public sealed record EventIngestionWrite(
    string ClientId,
    PersistentEvent Event,
    StackFingerprint Fingerprint,
    StackRoute? Route,
    bool IsRegressionCandidate = false);

public sealed record EventIngestionIdentity(
    string ClientId,
    string EventId,
    bool IsDuplicate,
    bool IsPersisted,
    string? PersistedStackId,
    StackStatus? PersistedStackStatus,
    bool IsRecoveryEligible,
    DateTime CreatedUtc,
    DateTimeOffset EventDate);

public sealed record EventIngestionReconciliation(
    string EventId,
    string StackId);

public sealed record EventUsageSettlement(string EventId, DateTime CreatedUtc);

public sealed record EventBatchWriteResult(
    int Persisted,
    int Duplicate,
    IReadOnlyCollection<EventUsageSettlement> Settlements);

public sealed record EventIngestionReservation(
    string Id,
    string OrganizationId,
    int Count,
    bool IsUnlimited = false)
{
    public static EventIngestionReservation Unlimited(string organizationId, int count) => new(String.Empty, organizationId, count, true);
}

public sealed class EventBatchWriteException(Exception innerException, IReadOnlyCollection<EventUsageSettlement> settlements)
    : Exception("Event persistence completed only partially.", innerException)
{
    public IReadOnlyCollection<EventUsageSettlement> Settlements { get; } = settlements;
}
