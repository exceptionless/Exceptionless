using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Aggregations;
using Elastic.Clients.Elasticsearch.Core.ReindexRethrottle;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Configuration;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Repositories;
using Foundatio.Repositories.Elasticsearch.Extensions;
using Foundatio.Repositories.Extensions;
using Foundatio.Repositories.Models;
using Foundatio.Resilience;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Exceptionless.Core.Jobs;

[Job(Description = "Deletes orphaned data.", IsContinuous = false)]
public class CleanupOrphanedDataJob : JobWithLockBase, IHealthCheck
{
    private readonly ExceptionlessElasticConfiguration _config;
    private readonly ElasticsearchClient _elasticClient;
    private readonly IStackRepository _stackRepository;
    private readonly IEventRepository _eventRepository;
    private readonly ICacheClient _cacheClient;
    private readonly ILockProvider _lockProvider;
    private DateTime? _lastRun;

    public CleanupOrphanedDataJob(ExceptionlessElasticConfiguration config, IStackRepository stackRepository,
        IEventRepository eventRepository, ICacheClient cacheClient, ILockProvider lockProvider,
        TimeProvider timeProvider,
        IResiliencePolicyProvider resiliencePolicyProvider,
        ILoggerFactory loggerFactory
    ) : base(timeProvider, resiliencePolicyProvider, loggerFactory)
    {
        _config = config;
        _elasticClient = config.Client;
        _stackRepository = stackRepository;
        _eventRepository = eventRepository;
        _cacheClient = cacheClient;
        _lockProvider = lockProvider;
    }

    protected override Task<ILock?> GetLockAsync(CancellationToken cancellationToken = default)
    {
        return _lockProvider.AcquireAsync(nameof(CleanupOrphanedDataJob), TimeSpan.FromHours(2), cancellationToken);
    }

    protected override async Task<JobResult> RunInternalAsync(JobContext context)
    {
        await DeleteOrphanedEventsByStackAsync(context);
        await DeleteOrphanedEventsByProjectAsync(context);
        await DeleteOrphanedEventsByOrganizationAsync(context);

        await FixDuplicateStacks(context);

        return JobResult.Success;
    }

    public async Task DeleteOrphanedEventsByStackAsync(JobContext context)
    {
        // get approximate number of unique stack ids
        var stackCardinality = await _elasticClient.SearchAsync<PersistentEvent>(s => s
            .AddAggregation("cardinality_stack_id", a => a.Cardinality(c => c.Field(f => f.StackId).PrecisionThreshold(40000))));

        double? uniqueStackIdCount = stackCardinality.Aggregations?.GetCardinality("cardinality_stack_id")?.Value;
        if (!uniqueStackIdCount.HasValue || uniqueStackIdCount.Value <= 0)
            return;

        // break into batches of 500
        const int batchSize = 500;
        int buckets = (int)uniqueStackIdCount.Value / batchSize;
        buckets = Math.Max(1, buckets);
        int totalOrphanedEventCount = 0;
        int totalStackIds = 0;

        for (int batchNumber = 0; batchNumber < buckets; batchNumber++)
        {
            await RenewLockAsync(context);

            var stackIdTerms = await _elasticClient.SearchAsync<PersistentEvent>(s => s
                .AddAggregation("terms_stack_id", a => a.Terms(c => c.Field(f => f.StackId).Include(new TermsInclude(batchNumber, buckets)).Size(batchSize * 2))));

            string[] stackIds = stackIdTerms.Aggregations?.GetStringTerms("terms_stack_id")?.Buckets.Select(b => b.Key.ToString()!).ToArray() ?? [];
            if (stackIds.Length == 0)
                continue;

            totalStackIds += stackIds.Length;

            var stacks = await _elasticClient.MultiGetAsync<Stack>(r => r.Source(false).Ids(stackIds));
            string[] missingStackIds = stacks.Docs
                .Select(d => d.Value1).Where(r => r is not null && !r.Found).Select(r => r!.Id).ToArray();


            if (missingStackIds.Length == 0)
            {
                _logger.LogInformation("{BatchNumber}/{BatchCount}: Did not find any missing stacks out of {StackIdCount}", batchNumber, buckets, stackIds.Length);
                continue;
            }

            totalOrphanedEventCount += missingStackIds.Length;
            _logger.LogInformation("{BatchNumber}/{BatchCount}: Found {OrphanedEventCount} orphaned events from missing stacks {MissingStackIds} out of {StackIdCount}", batchNumber, buckets, missingStackIds.Length, missingStackIds, stackIds.Length);
            await _elasticClient.DeleteByQueryAsync<PersistentEvent>(r => r.Query(q => q.Terms(t => t.Field(f => f.StackId).Terms(new TermsQueryField(missingStackIds.Select(id => (FieldValue)id).ToList())))));
        }

        _logger.LogInformation("Found {OrphanedEventCount} orphaned events from missing stacks out of {StackIdCount}", totalOrphanedEventCount, totalStackIds);
    }

    public async Task DeleteOrphanedEventsByProjectAsync(JobContext context)
    {
        // get approximate number of unique project ids
        var projectCardinality = await _elasticClient.SearchAsync<PersistentEvent>(s => s
            .AddAggregation("cardinality_project_id", a => a.Cardinality(c => c.Field(f => f.ProjectId).PrecisionThreshold(40000))));

        double? uniqueProjectIdCount = projectCardinality.Aggregations?.GetCardinality("cardinality_project_id")?.Value;
        if (!uniqueProjectIdCount.HasValue || uniqueProjectIdCount.Value <= 0)
            return;

        // break into batches of 500
        const int batchSize = 500;
        int buckets = (int)uniqueProjectIdCount.Value / batchSize;
        buckets = Math.Max(1, buckets);
        int totalOrphanedEventCount = 0;
        int totalProjectIds = 0;

        for (int batchNumber = 0; batchNumber < buckets; batchNumber++)
        {
            await RenewLockAsync(context);

            var projectIdTerms = await _elasticClient.SearchAsync<PersistentEvent>(s => s
                .AddAggregation("terms_project_id", a => a.Terms(c => c.Field(f => f.ProjectId).Include(new TermsInclude(batchNumber, buckets)).Size(batchSize * 2))));

            string[] projectIds = projectIdTerms.Aggregations?.GetStringTerms("terms_project_id")?.Buckets.Select(b => b.Key.ToString()!).ToArray() ?? [];
            if (projectIds.Length == 0)
                continue;

            totalProjectIds += projectIds.Length;

            var projects = await _elasticClient.MultiGetAsync<Project>(r => r.Source(false).Ids(projectIds));
            string[] missingProjectIds = projects.Docs
                .Select(d => d.Value1).Where(r => r is not null && !r.Found).Select(r => r!.Id).ToArray();

            if (missingProjectIds.Length == 0)
            {
                _logger.LogInformation("{BatchNumber}/{BatchCount}: Did not find any missing projects out of {ProjectIdCount}", batchNumber, buckets, projectIds.Length);
                continue;
            }

            _logger.LogInformation("{BatchNumber}/{BatchCount}: Found {OrphanedEventCount} orphaned events from missing projects {MissingProjectIds} out of {ProjectIdCount}", batchNumber, buckets, missingProjectIds.Length, missingProjectIds, projectIds.Length);
            await _elasticClient.DeleteByQueryAsync<PersistentEvent>(r => r.Query(q => q.Terms(t => t.Field(f => f.ProjectId).Terms(new TermsQueryField(missingProjectIds.Select(id => (FieldValue)id).ToList())))));
        }

        _logger.LogInformation("Found {OrphanedEventCount} orphaned events from missing projects out of {ProjectIdCount}", totalOrphanedEventCount, totalProjectIds);
    }

    public async Task DeleteOrphanedEventsByOrganizationAsync(JobContext context)
    {
        // get approximate number of unique organization ids
        var organizationCardinality = await _elasticClient.SearchAsync<PersistentEvent>(s => s
            .AddAggregation("cardinality_organization_id", a => a.Cardinality(c => c.Field(f => f.OrganizationId).PrecisionThreshold(40000))));

        double? uniqueOrganizationIdCount = organizationCardinality.Aggregations?.GetCardinality("cardinality_organization_id")?.Value;
        if (!uniqueOrganizationIdCount.HasValue || uniqueOrganizationIdCount.Value <= 0)
            return;

        // break into batches of 500
        const int batchSize = 500;
        int buckets = (int)uniqueOrganizationIdCount.Value / batchSize;
        buckets = Math.Max(1, buckets);
        int totalOrphanedEventCount = 0;
        int totalOrganizationIds = 0;

        for (int batchNumber = 0; batchNumber < buckets; batchNumber++)
        {
            await RenewLockAsync(context);

            var organizationIdTerms = await _elasticClient.SearchAsync<PersistentEvent>(s => s
                .AddAggregation("terms_organization_id", a => a.Terms(c => c.Field(f => f.OrganizationId).Include(new TermsInclude(batchNumber, buckets)).Size(batchSize * 2))));

            string[] organizationIds = organizationIdTerms.Aggregations?.GetStringTerms("terms_organization_id")?.Buckets.Select(b => b.Key.ToString()!).ToArray() ?? [];
            if (organizationIds.Length == 0)
                continue;

            totalOrganizationIds += organizationIds.Length;

            var organizations = await _elasticClient.MultiGetAsync<Organization>(r => r.Source(false).Ids(organizationIds));
            string[] missingOrganizationIds = organizations.Docs
                .Select(d => d.Value1).Where(r => r is not null && !r.Found).Select(r => r!.Id).ToArray();

            if (missingOrganizationIds.Length == 0)
            {
                _logger.LogInformation("{BatchNumber}/{BatchCount}: Did not find any missing organizations out of {OrganizationIdCount}", batchNumber, buckets, organizationIds.Length);
                continue;
            }

            _logger.LogInformation("{BatchNumber}/{BatchCount}: Found {OrphanedEventCount} orphaned events from missing organizations {MissingOrganizationIds} out of {OrganizationIdCount}", batchNumber, buckets, missingOrganizationIds.Length, missingOrganizationIds, organizationIds.Length);
            await _elasticClient.DeleteByQueryAsync<PersistentEvent>(r => r.Query(q => q.Terms(t => t.Field(f => f.OrganizationId).Terms(new TermsQueryField(missingOrganizationIds.Select(id => (FieldValue)id).ToList())))));
        }

        _logger.LogInformation("Found {OrphanedEventCount} orphaned events from missing organizations out of {OrganizationIdCount}", totalOrphanedEventCount, totalOrganizationIds);
    }

    public async Task FixDuplicateStacks(JobContext context)
    {
        _logger.LogInformation("Getting duplicate stacks");

        var duplicateStackAgg = await _elasticClient.SearchAsync<Stack>(q => q
            .Query(q => q.QueryString(qs => qs.Query("is_deleted:false")))
            .Size(0)
            .AddAggregation("stacks", a => a.Terms(t => t.Field(f => f.DuplicateSignature).MinDocCount(2).Size(10000))));
        _logger.LogRequest(duplicateStackAgg, LogLevel.Trace);

        var buckets = duplicateStackAgg.Aggregations?.GetStringTerms("stacks")?.Buckets.ToList() ?? [];
        int total = buckets.Count;
        int processed = 0;
        int error = 0;
        long totalUpdatedEventCount = 0;
        var lastStatus = _timeProvider.GetUtcNow().UtcDateTime;
        int batch = 1;

        while (buckets.Count > 0)
        {
            _logger.LogInformation($"Found {buckets.Count} duplicate stacks in batch #{batch}.");
            await RenewLockAsync(context);

            foreach (var duplicateSignature in buckets)
            {
                string? projectId = null;
                string? signature = null;
                try
                {
                    string[] parts = duplicateSignature.Key.ToString().Split(':');
                    if (parts.Length != 2)
                    {
                        _logger.LogError("Error parsing duplicate signature {DuplicateSignature}", duplicateSignature.Key.ToString());
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
                    targetStack.DateFixed = stacks.Documents.Max(d => d.DateFixed); ;
                    targetStack.TotalOccurrences += duplicateStacks.Sum(d => d.TotalOccurrences);
                    targetStack.Tags.AddRange(duplicateStacks.SelectMany(d => d.Tags));
                    targetStack.References = stacks.Documents.SelectMany(d => d.References).Distinct().ToList();
                    targetStack.OccurrencesAreCritical = stacks.Documents.Any(d => d.OccurrencesAreCritical);

                    duplicateStacks.ForEach(s => s.IsDeleted = true);
                    await _stackRepository.SaveAsync(duplicateStacks);
                    await _stackRepository.SaveAsync(targetStack);
                    processed++;

                    long eventsToMove = eventCountBuckets.Where(b => b.Key != targetStack.Id).Sum(b => b.Total) ?? 0;
                    _logger.LogInformation("De-duped stack: Target={TargetId} Events={EventCount} Dupes={DuplicateIds} HasEvents={HasEvents}", targetStack.Id, eventsToMove, duplicateStacks.Select(s => s.Id), shouldUpdateEvents);

                    if (shouldUpdateEvents)
                    {
                        var response = await _elasticClient.UpdateByQueryAsync<PersistentEvent>(u => u
                            .Query(q => q.Bool(b => b.Must(m => m
                                .Terms(t => t.Field(f => f.StackId).Terms(new TermsQueryField(duplicateStacks.Select(s => (FieldValue)s.Id).ToList())))
                            )))
                            .Script(s => s.Source($"ctx._source.stack_id = '{targetStack.Id}'").Lang(ScriptLanguage.Painless))
                            .Conflicts(Conflicts.Proceed)
                            .WaitForCompletion(false));
                        _logger.LogRequest(response, LogLevel.Trace);

                        var taskStartedTime = _timeProvider.GetUtcNow().UtcDateTime;
                        var taskId = response.Task;
                        int attempts = 0;
                        long affectedRecords = 0;
                        do
                        {
                            attempts++;
                            var taskStatus = await _elasticClient.Tasks.GetAsync(taskId!.FullyQualifiedId);
                            var status = taskStatus.Task.Status as ReindexStatus;
                            if (taskStatus.Completed)
                            {
                                // TODO: need to check to see if the task failed or completed successfully. Throw if it failed.
                                if (_timeProvider.GetUtcNow().UtcDateTime.Subtract(taskStartedTime) > TimeSpan.FromSeconds(30))
                                    _logger.LogInformation("Script operation task ({TaskId}) completed: Created: {Created} Updated: {Updated} Deleted: {Deleted} Conflicts: {Conflicts} Total: {Total}", taskId, status?.Created, status?.Updated, status?.Deleted, status?.VersionConflicts, status?.Total);

                                affectedRecords += (status?.Created ?? 0) + (status?.Updated ?? 0) + (status?.Deleted ?? 0);
                                break;
                            }

                            if (_timeProvider.GetUtcNow().UtcDateTime.Subtract(taskStartedTime) > TimeSpan.FromSeconds(30))
                            {
                                await RenewLockAsync(context);
                                _logger.LogInformation("Checking script operation task ({TaskId}) status: Created: {Created} Updated: {Updated} Deleted: {Deleted} Conflicts: {Conflicts} Total: {Total}", taskId, status?.Created, status?.Updated, status?.Deleted, status?.VersionConflicts, status?.Total);
                            }

                            var delay = TimeSpan.FromMilliseconds(50);
                            if (attempts > 20)
                                delay = TimeSpan.FromSeconds(5);
                            else if (attempts > 10)
                                delay = TimeSpan.FromSeconds(1);
                            else if (attempts > 5)
                                delay = TimeSpan.FromMilliseconds(250);

                            await Task.Delay(delay, _timeProvider);
                        } while (true);

                        _logger.LogInformation("Migrated stack events: Target={TargetId} Events={UpdatedEvents} Dupes={DuplicateIds}", targetStack.Id, affectedRecords, duplicateStacks.Select(s => s.Id));

                        totalUpdatedEventCount += affectedRecords;
                    }

                    if (_timeProvider.GetUtcNow().UtcDateTime.Subtract(lastStatus) > TimeSpan.FromSeconds(5))
                    {
                        lastStatus = _timeProvider.GetUtcNow().UtcDateTime;
                        _logger.LogInformation("Total={Processed}/{Total} Errors={ErrorCount}", processed, total, error);
                        await _cacheClient.RemoveByPrefixAsync(nameof(Stack));
                    }
                }
                catch (Exception ex)
                {
                    error++;
                    _logger.LogError(ex, "Error fixing duplicate stack {ProjectId} {SignatureHash}", projectId, signature);
                }
            }

            await _elasticClient.Indices.RefreshAsync(_config.Stacks.VersionedName);
            duplicateStackAgg = await _elasticClient.SearchAsync<Stack>(q => q
                .Query(q => q.QueryString(qs => qs.Query("is_deleted:false")))
                .Size(0)
                .AddAggregation("stacks", a => a.Terms(t => t.Field(f => f.DuplicateSignature).MinDocCount(2).Size(10000))));
            _logger.LogRequest(duplicateStackAgg, LogLevel.Trace);

            buckets = duplicateStackAgg.Aggregations?.GetStringTerms("stacks")?.Buckets.ToList() ?? [];
            total += buckets.Count;
            batch++;

            _logger.LogInformation("Done de-duping stacks: Total={Processed}/{Total} Errors={ErrorCount}", processed, total, error);
            await _cacheClient.RemoveByPrefixAsync(nameof(Stack));
        }
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
