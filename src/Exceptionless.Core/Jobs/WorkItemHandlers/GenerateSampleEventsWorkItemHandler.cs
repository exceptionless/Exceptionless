using Exceptionless.Core.Models;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Repositories;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Jobs.WorkItemHandlers;

public class GenerateSampleEventsWorkItemHandler : WorkItemHandlerBase
{
    private readonly EventPipeline _eventPipeline;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly ILockProvider _lockProvider;
    private readonly TimeProvider _timeProvider;

    public GenerateSampleEventsWorkItemHandler(
        EventPipeline eventPipeline,
        IOrganizationRepository organizationRepository,
        IProjectRepository projectRepository,
        ILockProvider lockProvider,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory) : base(loggerFactory)
    {
        _eventPipeline = eventPipeline;
        _organizationRepository = organizationRepository;
        _projectRepository = projectRepository;
        _lockProvider = lockProvider;
        _timeProvider = timeProvider;
    }

    public override Task<ILock?> GetWorkItemLockAsync(object workItem, CancellationToken cancellationToken = default)
    {
        return _lockProvider.AcquireAsync(nameof(GenerateSampleEventsWorkItemHandler), TimeSpan.FromMinutes(30), cancellationToken);
    }

    public override async Task HandleItemAsync(WorkItemContext context)
    {
        var workItem = context.GetData<GenerateSampleEventsWorkItem>()!;
        int eventCount = Math.Clamp(workItem.EventCount, 1, 10000);
        int daysBack = Math.Clamp(workItem.DaysBack, 1, 365);

        Log.LogInformation("Generating {EventCount} sample events over {DaysBack} days", eventCount, daysBack);
        await context.ReportProgressAsync(0, $"Generating {eventCount} sample events");

        var generator = new RandomEventGenerator(_timeProvider);
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        var minDate = utcNow.AddDays(-daysBack);

        var projectResults = await _projectRepository.GetByOrganizationIdAsync(SampleDataService.TEST_ORG_ID);
        var projectList = projectResults.Documents.ToList();
        if (projectList.Count == 0)
        {
            Log.LogWarning("No projects found for sample organization {OrganizationId}", SampleDataService.TEST_ORG_ID);
            return;
        }

        var organization = await _organizationRepository.GetByIdAsync(SampleDataService.TEST_ORG_ID);
        if (organization is null)
        {
            Log.LogWarning("Sample organization {OrganizationId} not found", SampleDataService.TEST_ORG_ID);
            return;
        }

        int eventsPerProject = eventCount / projectList.Count;
        int remainder = eventCount % projectList.Count;
        int totalProcessed = 0;
        const int batchSize = 50;

        for (int p = 0; p < projectList.Count; p++)
        {
            if (context.CancellationToken.IsCancellationRequested)
                break;

            var project = projectList[p];
            int projectEventCount = eventsPerProject + (p < remainder ? 1 : 0);

            var events = generator.Generate(organization.Id, project.Id, projectEventCount, minDate, utcNow);

            for (int i = 0; i < events.Count; i += batchSize)
            {
                if (context.CancellationToken.IsCancellationRequested)
                    break;

                var batch = events.Skip(i).Take(batchSize).ToList();
                await _eventPipeline.RunAsync(batch, organization, project);
                totalProcessed += batch.Count;

                int percentage = (int)Math.Min(99, totalProcessed * 100.0 / eventCount);
                await context.ReportProgressAsync(percentage, $"Processed {totalProcessed}/{eventCount} events");
            }
        }

        await context.ReportProgressAsync(100, $"Generated {totalProcessed} sample events across {projectList.Count} projects");
        Log.LogInformation("Generated {TotalEvents} sample events across {ProjectCount} projects", totalProcessed, projectList.Count);
    }
}
