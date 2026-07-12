using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Repositories;
using Foundatio.Repositories.Exceptions;
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
        string? nextValue = null;
        bool hasMore = true;

        while (hasMore && !context.CancellationToken.IsCancellationRequested)
        {
            await RenewLockAsync(context);

            var page = await _eventRepository.GetDistinctStackIdsAsync(500, nextValue, context.CancellationToken);
            var stackIds = page.Values;
            if (stackIds.Count == 0)
                break;

            nextValue = page.NextValue;
            hasMore = !String.IsNullOrEmpty(nextValue);
            totalStackIds += stackIds.Count;

            var existingStacks = await _stackRepository.GetByIdsAsync(stackIds.ToArray(), o => o.Include(s => s.Id, s => s.IsDeleted));
            var existingStackIds = existingStacks.Select(s => s.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            string[] missingStackIds = stackIds.Where(id => !existingStackIds.Contains(id)).ToArray();

            if (missingStackIds.Length == 0)
                continue;

            // Redirect tombstones are intentionally retained so events from an in-flight ingestion
            // context can never be mistaken for orphaned data. Move those late events to the
            // canonical stack and refresh its metadata before deleting only truly missing stacks.
            var redirectedStacks = await _stackRepository.GetByIdsAsync(
                missingStackIds,
                o => o.SoftDeleteMode(SoftDeleteQueryMode.All));
            var redirectedStackIds = redirectedStacks
                .Where(s => !String.IsNullOrEmpty(s.RedirectToStackId))
                .Select(s => s.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var redirectedStackDocuments = redirectedStacks
                .Where(s => redirectedStackIds.Contains(s.Id))
                .ToList();

            if (redirectedStackDocuments.Count > 0)
                await ReconcileRedirectedStacksAsync(redirectedStackDocuments, context);

            string[] orphanedStackIds = missingStackIds.Where(id => !redirectedStackIds.Contains(id)).ToArray();
            if (orphanedStackIds.Length == 0)
                continue;

            long deletedCount = await _eventRepository.RemoveAllByStackIdsAsync(orphanedStackIds, o => o.Notifications(false));
            totalOrphanedEvents += deletedCount;

            _logger.LogInformation("Deleted {DeletedCount} orphaned events from {MissingStackCount} missing stacks out of {StackIdCount} checked", deletedCount, orphanedStackIds.Length, stackIds.Count);
        }

        await ReconcileDirtyRedirectedStacksAsync(context);

        _logger.LogInformation("Completed orphaned events cleanup by stack: deleted {TotalOrphanedEvents} events, checked {TotalStackIds} stacks", totalOrphanedEvents, totalStackIds);
    }

    public async Task DeleteOrphanedEventsByProjectAsync(JobContext context)
    {
        _logger.LogInformation("Starting orphaned events cleanup by project");
        long totalOrphanedEvents = 0;
        long totalProjectIds = 0;
        string? nextValue = null;
        bool hasMore = true;

        while (hasMore && !context.CancellationToken.IsCancellationRequested)
        {
            await RenewLockAsync(context);

            var page = await _eventRepository.GetDistinctProjectIdsAsync(500, nextValue, context.CancellationToken);
            var projectIds = page.Values;
            if (projectIds.Count == 0)
                break;

            nextValue = page.NextValue;
            hasMore = !String.IsNullOrEmpty(nextValue);
            totalProjectIds += projectIds.Count;

            var existingProjects = await _projectRepository.GetByIdsAsync(projectIds.ToArray(), o => o.Include(p => p.Id, p => p.IsDeleted));
            var existingProjectIds = existingProjects.Select(p => p.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            string[] missingProjectIds = projectIds.Where(id => !existingProjectIds.Contains(id)).ToArray();

            if (missingProjectIds.Length == 0)
                continue;

            long deletedCount = await _eventRepository.RemoveAllByProjectIdsAsync(missingProjectIds, o => o.Notifications(false));
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
        string? nextValue = null;
        bool hasMore = true;

        while (hasMore && !context.CancellationToken.IsCancellationRequested)
        {
            await RenewLockAsync(context);

            var page = await _eventRepository.GetDistinctOrganizationIdsAsync(500, nextValue, context.CancellationToken);
            var organizationIds = page.Values;
            if (organizationIds.Count == 0)
                break;

            nextValue = page.NextValue;
            hasMore = !String.IsNullOrEmpty(nextValue);
            totalOrganizationIds += organizationIds.Count;

            var existingOrganizations = await _organizationRepository.GetByIdsAsync(organizationIds.ToArray(), o => o.Include(organization => organization.Id, organization => organization.IsDeleted));
            var existingOrganizationIds = existingOrganizations.Select(organization => organization.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            string[] missingOrganizationIds = organizationIds.Where(id => !existingOrganizationIds.Contains(id)).ToArray();

            if (missingOrganizationIds.Length == 0)
                continue;

            long deletedCount = await _eventRepository.RemoveAllByOrganizationIdsAsync(missingOrganizationIds, o => o.Notifications(false));
            totalOrphanedEvents += deletedCount;

            _logger.LogInformation("Deleted {DeletedCount} orphaned events from {MissingOrganizationCount} missing organizations out of {OrganizationIdCount} checked", deletedCount, missingOrganizationIds.Length, organizationIds.Count);
        }

        _logger.LogInformation("Completed orphaned events cleanup by organization: deleted {TotalOrphanedEvents} events, checked {TotalOrganizationIds} organizations", totalOrphanedEvents, totalOrganizationIds);
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
        // (via ImmediateConsistency on GetDuplicateSignaturesAsync) so soft-deleted stacks are
        // excluded from subsequent aggregation calls, preventing re-processing.
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

                    var stacks = await _stackRepository.FindAsync(q => q.Project(projectId).FieldEquals(s => s.SignatureHash, signature));
                    if (stacks.Documents.Count < 2)
                    {
                        _logger.LogError("Did not find multiple stacks with signature {SignatureHash} and project {ProjectId}", signature, projectId);
                        continue;
                    }

                    var targetCandidates = stacks.Documents.Where(s => String.IsNullOrEmpty(s.RedirectToStackId)).ToList();
                    if (targetCandidates.Count == 0)
                    {
                        _logger.LogError("Did not find a canonical stack for signature {SignatureHash} and project {ProjectId}", signature, projectId);
                        continue;
                    }

                    var eventCounts = await _eventRepository.CountAsync(q => q.Stack(stacks.Documents.Select(s => s.Id)).AggregationsExpression("terms:stack_id"));
                    var eventCountBuckets = eventCounts.Aggregations.Terms("terms_stack_id")?.Buckets ?? new List<KeyedBucket<string>>();

                    // We only need to update events if more than one stack has events associated to it.
                    bool shouldUpdateEvents = eventCountBuckets.Count > 1;

                    // Default to using the oldest stack.
                    var targetStack = targetCandidates.OrderBy(s => s.CreatedUtc).First();
                    var duplicateStacks = stacks.Documents.Where(s => s.Id != targetStack.Id).OrderBy(s => s.CreatedUtc).ToList();

                    // Use the stack that has the most events on it so we can reduce the number of updates.
                    var targetCandidateIds = targetCandidates.Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
                    var targetBuckets = eventCountBuckets.Where(b => targetCandidateIds.Contains(b.Key)).ToList();
                    if (targetBuckets.Count > 0)
                    {
                        string targetStackId = targetBuckets.OrderByDescending(b => b.Total).First().Key;
                        targetStack = targetCandidates.Single(d => d.Id == targetStackId);
                        duplicateStacks = stacks.Documents.Where(d => d.Id != targetStackId).OrderBy(d => d.CreatedUtc).ToList();
                    }

                    // Publish durable redirects before moving events. New ingestion resolves these
                    // immediately, and any already in-flight writes are preserved by the orphan pass.
                    foreach (var duplicateStack in duplicateStacks)
                        await _stackRepository.SetDuplicateStackRedirectAsync(duplicateStack, targetStack.Id);

                    // Always run strict verification, even when open-index counts show no source
                    // events. Closed or unavailable daily indices must block stack deletion.
                    long affectedRecords = await AwaitWithLockRenewalAsync(
                        _eventRepository.ReassignStackAsync(
                            duplicateStacks.Select(s => s.Id), targetStack.Id, context.CancellationToken),
                        context);

                    _logger.LogInformation("Migrated stack events: Target={TargetId} Events={UpdatedEvents} Dupes={DuplicateIds}", targetStack.Id, affectedRecords, duplicateStacks.Select(s => s.Id));
                    totalUpdatedEventCount += affectedRecords;

                    context.CancellationToken.ThrowIfCancellationRequested();

                    // Apply each source's metadata to the target using a durable occurrence ledger,
                    // then hide the redirected source. This ordering is retry-safe when any write fails.
                    foreach (var duplicateStack in duplicateStacks)
                    {
                        await _stackRepository.MergeDuplicateStackAsync(targetStack.Id, duplicateStack);
                        await _stackRepository.SetDuplicateStackRedirectAsync(duplicateStack, targetStack.Id, isDeleted: true);
                    }

                    processed++;
                    batchProcessed++;

                    long eventsToMove = eventCountBuckets.Where(b => b.Key != targetStack.Id).Sum(b => b.Total) ?? 0;
                    _logger.LogInformation("De-duped stack: Target={TargetId} Events={EventCount} Dupes={DuplicateIds} HasEvents={HasEvents}", targetStack.Id, eventsToMove, duplicateStacks.Select(s => s.Id), shouldUpdateEvents);

                    if (_timeProvider.GetUtcNow().UtcDateTime.Subtract(lastStatus) > TimeSpan.FromSeconds(5))
                    {
                        lastStatus = _timeProvider.GetUtcNow().UtcDateTime;
                        await RenewLockAsync(context);
                        _logger.LogInformation("Total={Processed}/{Total} Errors={ErrorCount}", processed, total, error);
                    }
                }
                catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // Intentionally broad: log and continue processing other groups rather than
                    // aborting the entire job for a single corrupt or transiently failing signature.
                    error++;
                    _logger.LogError(ex, "Error fixing duplicate stack {ProjectId} {SignatureHash}: {Message}", projectId, signature, ex.Message);
                }
            }

            _logger.LogInformation("Batch #{Batch} complete: Processed={BatchProcessed} Total={Processed}/{Total} Errors={ErrorCount} UpdatedEvents={UpdatedEventCount}", batch, batchProcessed, processed, total, error, totalUpdatedEventCount);
            await _cacheClient.RemoveByPrefixAsync(nameof(Stack));

            // If nothing was processed this batch (all errors), stop to avoid an infinite loop
            // where the same failing signatures are retried indefinitely.
            if (batchProcessed == 0)
                break;
        }

        _logger.LogInformation("Done de-duping stacks: Total={Processed}/{Total} Errors={ErrorCount} UpdatedEvents={UpdatedEventCount}", processed, total, error, totalUpdatedEventCount);
    }

    private async Task ReconcileDirtyRedirectedStacksAsync(JobContext context)
    {
        var redirectedStacks = await _stackRepository.GetRedirectedStacksNeedingReconciliationAsync();
        while (redirectedStacks.Documents.Count > 0 && !context.CancellationToken.IsCancellationRequested)
        {
            await ReconcileRedirectedStacksAsync(redirectedStacks.Documents, context);

            if (!await redirectedStacks.NextPageAsync())
                break;
        }
    }

    private async Task ReconcileRedirectedStacksAsync(IReadOnlyCollection<Stack> redirectedStacks, JobContext context)
    {
        var stacksByTarget = new Dictionary<string, (Stack Target, List<Stack> Sources)>(StringComparer.Ordinal);
        foreach (var sourceStack in redirectedStacks)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            try
            {
                var targetStack = await _stackRepository.GetCanonicalStackAsync(sourceStack.RedirectToStackId!);
                if (targetStack is null)
                {
                    _logger.LogWarning(
                        "Preserving redirected stack {SourceStackId} because target stack {TargetStackId} is unavailable",
                        sourceStack.Id,
                        sourceStack.RedirectToStackId);
                    continue;
                }

                if (!stacksByTarget.TryGetValue(targetStack.Id, out var group))
                {
                    group = (targetStack, []);
                    stacksByTarget[targetStack.Id] = group;
                }

                group.Sources.Add(sourceStack);
            }
            catch (DocumentException ex)
            {
                _logger.LogError(
                    ex,
                    "Unable to resolve redirected stack {SourceStackId} to target {TargetStackId}",
                    sourceStack.Id,
                    sourceStack.RedirectToStackId);
            }
        }

        foreach (var (_, group) in stacksByTarget)
        {
            // Merge metadata first. If event reassignment fails, the contribution ledger makes
            // this safe to retry. Counter patches mark a tombstone dirty, while event-bearing
            // tombstones are discovered directly by the orphan scan.
            foreach (var sourceStack in group.Sources)
                await _stackRepository.MergeDuplicateStackAsync(group.Target.Id, sourceStack);

            long reassigned = await AwaitWithLockRenewalAsync(
                _eventRepository.ReassignStackAsync(
                    group.Sources.Select(s => s.Id), group.Target.Id, context.CancellationToken),
                context);

            foreach (var sourceStack in group.Sources)
            {
                if (!sourceStack.IsDeleted)
                {
                    await _stackRepository.SetDuplicateStackRedirectAsync(sourceStack, group.Target.Id, isDeleted: true);
                    sourceStack.IsDeleted = true;
                    sourceStack.RedirectToStackId = group.Target.Id;
                }

                await _stackRepository.MarkDuplicateStackReconciledAsync(sourceStack);
            }

            if (reassigned > 0)
            {
                _logger.LogInformation(
                    "Reassigned {EventCount} late event(s) from {SourceStackCount} duplicate stack(s) to {TargetStackId}",
                    reassigned,
                    group.Sources.Count,
                    group.Target.Id);
            }
        }
    }

    private Task RenewLockAsync(JobContext context)
    {
        _lastRun = _timeProvider.GetUtcNow().UtcDateTime;
        return context.RenewLockAsync();
    }

    private async Task<T> AwaitWithLockRenewalAsync<T>(Task<T> operation, JobContext context)
    {
        while (!operation.IsCompleted)
        {
            var completed = await Task.WhenAny(operation, Task.Delay(TimeSpan.FromSeconds(30), _timeProvider));
            if (completed == operation)
                break;

            await RenewLockAsync(context);
        }

        return await operation;
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
