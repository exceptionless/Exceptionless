using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Repositories;
using Foundatio.Jobs;
using Foundatio.Lock;
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

    public override Task<ILock> GetWorkItemLockAsync(object workItem, CancellationToken cancellationToken = new())
    {
        var wi = (RemoveBotEventsWorkItem)workItem;
        string cacheKey = $"{nameof(RemoveBotEventsWorkItem)}:{wi.OrganizationId}:{wi.ProjectId}";
        return _lockProvider.AcquireAsync(cacheKey, TimeSpan.FromMinutes(15), cancellationToken);
    }

    public override async Task HandleItemAsync(WorkItemContext context)
    {
        var wi = context.GetData<RemoveBotEventsWorkItem>();
        using var _ = Log.BeginScope(new ExceptionlessState().Organization(wi.OrganizationId).Project(wi.ProjectId).Tag("Delete").Tag("Bot"));
        Log.LogInformation("Received remove bot events work item OrganizationId={Organization} ProjectId={Project}, ClientIpAddress={ClientIpAddress}, UtcStartDate={UtcStartDate}, UtcEndDate={UtcEndDate}", wi.OrganizationId, wi.ProjectId, wi.ClientIpAddress, wi.UtcStartDate, wi.UtcEndDate);

        await context.ReportProgressAsync(0, $"Starting deleting of bot events... OrganizationId={wi.OrganizationId}");
        long deleted = await _eventRepository.RemoveAllAsync(wi.OrganizationId, wi.ClientIpAddress, wi.UtcStartDate, wi.UtcEndDate);
        await context.ReportProgressAsync(100, $"Bot events deleted: {deleted} OrganizationId={wi.OrganizationId}");
        Log.LogInformation("Removed {Deleted} bot events OrganizationId={OrganizationId} ProjectId={ProjectId}, ClientIpAddress={ClientIpAddress}, UtcStartDate={UtcStartDate}, UtcEndDate={UtcEndDate}", deleted, wi.OrganizationId, wi.ProjectId, wi.ClientIpAddress, wi.UtcStartDate, wi.UtcEndDate);
    }
}
