using Exceptionless.Core.Models;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Configuration;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Repositories;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Jobs.WorkItemHandlers;

public class RemoveBotEventsWorkItemHandler : WorkItemHandlerBase
{
    private readonly IEventRepository _eventRepository;
    private readonly ILockProvider _lockProvider;

    public RemoveBotEventsWorkItemHandler(IEventRepository eventRepository, ILockProvider lockProvider, ILoggerFactory loggerFactory) : base(loggerFactory)
    {
        _eventRepository = eventRepository;
        _lockProvider = lockProvider;
    }

    public override Task<ILock?> GetWorkItemLockAsync(object workItem, CancellationToken cancellationToken = default)
    {
        var wi = (RemoveBotEventsWorkItem)workItem;
        string cacheKey = $"{nameof(RemoveBotEventsWorkItem)}:{wi.OrganizationId}:{wi.ProjectId}";
        return _lockProvider.TryAcquireAsync(cacheKey, TimeSpan.FromMinutes(15), cancellationToken);
    }

    public override async Task HandleItemAsync(WorkItemContext context)
    {
        var wi = context.GetData<RemoveBotEventsWorkItem>()!;
        ArgumentException.ThrowIfNullOrWhiteSpace(wi.OrganizationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(wi.ProjectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(wi.ClientIpAddress);

        using var _ = Log.BeginScope(new ExceptionlessState().Organization(wi.OrganizationId).Project(wi.ProjectId).Tag("Delete").Tag("Bot"));
        Log.LogInformation("Received remove bot events work item OrganizationId={OrganizationId} ProjectId={ProjectId}, ClientIpAddress={ClientIpAddress}, UtcStartDate={UtcStartDate}, UtcEndDate={UtcEndDate}", wi.OrganizationId, wi.ProjectId, wi.ClientIpAddress, wi.UtcStartDate, wi.UtcEndDate);

        await context.ReportProgressAsync(0, $"Starting deleting of bot events... OrganizationId={wi.OrganizationId}");
        var query = new RepositoryQuery<PersistentEvent>()
            .Organization(wi.OrganizationId)
            .Project(wi.ProjectId)
            .DateRange(wi.UtcStartDate, wi.UtcEndDate, (PersistentEvent e) => e.Date)
            .Index(wi.UtcStartDate, wi.UtcEndDate)
            .FieldEquals(EventIndex.Alias.IpAddress, wi.ClientIpAddress);
        long deleted = await _eventRepository.RemoveAllAsync(q => query);
        await context.ReportProgressAsync(100, $"Bot events deleted: {deleted} OrganizationId={wi.OrganizationId}");
        Log.LogInformation("Removed {Deleted} bot events OrganizationId={OrganizationId} ProjectId={ProjectId}, ClientIpAddress={ClientIpAddress}, UtcStartDate={UtcStartDate}, UtcEndDate={UtcEndDate}", deleted, wi.OrganizationId, wi.ProjectId, wi.ClientIpAddress, wi.UtcStartDate, wi.UtcEndDate);
    }
}
