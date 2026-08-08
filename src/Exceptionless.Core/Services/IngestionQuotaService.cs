using Exceptionless.Core.Models.Ingestion;

namespace Exceptionless.Core.Services;

public interface IIngestionQuotaService
{
    Task<EventIngestionReservation> ReserveAsync(string organizationId, int eventCount);
    Task CommitAsync(string organizationId, string projectId, IReadOnlyCollection<EventUsageSettlement> settlements);
    Task ReleaseAsync(EventIngestionReservation reservation);
    Task TrackBlockedAsync(string organizationId, string projectId, int eventCount);
    Task TrackDiscardedAsync(string organizationId, string projectId, int eventCount);
}

public sealed class IngestionQuotaService(UsageService usageService) : IIngestionQuotaService
{
    public Task<EventIngestionReservation> ReserveAsync(string organizationId, int eventCount) => usageService.ReserveEventsAsync(organizationId, eventCount);
    public Task CommitAsync(string organizationId, string projectId, IReadOnlyCollection<EventUsageSettlement> settlements) =>
        usageService.IncrementTotalAsync(organizationId, projectId, settlements);
    public Task ReleaseAsync(EventIngestionReservation reservation) => usageService.ReleaseEventReservationAsync(reservation);
    public Task TrackBlockedAsync(string organizationId, string projectId, int eventCount) => usageService.IncrementBlockedAsync(organizationId, projectId, eventCount);
    public Task TrackDiscardedAsync(string organizationId, string projectId, int eventCount) => usageService.IncrementDiscardedAsync(organizationId, projectId, eventCount);
}
