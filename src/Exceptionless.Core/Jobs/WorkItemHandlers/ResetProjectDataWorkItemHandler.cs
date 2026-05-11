using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Repositories;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Lock;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Jobs.WorkItemHandlers;

public class ResetProjectDataWorkItemHandler : WorkItemHandlerBase
{
    private readonly IEventRepository _eventRepository;
    private readonly IStackRepository _stackRepository;
    private readonly ICacheClient _cacheClient;
    private readonly ILockProvider _lockProvider;

    public ResetProjectDataWorkItemHandler(IEventRepository eventRepository, IStackRepository stackRepository, ICacheClient cacheClient, ILockProvider lockProvider, ILoggerFactory loggerFactory) : base(loggerFactory)
    {
        _eventRepository = eventRepository;
        _stackRepository = stackRepository;
        _cacheClient = cacheClient;
        _lockProvider = lockProvider;
    }

    public override Task<ILock?> GetWorkItemLockAsync(object workItem, CancellationToken cancellationToken = default)
    {
        string cacheKey = $"{nameof(ResetProjectDataWorkItemHandler)}:{((ResetProjectDataWorkItem)workItem).ProjectId}";
        return _lockProvider.TryAcquireAsync(cacheKey, TimeSpan.FromMinutes(15), cancellationToken);
    }

    public override async Task HandleItemAsync(WorkItemContext context)
    {
        var workItem = context.GetData<ResetProjectDataWorkItem>()!;

        using (Log.BeginScope(new ExceptionlessState().Organization(workItem.OrganizationId).Project(workItem.ProjectId)))
        {
            Log.LogInformation("Received reset project data work item for project: {ProjectId}", workItem.ProjectId);
            await context.ReportProgressAsync(0, "Starting project data reset...");

            long removedEvents = await _eventRepository.RemoveAllByProjectIdAsync(workItem.OrganizationId, workItem.ProjectId);
            await context.ReportProgressAsync(50, $"Events removed: {removedEvents}");

            long removedStacks = await _stackRepository.RemoveAllByProjectIdAsync(workItem.OrganizationId, workItem.ProjectId);
            await _cacheClient.RemoveByPrefixAsync(String.Concat("stack-filter:", workItem.OrganizationId, ":", workItem.ProjectId));

            await context.ReportProgressAsync(100, $"Events removed: {removedEvents}, stacks removed: {removedStacks}");
            Log.LogInformation("Reset project data for project {ProjectId}. Events removed: {RemovedEvents}, stacks removed: {RemovedStacks}", workItem.ProjectId, removedEvents, removedStacks);
        }
    }
}
