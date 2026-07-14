using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Queries;
using Exceptionless.Core.Services;
using Exceptionless.Core.Utility;
using Exceptionless.DateTimeExtensions;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;
using Foundatio.Resilience;
using Foundatio.Storage;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Jobs;

[Job(Description = "Deletes soft deleted data and enforces data retention.", IsContinuous = false)]
public class CleanupDataJob : JobWithLockBase, IHealthCheck
{
    private static readonly TimeSpan OAuthTokenCleanupSafetyWindow = TimeSpan.FromDays(1);
    private static readonly TimeSpan SyntheticOrganizationCleanupSafetyWindow = TimeSpan.FromDays(1);
    private static readonly TimeSpan SyntheticUserCleanupSafetyWindow = TimeSpan.FromDays(1);
    private const string SyntheticOrganizationNamePrefix = "E2E Playwright Org";
    private const string SyntheticUserEmailPrefix = "playwright-";
    private const string SyntheticUserEmailSuffix = "@exceptionless.test";
    private const string SyntheticUserFullNamePrefix = "Playwright User";

    private readonly IOrganizationRepository _organizationRepository;
    private readonly OrganizationService _organizationService;
    private readonly IUserRepository _userRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IStackRepository _stackRepository;
    private readonly IEventRepository _eventRepository;
    private readonly ITokenRepository _tokenRepository;
    private readonly IOAuthTokenRepository _oauthTokenRepository;
    private readonly IWebHookRepository _webHookRepository;
    private readonly BillingManager _billingManager;
    private readonly UsageService _usageService;
    private readonly AppOptions _appOptions;
    private readonly ILockProvider _lockProvider;
    private readonly ICacheClient _cacheClient;
    private readonly IFileStorage _fileStorage;
    private DateTime? _lastRun;

    public CleanupDataJob(
        IOrganizationRepository organizationRepository,
        OrganizationService organizationService,
        IUserRepository userRepository,
        IProjectRepository projectRepository,
        IStackRepository stackRepository,
        IEventRepository eventRepository,
        ITokenRepository tokenRepository,
        IOAuthTokenRepository oauthTokenRepository,
        IWebHookRepository webHookRepository,
        ILockProvider lockProvider,
        ICacheClient cacheClient,
        IFileStorage fileStorage,
        BillingManager billingManager,
        UsageService usageService,
        AppOptions appOptions,
        TimeProvider timeProvider,
        IResiliencePolicyProvider resiliencePolicyProvider,
        ILoggerFactory loggerFactory
    ) : base(timeProvider, resiliencePolicyProvider, loggerFactory)
    {
        _organizationRepository = organizationRepository;
        _organizationService = organizationService;
        _userRepository = userRepository;
        _projectRepository = projectRepository;
        _stackRepository = stackRepository;
        _eventRepository = eventRepository;
        _tokenRepository = tokenRepository;
        _oauthTokenRepository = oauthTokenRepository;
        _webHookRepository = webHookRepository;
        _billingManager = billingManager;
        _usageService = usageService;
        _appOptions = appOptions;
        _lockProvider = lockProvider;
        _cacheClient = cacheClient;
        _fileStorage = fileStorage;
    }

    protected override Task<ILock?> GetLockAsync(CancellationToken cancellationToken = default)
    {
        return _lockProvider.TryAcquireAsync(nameof(CleanupDataJob), TimeSpan.FromMinutes(15), cancellationToken);
    }

    protected override async Task<JobResult> RunInternalAsync(JobContext context)
    {
        _lastRun = _timeProvider.GetUtcNow().UtcDateTime;

        await MarkTokensSuspended(context);
        await CleanupOAuthTokensAsync(context);
        await CleanupSyntheticOrganizationsAsync(context);
        await CleanupSyntheticUsersAsync(context);
        await CleanupSoftDeletedOrganizationsAsync(context);
        await CleanupSoftDeletedProjectsAsync(context);
        await CleanupSoftDeletedStacksAsync(context);

        await EnforceRetentionAsync(context);

        _logger.CleanupFinished();

        return JobResult.Success;
    }

    private async Task MarkTokensSuspended(JobContext context)
    {
        var suspendedOrganizations = await _organizationRepository.FindAsync(q => q.FieldEquals(o => o.IsSuspended, true).OnlyIds(), o => o.SearchAfterPaging().PageLimit(1000));
        _logger.LogInformation("Found {SuspendedOrganizationCount} suspended organizations", suspendedOrganizations.Total);
        if (suspendedOrganizations.Total == 0)
            return;

        do
        {
            var suspendedOrganizationIds = suspendedOrganizations.Hits.Where(o => o.Id is not null).Select(o => o.Id!).ToList();
            long updatedCount = await _tokenRepository.PatchAllAsync(q => q.Organization(suspendedOrganizationIds).FieldEquals(t => t.IsSuspended, false), new PartialPatch(new { is_suspended = true }));
            if (updatedCount > 0)
                _logger.LogInformation("Marking {SuspendedTokenCount} tokens as suspended", updatedCount);
        } while (!context.CancellationToken.IsCancellationRequested && await suspendedOrganizations.NextPageAsync());
    }

    private async Task CleanupOAuthTokensAsync(JobContext context)
    {
        var utcCutoff = _timeProvider.GetUtcNow().UtcDateTime.Subtract(OAuthTokenCleanupSafetyWindow);
        long removed = await _oauthTokenRepository.RemoveExpiredDisabledAsync(utcCutoff, context.CancellationToken);
        _logger.LogInformation("Removed {OAuthTokenCount} expired disabled OAuth token(s)", removed);
    }

    private async Task CleanupSyntheticOrganizationsAsync(JobContext context)
    {
        var utcCutoff = _timeProvider.GetUtcNow().UtcDateTime.Subtract(SyntheticOrganizationCleanupSafetyWindow);
        var organizationResults = await _organizationRepository.FindAsync(q => q
            .FilterExpression($"name:\"{SyntheticOrganizationNamePrefix}\"")
            .DateRange(null, utcCutoff, (Organization organization) => organization.CreatedUtc)
            .SortAscending(organization => organization.Id), o => o.SearchAfterPaging().PageLimit(5));

        _logger.LogInformation("Found {SyntheticOrganizationCount} synthetic E2E organization(s) older than {SyntheticOrganizationCutoff}", organizationResults.Total, utcCutoff);

        while (organizationResults.Documents.Count > 0 && !context.CancellationToken.IsCancellationRequested)
        {
            foreach (var organization in organizationResults.Documents.Where(IsSyntheticOrganization))
            {
                using var _ = _logger.BeginScope(new ExceptionlessState().Organization(organization.Id));
                try
                {
                    await RemoveOrganizationAsync(organization, context);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error removing synthetic E2E organization {OrganizationId}: {Message}", organization.Id, ex.Message);
                }

                // Sleep so we are not hammering the backend.
                await Task.Delay(TimeSpan.FromSeconds(2.5), _timeProvider);
            }

            if (context.CancellationToken.IsCancellationRequested || !await organizationResults.NextPageAsync())
                break;
        }
    }

    private async Task CleanupSyntheticUsersAsync(JobContext context)
    {
        var utcCutoff = _timeProvider.GetUtcNow().UtcDateTime.Subtract(SyntheticUserCleanupSafetyWindow);
        var userResults = await _userRepository.FindAsync(q => q
            .FilterExpression($"email_address:{SyntheticUserEmailPrefix}*")
            .DateRange(null, utcCutoff, (User user) => user.CreatedUtc)
            .SortAscending(user => user.Id), o => o.SearchAfterPaging().PageLimit(25));

        _logger.LogInformation("Found {SyntheticUserCount} synthetic E2E user(s) older than {SyntheticUserCutoff}", userResults.Total, utcCutoff);

        while (userResults.Documents.Count > 0 && !context.CancellationToken.IsCancellationRequested)
        {
            foreach (var user in userResults.Documents.Where(IsStandaloneSyntheticUser))
            {
                using var _ = _logger.BeginScope(new ExceptionlessState().Identity(user.EmailAddress));
                try
                {
                    await RemoveSyntheticUserAsync(user);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error removing synthetic E2E user {UserId}: {Message}", user.Id, ex.Message);
                }
            }

            if (context.CancellationToken.IsCancellationRequested || !await userResults.NextPageAsync())
                break;
        }
    }

    private async Task CleanupSoftDeletedOrganizationsAsync(JobContext context)
    {
        var organizationResults = await _organizationRepository.GetAllAsync(o => o.SoftDeleteMode(SoftDeleteQueryMode.DeletedOnly).SearchAfterPaging().PageLimit(5));
        _logger.CleanupOrganizationSoftDeletes(organizationResults.Total);

        while (organizationResults.Documents.Count > 0 && !context.CancellationToken.IsCancellationRequested)
        {
            foreach (var organization in organizationResults.Documents)
            {
                using var _ = _logger.BeginScope(new ExceptionlessState().Organization(organization.Id));
                try
                {
                    await RemoveOrganizationAsync(organization, context);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error removing soft deleted organization {OrganizationId}: {Message}", organization.Id, ex.Message);
                }

                // Sleep so we are not hammering the backend.
                await Task.Delay(TimeSpan.FromSeconds(2.5), _timeProvider);
            }

            if (context.CancellationToken.IsCancellationRequested || !await organizationResults.NextPageAsync())
                break;
        }
    }

    private async Task CleanupSoftDeletedProjectsAsync(JobContext context)
    {
        var projectResults = await _projectRepository.GetAllAsync(o => o.SoftDeleteMode(SoftDeleteQueryMode.DeletedOnly).SearchAfterPaging().PageLimit(5));
        _logger.CleanupProjectSoftDeletes(projectResults.Total);

        while (projectResults.Documents.Count > 0 && !context.CancellationToken.IsCancellationRequested)
        {
            foreach (var project in projectResults.Documents)
            {
                using var _ = _logger.BeginScope(new ExceptionlessState().Organization(project.OrganizationId).Project(project.Id));
                try
                {
                    await RemoveProjectsAsync(project, context);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error removing soft deleted project {ProjectId}: {Message}", project.Id, ex.Message);
                }

                // Sleep so we are not hammering the backend.
                await Task.Delay(TimeSpan.FromSeconds(2.5), _timeProvider);
            }

            if (context.CancellationToken.IsCancellationRequested || !await projectResults.NextPageAsync())
                break;
        }
    }

    private async Task CleanupSoftDeletedStacksAsync(JobContext context)
    {
        var stackResults = await _stackRepository.GetSoftDeleted();
        _logger.CleanupStackSoftDeletes(stackResults.Total);

        while (stackResults.Documents.Count > 0 && !context.CancellationToken.IsCancellationRequested)
        {
            try
            {
                await RemoveStacksAsync(stackResults.Documents, context, trackDeletedUsage: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing soft deleted stacks: {Message}", ex.Message);
            }

            if (context.CancellationToken.IsCancellationRequested || !await stackResults.NextPageAsync())
                break;
        }
    }

    private async Task RemoveOrganizationAsync(Organization organization, JobContext context)
    {
        _logger.RemoveOrganizationStart(organization.Name, organization.Id);
        await _organizationService.RemoveTokensAsync(organization);
        await _organizationService.RemoveWebHooksAsync(organization);
        await _organizationService.RemoveSavedViewsAsync(organization);
        await _organizationService.CancelSubscriptionsAsync(organization);
        await _organizationService.RemoveUsersAsync(organization, null);

        await RenewLockAsync(context);
        long removedEvents = await _eventRepository.RemoveAllByOrganizationIdAsync(organization.Id);

        await RenewLockAsync(context);
        long removedStacks = await _stackRepository.RemoveAllByOrganizationIdAsync(organization.Id);

        await RenewLockAsync(context);
        long removedProjects = await _projectRepository.RemoveAllByOrganizationIdAsync(organization.Id);

        await RenewLockAsync(context);
        await RemoveOrganizationFilesAsync(organization, context);

        await _organizationRepository.RemoveAsync(organization);
        _logger.RemoveOrganizationComplete(organization.Name, organization.Id, removedProjects, removedStacks, removedEvents);
    }

    private static bool IsSyntheticOrganization(Organization organization)
        => organization.Name.StartsWith(SyntheticOrganizationNamePrefix, StringComparison.Ordinal);

    private static bool IsStandaloneSyntheticUser(User user)
        => user.OrganizationIds.Count == 0
            && user.EmailAddress.StartsWith(SyntheticUserEmailPrefix, StringComparison.OrdinalIgnoreCase)
            && user.EmailAddress.EndsWith(SyntheticUserEmailSuffix, StringComparison.OrdinalIgnoreCase)
            && user.FullName.StartsWith(SyntheticUserFullNamePrefix, StringComparison.Ordinal);

    private async Task RemoveSyntheticUserAsync(User user)
    {
        long removed = await _tokenRepository.RemoveAllByUserIdAsync(user.Id);
        removed += await _oauthTokenRepository.RemoveAllByUserIdAsync(user.Id);
        await _userRepository.RemoveAsync(user);
        _logger.LogInformation("Removed synthetic E2E user {UserId} and {SyntheticUserTokenCount} token(s)", user.Id, removed);
    }

    private Task RemoveOrganizationFilesAsync(Organization organization, JobContext context)
        => RemoveFilesAsync(OrganizationStoragePaths.GetProfileImagesPath(organization.Id), context.CancellationToken);

    private async Task RemoveFilesAsync(string path, CancellationToken cancellationToken)
    {
        string searchPattern = $"{path}/*";
        var files = await _fileStorage.GetFileListAsync(searchPattern, cancellationToken: cancellationToken);
        if (!files.Any())
            return;

        await _fileStorage.DeleteFilesAsync(searchPattern, cancellationToken);
    }

    private async Task RemoveProjectsAsync(Project project, JobContext context)
    {
        _logger.RemoveProjectStart(project.Name, project.Id);
        await _tokenRepository.RemoveAllByProjectIdAsync(project.OrganizationId, project.Id);
        await _webHookRepository.RemoveAllByProjectIdAsync(project.OrganizationId, project.Id);

        await RenewLockAsync(context);
        long removedEvents = await _eventRepository.RemoveAllByProjectIdAsync(project.OrganizationId, project.Id);

        if (removedEvents > 0)
            await _usageService.IncrementDeletedAsync(project.OrganizationId, project.Id, (int)removedEvents);

        await RenewLockAsync(context);
        long removedStacks = await _stackRepository.RemoveAllByProjectIdAsync(project.OrganizationId, project.Id);

        await _fileStorage.DeleteFilesAsync($"source-maps/{project.Id}/*", context.CancellationToken);
        await _projectRepository.RemoveAsync(project);
        _logger.RemoveProjectComplete(project.Name, project.Id, removedStacks, removedEvents);
    }

    private async Task RemoveStacksAsync(IReadOnlyCollection<Stack> stacks, JobContext context, bool trackDeletedUsage = false)
    {
        await RenewLockAsync(context);

        var projectGroups = stacks.GroupBy(s => (s.OrganizationId, s.ProjectId)).ToList();

        long totalRemovedEvents = 0;
        if (trackDeletedUsage)
        {
            foreach (var projectGroup in projectGroups)
            {
                string[] stackIds = projectGroup.Select(s => s.Id).ToArray();
                long removedEvents = await _eventRepository.RemoveAllByStackIdsAsync(stackIds);
                totalRemovedEvents += removedEvents;

                if (removedEvents > 0)
                    await _usageService.IncrementDeletedAsync(projectGroup.Key.OrganizationId, projectGroup.Key.ProjectId, (int)removedEvents);
            }
        }
        else
        {
            string[] allStackIds = stacks.Select(s => s.Id).ToArray();
            totalRemovedEvents = await _eventRepository.RemoveAllByStackIdsAsync(allStackIds);
        }

        await _stackRepository.RemoveAsync(stacks);

        foreach (var projectGroup in projectGroups)
            await _cacheClient.RemoveByPrefixAsync(EventStackFilterQueryBuilder.GetScopedCachePrefix(projectGroup.Key.OrganizationId, projectGroup.Key.ProjectId));

        _logger.RemoveStacksComplete(stacks.Count, totalRemovedEvents);
    }

    private async Task EnforceRetentionAsync(JobContext context)
    {
        var results = await _organizationRepository.FindAsync(q => q.Include(o => o.Id, o => o.Name, o => o.RetentionDays), o => o.SearchAfterPaging().PageLimit(100));
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
                    await EnforceStackRetentionDaysAsync(organization, retentionDays + 60, context);
                    await EnforceEventRetentionDaysAsync(organization, retentionDays, context);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error enforcing retention for Organization {OrganizationId}: {Message}", organization.Id, ex.Message);
                }

                // Sleep so we are not hammering the backend.
                await Task.Delay(TimeSpan.FromSeconds(2.5), _timeProvider);
            }

            if (context.CancellationToken.IsCancellationRequested || !await results.NextPageAsync())
                break;
        }
    }

    private async Task EnforceStackRetentionDaysAsync(Organization organization, int retentionDays, JobContext context)
    {
        await RenewLockAsync(context);

        var cutoff = _timeProvider.GetUtcNow().UtcDateTime.Date.SubtractDays(retentionDays);
        var stackResults = await _stackRepository.GetStacksForCleanupAsync(organization.Id, cutoff);
        _logger.RetentionEnforcementStackStart(cutoff, organization.Name, organization.Id, stackResults.Total);

        while (stackResults.Documents.Count > 0 && !context.CancellationToken.IsCancellationRequested)
        {
            try
            {
                // Retention-based deletions intentionally do NOT track deleted usage.
                // These events expired by plan policy, not user action — surfacing them
                // in usage charts would be misleading.
                await RemoveStacksAsync(stackResults.Documents, context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing stacks: {Message}", ex.Message);
            }

            if (context.CancellationToken.IsCancellationRequested || !await stackResults.NextPageAsync())
                break;
        }

        _logger.RetentionEnforcementStackComplete(organization.Name, organization.Id, stackResults.Documents.Count);
    }

    private async Task EnforceEventRetentionDaysAsync(Organization organization, int retentionDays, JobContext context)
    {
        await RenewLockAsync(context);

        var cutoff = _timeProvider.GetUtcNow().UtcDateTime.Date.SubtractDays(retentionDays);
        _logger.RetentionEnforcementEventStart(cutoff, organization.Name, organization.Id);

        long removedEvents = await _eventRepository.RemoveAllAsync(organization.Id, null, null, cutoff);
        _logger.RetentionEnforcementEventComplete(organization.Name, organization.Id, removedEvents);
    }

    private Task RenewLockAsync(JobContext context)
    {
        _lastRun = _timeProvider.GetUtcNow().UtcDateTime;
        return context.RenewLockAsync();
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!_lastRun.HasValue)
            return Task.FromResult(HealthCheckResult.Healthy("Job has not been run yet."));

        if (_timeProvider.GetUtcNow().UtcDateTime.Subtract(_lastRun.Value) > TimeSpan.FromMinutes(65))
            return Task.FromResult(HealthCheckResult.Unhealthy("Job has not run in the last 65 minutes."));

        return Task.FromResult(HealthCheckResult.Healthy("Job has run in the last 65 minutes."));
    }
}
