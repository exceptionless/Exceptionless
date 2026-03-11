using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Repositories;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Jobs.WorkItemHandlers;

public class UpdateProjectNotificationSettingsWorkItemHandler : WorkItemHandlerBase
{
    private const int BATCH_SIZE = 50;

    private readonly IOrganizationRepository _organizationRepository;
    private readonly OrganizationService _organizationService;
    private readonly ILockProvider _lockProvider;
    private readonly TimeProvider _timeProvider;

    public UpdateProjectNotificationSettingsWorkItemHandler(
        IOrganizationRepository organizationRepository,
        OrganizationService organizationService,
        ILockProvider lockProvider,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory) : base(loggerFactory)
    {
        _organizationRepository = organizationRepository;
        _organizationService = organizationService;
        _lockProvider = lockProvider;
        _timeProvider = timeProvider;
    }

    public override Task<ILock> GetWorkItemLockAsync(object workItem, CancellationToken cancellationToken = new())
    {
        return _lockProvider.AcquireAsync(nameof(UpdateProjectNotificationSettingsWorkItemHandler), TimeSpan.FromMinutes(15), cancellationToken);
    }

    public override async Task HandleItemAsync(WorkItemContext context)
    {
        var workItem = context.GetData<UpdateProjectNotificationSettingsWorkItem>();
        Log.LogInformation("Received update project notification settings work item. Organization={Organization}", workItem.OrganizationId);

        long totalNotificationSettingsRemoved = 0;
        long organizationsProcessed = 0;

        if (!String.IsNullOrEmpty(workItem.OrganizationId))
        {
            await context.ReportProgressAsync(0, $"Starting project notification settings update for organization {workItem.OrganizationId}");

            var organization = await _organizationRepository.GetByIdAsync(workItem.OrganizationId);
            if (organization is null)
            {
                Log.LogWarning("Organization {Organization} not found", workItem.OrganizationId);
                return;
            }

            totalNotificationSettingsRemoved += await _organizationService.CleanupProjectNotificationSettingsAsync(
                organization,
                [],
                context.CancellationToken,
                context.RenewLockAsync);
            organizationsProcessed++;
        }
        else
        {
            await context.ReportProgressAsync(0, "Starting project notification settings update for all organizations");

            var results = await _organizationRepository.FindAsync(
                q => q.Include(o => o.Id),
                o => o.SearchAfterPaging().PageLimit(BATCH_SIZE));

            long totalOrganizations = results.Total;

            while (results.Documents.Count > 0 && !context.CancellationToken.IsCancellationRequested)
            {
                foreach (var organization in results.Documents)
                {
                    totalNotificationSettingsRemoved += await _organizationService.CleanupProjectNotificationSettingsAsync(
                        organization,
                        [],
                        context.CancellationToken,
                        context.RenewLockAsync);
                    organizationsProcessed++;
                }

                int percentage = totalOrganizations > 0
                    ? (int)Math.Min(99, organizationsProcessed * 100.0 / totalOrganizations)
                    : 99;
                await context.ReportProgressAsync(percentage, $"Processed {organizationsProcessed}/{totalOrganizations} organizations, removed {totalNotificationSettingsRemoved} invalid notification settings");

                await Task.Delay(TimeSpan.FromSeconds(2.5), _timeProvider);

                if (context.CancellationToken.IsCancellationRequested || !await results.NextPageAsync())
                    break;

                if (results.Documents.Count > 0)
                    await context.RenewLockAsync();
            }
        }

        Log.LogInformation("Project notification settings update complete. Organizations processed: {OrganizationsProcessed}, invalid notification settings removed: {RemovedNotificationSettings}", organizationsProcessed, totalNotificationSettingsRemoved);
    }
}
