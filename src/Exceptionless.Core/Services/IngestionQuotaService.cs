namespace Exceptionless.Core.Services;

public interface IIngestionQuotaService
{
    Task<int> ReserveAsync(string organizationId, int eventCount);
    Task CommitAsync(string organizationId, string projectId, int eventCount);
    Task ReleaseAsync(string organizationId, int eventCount);
    Task TrackBlockedAsync(string organizationId, string projectId, int eventCount);
    Task TrackDiscardedAsync(string organizationId, string projectId, int eventCount);
}

public sealed class IngestionQuotaService(UsageService usageService) : IIngestionQuotaService
{
    public Task<int> ReserveAsync(string organizationId, int eventCount) => usageService.ReserveEventsAsync(organizationId, eventCount);
    public Task CommitAsync(string organizationId, string projectId, int eventCount) => usageService.IncrementTotalAsync(organizationId, projectId, eventCount);
    public Task ReleaseAsync(string organizationId, int eventCount) => usageService.ReleaseEventReservationAsync(organizationId, eventCount);
    public Task TrackBlockedAsync(string organizationId, string projectId, int eventCount) => usageService.IncrementBlockedAsync(organizationId, projectId, eventCount);
    public Task TrackDiscardedAsync(string organizationId, string projectId, int eventCount) => usageService.IncrementDiscardedAsync(organizationId, projectId, eventCount);
}
