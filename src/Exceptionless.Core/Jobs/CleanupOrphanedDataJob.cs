using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;
using Foundatio.Resilience;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Jobs;

[Job(Description = "Deletes orphaned data.", IsContinuous = false)]
public class CleanupOrphanedDataJob : JobWithLockBase, IHealthCheck
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IStackRepository _stackRepository;
    private readonly IEventRepository _eventRepository;
    private readonly ICacheClient _cacheClient;
    private readonly ILockProvider _lockProvider;
    private DateTime? _lastRun;

    public CleanupOrphanedDataJob(
        IOrganizationRepository organizationRepository,
        IProjectRepository projectRepository,
        IStackRepository stackRepository,
        IEventRepository eventRepository,
        ICacheClient cacheClient,
        ILockProvider lockProvider,
        TimeProvider timeProvider,
        IResiliencePolicyProvider resiliencePolicyProvider,
        ILoggerFactory loggerFactory
    ) : base(timeProvider, resiliencePolicyProvider, loggerFactory)
    {
        _organizationRepository = organizationRepository;
        _projectRepository = projectRepository;
        _stackRepository = stackRepository;
        _eventRepository = eventRepository;
        _cacheClient = cacheClient;
        _lockProvider = lockProvider;
    }

    protected override Task<ILock?> GetLockAsync(CancellationToken cancellationToken = default)
    {
        return _lockProvider.TryAcquireAsync(nameof(CleanupOrphanedDataJob), TimeSpan.FromHours(2), cancellationToken);
    }

    protected override async Task<JobResult> RunInternalAsync(JobContext context)
    {
        _lastRun = _timeProvider.GetUtcNow().UtcDateTime;

        await DeleteOrphanedEventsByStackAsync(context);
        await DeleteOrphanedEventsByProjectAsync(context);
        await DeleteOrphanedEventsByOrganizationAsync(context);

        await FixDuplicateStacks(context);

        return JobResult.Success;
    }

    public async Task DeleteOrphanedEventsByStackAsync(JobContext context)
    {
        _logger.LogInformation("Starting orphaned events cleanup by stack");
        long totalOrphanedEvents = 0;
        long totalStackIds = 0;
        var afterKey = new CompositeKeyResult();
        bool hasMore = true;

        while (hasMore && !context.CancellationToken.IsCancellationRequested)
        {
            await RenewLockAsync(context);

            var stackIds = await _eventRepository.GetDistinctStackIdsAsync(500, afterKey);
            if (stackIds.Count == 0)
                break;

            hasMore = stackIds.Count == 500;
            totalStackIds += stackIds.Count;

            var existingStacks = await _stackRepository.GetByIdsAsync(stackIds.ToArray(), o => o.Include(s => s.Id));
            var existingStackIds = existingStacks.Select(s => s.Id).ToHashSet();
            string[] missingStackIds = stackIds.Where(id => !existingStackIds.Contains(id)).ToArray();

            if (missingStackIds.Length == 0)
                continue;

            long deletedCount = await _eventRepository.RemoveAllByStackIdsAsync(missingStackIds);
            totalOrphanedEvents += deletedCount;

            _logger.LogInformation("Deleted {DeletedCount} orphaned events from {MissingStackCount} missing stacks out of {StackIdCount} checked", deletedCount, missingStackIds.Length, stackIds.Count);
        }

        _logger.LogInformation("Completed orphaned events cleanup by stack: deleted {TotalOrphanedEvents} events, checked {TotalStackIds} stacks", totalOrphanedEvents, totalStackIds);
    }

    public async Task DeleteOrphanedEventsByProjectAsync(JobContext context)
    {
        _logger.LogInformation("Starting orphaned events cleanup by project");
        long totalOrphanedEvents = 0;
        long totalProjectIds = 0;
        var afterKey = new CompositeKeyResult();
        bool hasMore = true;

        while (hasMore && !context.CancellationToken.IsCancellationRequested)
        {
            await RenewLockAsync(context);

            var projectIds = await _eventRepository.GetDistinctProjectIdsAsync(500, afterKey);
            if (projectIds.Count == 0)
                break;

            hasMore = projectIds.Count == 500;
            totalProjectIds += projectIds.Count;

            var existingProjects = await _projectRepository.GetByIdsAsync(projectIds.ToArray(), o => o.Include(p => p.Id));
            var existingProjectIds = existingProjects.Select(p => p.Id).ToHashSet();
            string[] missingProjectIds = projectIds.Where(id => !existingProjectIds.Contains(id)).ToArray();

            if (missingProjectIds.Length == 0)
                continue;

            long deletedCount = await _eventRepository.RemoveAllByProjectIdsAsync(missingProjectIds);
            totalOrphanedEvents += deletedCount;

            _logger.LogInformation("Deleted {DeletedCount} orphaned events from {MissingProjectCount} missing projects out of {ProjectIdCount} checked", deletedCount, missingProjectIds.Length, projectIds.Count);
        }

        _logger.LogInformation("Completed orphaned events cleanup by project: deleted {TotalOrphanedEvents} events, checked {TotalProjectIds} projects", totalOrphanedEvents, totalProjectIds);
    }

    public async Task DeleteOrphanedEventsByOrganizationAsync(JobContext context)
    {
        _logger.LogInformation("Starting orphaned events cleanup by organization");
        long totalOrphanedEvents = 0;
        long totalOrganizationIds = 0;
        var afterKey = new CompositeKeyResult();
        bool hasMore = true;

        while (hasMore && !context.CancellationToken.IsCancellationRequested)
        {
            await RenewLockAsync(context);

            var organizationIds = await _eventRepository.GetDistinctOrganizationIdsAsync(500, afterKey);
            if (organizationIds.Count == 0)
                break;

            hasMore = organizationIds.Count == 500;
            totalOrganizationIds += organizationIds.Count;

            var existingOrgs = await _organizationRepository.GetByIdsAsync(organizationIds.ToArray(), o => o.Include(org => org.Id));
            var existingOrgIds = existingOrgs.Select(o => o.Id).ToHashSet();
            string[] missingOrganizationIds = organizationIds.Where(id => !existingOrgIds.Contains(id)).ToArray();

            if (missingOrganizationIds.Length == 0)
                continue;

            long deletedCount = await _eventRepository.RemoveAllByOrganizationIdsAsync(missingOrganizationIds);
            totalOrphanedEvents += deletedCount;

            _logger.LogInformation("Deleted {DeletedCount} orphaned events from {MissingOrgCount} missing organizations out of {OrgIdCount} checked", deletedCount, missingOrganizationIds.Length, organizationIds.Count);
        }

        _logger.LogInformation("Completed orphaned events cleanup by organization: deleted {TotalOrphanedEvents} events, checked {TotalOrgIds} organizations", totalOrphanedEvents, totalOrganizationIds);
    }

    public async Task FixDuplicateStacks(JobContext context)
    {
        _logger.LogInformation("Getting duplicate stacks");

        int total = 0;
        int processed = 0;
        int error = 0;
        long totalUpdatedEventCount = 0;
        var lastStatus = _timeProvider.GetUtcNow().UtcDateTime;
        int batch = 0;

        // Loop until no more duplicate signatures exist. Each iteration forces an index refresh
        // (via ImmediateConsistency on SaveAsync) so soft-deleted stacks are excluded from
        // subsequent aggregation calls, preventing the same signatures from being re-processed.
        while (!context.CancellationToken.IsCancellationRequested)
        {
            var duplicateSignatures = await _stackRepository.GetDuplicateSignaturesAsync();
            if (duplicateSignatures.Count == 0)
                break;

            batch++;
            total += duplicateSignatures.Count;
            _logger.LogInformation("Found {Total} duplicate stacks in batch #{Batch}", duplicateSignatures.Count, batch);
            await RenewLockAsync(context);

            int batchProcessed = 0;
            foreach (var duplicateSignature in duplicateSignatures)
            {
                if (context.CancellationToken.IsCancellationRequested)
                    break;

                string? projectId = null;
                string? signature = null;
                try
                {
                    string[] parts = duplicateSignature.Split(':');
                    if (parts.Length != 2)
                    {
                        _logger.LogError("Error parsing duplicate signature {DuplicateSignature}", duplicateSignature);
                        continue;
                    }
                    projectId = parts[0];
                    signature = parts[1];

                    var stacks = await _stackRepository.FindAsync(q => q.Project(projectId).FilterExpression($"signature_hash:{signature}"));
                    if (stacks.Documents.Count < 2)
                    {
                        _logger.LogError("Did not find multiple stacks with signature {SignatureHash} and project {ProjectId}", signature, projectId);
                        continue;
                    }

                    var eventCounts = await _eventRepository.CountAsync(q => q.Stack(stacks.Documents.Select(s => s.Id)).AggregationsExpression("terms:stack_id"));
                    var eventCountBuckets = eventCounts.Aggregations.Terms("terms_stack_id")?.Buckets ?? new List<Foundatio.Repositories.Models.KeyedBucket<string>>();

                    // we only need to update events if more than one stack has events associated to it
                    bool shouldUpdateEvents = eventCountBuckets.Count > 1;

                    // default to using the oldest stack
                    var targetStack = stacks.Documents.OrderBy(s => s.CreatedUtc).First();
                    var duplicateStacks = stacks.Documents.OrderBy(s => s.CreatedUtc).Skip(1).ToList();

                    // use the stack that has the most events on it so we can reduce the number of updates
                    if (eventCountBuckets.Count > 0)
                    {
                        string targetStackId = eventCountBuckets.OrderByDescending(b => b.Total).First().Key;
                        targetStack = stacks.Documents.Single(d => d.Id == targetStackId);
                        duplicateStacks = stacks.Documents.Where(d => d.Id != targetStackId).ToList();
                    }

                    targetStack.CreatedUtc = stacks.Documents.Min(d => d.CreatedUtc);
                    targetStack.Status = stacks.Documents.FirstOrDefault(d => d.Status != StackStatus.Open)?.Status ?? StackStatus.Open;
                    targetStack.LastOccurrence = stacks.Documents.Max(d => d.LastOccurrence);
                    targetStack.SnoozeUntilUtc = stacks.Documents.Max(d => d.SnoozeUntilUtc);
                    targetStack.DateFixed = stacks.Documents.Max(d => d.DateFixed);
                    targetStack.TotalOccurrences += duplicateStacks.Sum(d => d.TotalOccurrences);
                    targetStack.Tags.UnionWith(duplicateStacks.SelectMany(d => d.Tags));
                    targetStack.References = stacks.Documents.SelectMany(d => d.References).Distinct().ToList();
                    targetStack.OccurrencesAreCritical = stacks.Documents.Any(d => d.OccurrencesAreCritical);

                    duplicateStacks.ForEach(s => s.IsDeleted = true);

                    if (shouldUpdateEvents)
                    {
                        // Reassign events before soft-deleting duplicates: if event reassignment
                        // fails, the duplicate stacks remain visible and no data is lost.
                        long affectedRecords = await _eventRepository.ReassignStackAsync(
                            duplicateStacks.Select(s => s.Id), targetStack.Id);

                        _logger.LogInformation("Migrated stack events: Target={TargetId} Events={UpdatedEvents} Dupes={DuplicateIds}", targetStack.Id, affectedRecords, duplicateStacks.Select(s => s.Id));
                        totalUpdatedEventCount += affectedRecords;
                    }

                    // Soft-delete duplicates and save after events are safely migrated.
                    // No per-item ImmediateConsistency needed: GetDuplicateSignaturesAsync
                    // forces a refresh before each batch aggregation call.
                    await _stackRepository.SaveAsync([..duplicateStacks, targetStack]);
                    processed++;
                    batchProcessed++;

                    long eventsToMove = eventCountBuckets.Where(b => b.Key != targetStack.Id).Sum(b => b.Total) ?? 0;
                    _logger.LogInformation("De-duped stack: Target={TargetId} Events={EventCount} Dupes={DuplicateIds} HasEvents={HasEvents}", targetStack.Id, eventsToMove, duplicateStacks.Select(s => s.Id), shouldUpdateEvents);

                    if (_timeProvider.GetUtcNow().UtcDateTime.Subtract(lastStatus) > TimeSpan.FromSeconds(5))
                    {
                        lastStatus = _timeProvider.GetUtcNow().UtcDateTime;
                        await RenewLockAsync(context);
                        _logger.LogInformation("Total={Processed}/{Total} Errors={ErrorCount}", processed, total, error);
                        await _cacheClient.RemoveByPrefixAsync(nameof(Stack));
                    }
                }
                catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    error++;
                    _logger.LogError(ex, "Error fixing duplicate stack {ProjectId} {SignatureHash}: {Message}", projectId, signature, ex.Message);
                }
            }

            _logger.LogInformation("Batch #{Batch} complete: Processed={BatchProcessed} Total={Processed}/{Total} Errors={ErrorCount}", batch, batchProcessed, processed, total, error);
            await _cacheClient.RemoveByPrefixAsync(nameof(Stack));

            // If nothing was processed this batch (all errors), stop to avoid an infinite loop
            // where the same failing signatures are retried indefinitely.
            if (batchProcessed == 0)
                break;
        }

        _logger.LogInformation("Done de-duping stacks: Total={Processed}/{Total} Errors={ErrorCount}", processed, total, error);
        await _cacheClient.RemoveByPrefixAsync(nameof(Stack));
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
