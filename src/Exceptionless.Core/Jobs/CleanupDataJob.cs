using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services;
using Exceptionless.DateTimeExtensions;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;
using Foundatio.Utility;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Jobs;

[Job(Description = "Deletes soft deleted data and enforces data retention.", IsContinuous = false)]
public class CleanupDataJob : JobWithLockBase, IHealthCheck
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly OrganizationService _organizationService;
    private readonly IProjectRepository _projectRepository;
    private readonly IStackRepository _stackRepository;
    private readonly IEventRepository _eventRepository;
    private readonly ITokenRepository _tokenRepository;
    private readonly IWebHookRepository _webHookRepository;
    private readonly BillingManager _billingManager;
    private readonly AppOptions _appOptions;
    private readonly ILockProvider _lockProvider;
    private readonly ICacheClient _cacheClient;
    private DateTime? _lastRun;

    public CleanupDataJob(
        IOrganizationRepository organizationRepository,
        OrganizationService organizationService,
        IProjectRepository projectRepository,
        IStackRepository stackRepository,
        IEventRepository eventRepository,
        ITokenRepository tokenRepository,
        IWebHookRepository webHookRepository,
        ILockProvider lockProvider,
        ICacheClient cacheClient,
        BillingManager billingManager,
        AppOptions appOptions,
        ILoggerFactory loggerFactory
    ) : base(loggerFactory)
    {
        _organizationRepository = organizationRepository;
        _organizationService = organizationService;
        _projectRepository = projectRepository;
        _stackRepository = stackRepository;
        _eventRepository = eventRepository;
        _tokenRepository = tokenRepository;
        _webHookRepository = webHookRepository;
        _billingManager = billingManager;
        _appOptions = appOptions;
        _lockProvider = lockProvider;
        _cacheClient = cacheClient;
    }

    protected override Task<ILock> GetLockAsync(CancellationToken cancellationToken = default)
    {
        return _lockProvider.AcquireAsync(nameof(CleanupDataJob), TimeSpan.FromMinutes(15), new CancellationToken(true));
    }

    protected override async Task<JobResult> RunInternalAsync(JobContext context)
    {
        _lastRun = SystemClock.UtcNow;

        await MarkTokensSuspended(context).AnyContext();
        await CleanupSoftDeletedOrganizationsAsync(context).AnyContext();
        await CleanupSoftDeletedProjectsAsync(context).AnyContext();
        await CleanupSoftDeletedStacksAsync(context).AnyContext();

        await EnforceRetentionAsync(context).AnyContext();

        _logger.CleanupFinished();

        return JobResult.Success;
    }

    private async Task MarkTokensSuspended(JobContext context)
    {
        var suspendedOrgs = await _organizationRepository.FindAsync(q => q.FieldEquals(o => o.IsSuspended, true).OnlyIds(), o => o.SearchAfterPaging().PageLimit(1000));
        _logger.LogInformation("Found {SuspendedOrgCount} suspended orgs", suspendedOrgs.Total);
        if (suspendedOrgs.Total == 0)
            return;

        do
        {
            var updatedCount = await _tokenRepository.PatchAllAsync(q => q.Organization(suspendedOrgs.Hits.Select(o => o.Id)).FieldEquals(t => t.IsSuspended, false), new PartialPatch(new { is_suspended = true }));
            if (updatedCount > 0)
                _logger.LogInformation("Marking {SuspendedTokenCount} tokens as suspended", updatedCount);
        } while (!context.CancellationToken.IsCancellationRequested && await suspendedOrgs.NextPageAsync());
    }

    private async Task CleanupSoftDeletedOrganizationsAsync(JobContext context)
    {
        var organizationResults = await _organizationRepository.GetAllAsync(o => o.SoftDeleteMode(SoftDeleteQueryMode.DeletedOnly).SearchAfterPaging().PageLimit(5)).AnyContext();
        _logger.CleanupOrganizationSoftDeletes(organizationResults.Total);

        while (organizationResults.Documents.Count > 0 && !context.CancellationToken.IsCancellationRequested)
        {
            foreach (var organization in organizationResults.Documents)
            {
                using var _ = _logger.BeginScope(new ExceptionlessState().Organization(organization.Id));
                try
                {
                    await RemoveOrganizationAsync(organization, context).AnyContext();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error removing soft deleted organization {OrganizationId}: {Message}", organization.Id, ex.Message);
                }

                // Sleep so we are not hammering the backend.
                await SystemClock.SleepAsync(TimeSpan.FromSeconds(2.5)).AnyContext();
            }

            if (context.CancellationToken.IsCancellationRequested || !await organizationResults.NextPageAsync().AnyContext())
                break;
        }
    }

    private async Task CleanupSoftDeletedProjectsAsync(JobContext context)
    {
        var projectResults = await _projectRepository.GetAllAsync(o => o.SoftDeleteMode(SoftDeleteQueryMode.DeletedOnly).SearchAfterPaging().PageLimit(5)).AnyContext();
        _logger.CleanupProjectSoftDeletes(projectResults.Total);

        while (projectResults.Documents.Count > 0 && !context.CancellationToken.IsCancellationRequested)
        {
            foreach (var project in projectResults.Documents)
            {
                using var _ = _logger.BeginScope(new ExceptionlessState().Organization(project.OrganizationId).Project(project.Id));
                try
                {
                    await RemoveProjectsAsync(project, context).AnyContext();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error removing soft deleted project {ProjectId}: {Message}", project.Id, ex.Message);
                }

                // Sleep so we are not hammering the backend.
                await SystemClock.SleepAsync(TimeSpan.FromSeconds(2.5)).AnyContext();
            }

            if (context.CancellationToken.IsCancellationRequested || !await projectResults.NextPageAsync().AnyContext())
                break;
        }
    }

    private async Task CleanupSoftDeletedStacksAsync(JobContext context)
    {
        var stackResults = await _stackRepository.GetSoftDeleted().AnyContext();
        _logger.CleanupStackSoftDeletes(stackResults.Total);

        while (stackResults.Documents.Count > 0 && !context.CancellationToken.IsCancellationRequested)
        {
            try
            {
                await RemoveStacksAsync(stackResults.Documents, context).AnyContext();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing soft deleted stacks: {Message}", ex.Message);
            }

            if (context.CancellationToken.IsCancellationRequested || !await stackResults.NextPageAsync().AnyContext())
                break;
        }
    }

    private async Task RemoveOrganizationAsync(Organization organization, JobContext context)
    {
        _logger.RemoveOrganizationStart(organization.Name, organization.Id);
        await _organizationService.RemoveTokensAsync(organization).AnyContext();
        await _organizationService.RemoveWebHooksAsync(organization).AnyContext();
        await _organizationService.CancelSubscriptionsAsync(organization).AnyContext();
        await _organizationService.RemoveUsersAsync(organization, null).AnyContext();

        await RenewLockAsync(context).AnyContext();
        long removedEvents = await _eventRepository.RemoveAllByOrganizationIdAsync(organization.Id).AnyContext();

        await RenewLockAsync(context).AnyContext();
        long removedStacks = await _stackRepository.RemoveAllByOrganizationIdAsync(organization.Id).AnyContext();

        await RenewLockAsync(context).AnyContext();
        long removedProjects = await _projectRepository.RemoveAllByOrganizationIdAsync(organization.Id).AnyContext();

        await _organizationRepository.RemoveAsync(organization).AnyContext();
        _logger.RemoveOrganizationComplete(organization.Name, organization.Id, removedProjects, removedStacks, removedEvents);
    }

    private async Task RemoveProjectsAsync(Project project, JobContext context)
    {
        _logger.RemoveProjectStart(project.Name, project.Id);
        await _tokenRepository.RemoveAllByProjectIdAsync(project.OrganizationId, project.Id).AnyContext();
        await _webHookRepository.RemoveAllByProjectIdAsync(project.OrganizationId, project.Id).AnyContext();

        await RenewLockAsync(context).AnyContext();
        long removedEvents = await _eventRepository.RemoveAllByProjectIdAsync(project.OrganizationId, project.Id).AnyContext();

        await RenewLockAsync(context).AnyContext();
        long removedStacks = await _stackRepository.RemoveAllByProjectIdAsync(project.OrganizationId, project.Id).AnyContext();

        await _projectRepository.RemoveAsync(project).AnyContext();
        _logger.RemoveProjectComplete(project.Name, project.Id, removedStacks, removedEvents);
    }

    private async Task RemoveStacksAsync(IReadOnlyCollection<Stack> stacks, JobContext context)
    {
        await RenewLockAsync(context).AnyContext();

        string[] stackIds = stacks.Select(s => s.Id).ToArray();
        long removedEvents = await _eventRepository.RemoveAllByStackIdsAsync(stackIds).AnyContext();
        await _stackRepository.RemoveAsync(stacks).AnyContext();
        foreach (var orgGroup in stacks.GroupBy(s => (s.OrganizationId, s.ProjectId)))
            await _cacheClient.RemoveByPrefixAsync(String.Concat("stack-filter:", orgGroup.Key.OrganizationId, ":", orgGroup.Key.ProjectId));

        _logger.RemoveStacksComplete(stackIds.Length, removedEvents);
    }

    private async Task EnforceRetentionAsync(JobContext context)
    {
        var results = await _organizationRepository.FindAsync(q => q.Include(o => o.Id, o => o.Name, o => o.RetentionDays), o => o.SearchAfterPaging().PageLimit(100)).AnyContext();
        while (results.Documents.Count > 0 && !context.CancellationToken.IsCancellationRequested)
        {
            foreach (var organization in results.Documents)
            {
                using var _ = _logger.BeginScope(new ExceptionlessState().Organization(organization.Id));

                int retentionDays = _billingManager.GetBillingPlanByUpsellingRetentionPeriod(organization.RetentionDays)?.RetentionDays ?? _appOptions.MaximumRetentionDays;
                if (retentionDays <= 0)
                    retentionDays = _appOptions.MaximumRetentionDays;
                retentionDays = Math.Min(retentionDays, _appOptions.MaximumRetentionDays);

                try
                {
                    // adding 60 days to retention in order to keep track of whether a stack is new or not
                    await EnforceStackRetentionDaysAsync(organization, retentionDays + 60, context).AnyContext();
                    await EnforceEventRetentionDaysAsync(organization, retentionDays, context).AnyContext();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error enforcing retention for Organization {OrganizationId}: {Message}", organization.Id, ex.Message);
                }

                // Sleep so we are not hammering the backend.
                await SystemClock.SleepAsync(TimeSpan.FromSeconds(2.5)).AnyContext();
            }

            if (context.CancellationToken.IsCancellationRequested || !await results.NextPageAsync().AnyContext())
                break;
        }
    }

    private async Task EnforceStackRetentionDaysAsync(Organization organization, int retentionDays, JobContext context)
    {
        await RenewLockAsync(context).AnyContext();

        var cutoff = SystemClock.UtcNow.Date.SubtractDays(retentionDays);
        var stackResults = await _stackRepository.GetStacksForCleanupAsync(organization.Id, cutoff).AnyContext();
        _logger.RetentionEnforcementStackStart(cutoff, organization.Name, organization.Id, stackResults.Total);

        while (stackResults.Documents.Count > 0 && !context.CancellationToken.IsCancellationRequested)
        {
            try
            {
                await RemoveStacksAsync(stackResults.Documents, context).AnyContext();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing stacks: {Message}", ex.Message);
            }

            if (context.CancellationToken.IsCancellationRequested || !await stackResults.NextPageAsync().AnyContext())
                break;
        }

        _logger.RetentionEnforcementStackComplete(organization.Name, organization.Id, stackResults.Documents.Count);
    }

    private async Task EnforceEventRetentionDaysAsync(Organization organization, int retentionDays, JobContext context)
    {
        await RenewLockAsync(context).AnyContext();

        var cutoff = SystemClock.UtcNow.Date.SubtractDays(retentionDays);
        _logger.RetentionEnforcementEventStart(cutoff, organization.Name, organization.Id);

        long removedEvents = await _eventRepository.RemoveAllAsync(organization.Id, null, null, cutoff).AnyContext();
        _logger.RetentionEnforcementEventComplete(organization.Name, organization.Id, removedEvents);
    }

    private Task RenewLockAsync(JobContext context)
    {
        _lastRun = SystemClock.UtcNow;
        return context.RenewLockAsync();
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!_lastRun.HasValue)
            return Task.FromResult(HealthCheckResult.Healthy("Job has not been run yet."));

        if (SystemClock.UtcNow.Subtract(_lastRun.Value) > TimeSpan.FromMinutes(65))
            return Task.FromResult(HealthCheckResult.Unhealthy("Job has not run in the last 65 minutes."));

        return Task.FromResult(HealthCheckResult.Healthy("Job has run in the last 65 minutes."));
    }
}
