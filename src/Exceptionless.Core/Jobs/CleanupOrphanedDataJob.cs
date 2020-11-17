using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Configuration;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Repositories;
using Foundatio.Repositories.Elasticsearch.Extensions;
using Foundatio.Repositories.Models;
using Foundatio.Utility;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Nest;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Exceptionless.Core.Jobs {
    [Job(Description = "Deletes orphaned data.", IsContinuous = false)]
    public class CleanupOrphanedDataJob : JobWithLockBase, IHealthCheck {
        private readonly ExceptionlessElasticConfiguration _config;
        private readonly IElasticClient _elasticClient;
        private readonly IStackRepository _stackRepository;
        private readonly IEventRepository _eventRepository;
        private readonly ICacheClient _cacheClient;
        private readonly ILockProvider _lockProvider;
        private DateTime? _lastRun;

        public CleanupOrphanedDataJob(ExceptionlessElasticConfiguration config, IStackRepository stackRepository, IEventRepository eventRepository, ICacheClient cacheClient, ILockProvider lockProvider, ILoggerFactory loggerFactory = null) : base(loggerFactory) {
            _config = config;
            _elasticClient = config.Client;
            _stackRepository = stackRepository;
            _eventRepository = eventRepository;
            _cacheClient = cacheClient;
            _lockProvider = lockProvider;
        }

        protected override Task<ILock> GetLockAsync(CancellationToken cancellationToken = default) {
            return _lockProvider.AcquireAsync(nameof(CleanupOrphanedDataJob), TimeSpan.FromHours(2), new CancellationToken(true));
        }

        protected override async Task<JobResult> RunInternalAsync(JobContext context) {
            await DeleteOrphanedEventsByStackAsync(context).AnyContext();
            await DeleteOrphanedEventsByProjectAsync(context).AnyContext();
            await DeleteOrphanedEventsByOrganizationAsync(context).AnyContext();

            await FixDuplicateStacks(context).AnyContext();

            return JobResult.Success;
        }

        public async Task DeleteOrphanedEventsByStackAsync(JobContext context) {
            // get approximate number of unique stack ids
            var stackCardinality = await _elasticClient.SearchAsync<PersistentEvent>(s => s.Aggregations(a => a
                .Cardinality("cardinality_stack_id", c => c.Field(f => f.StackId).PrecisionThreshold(40000))));

            var uniqueStackIdCount = stackCardinality.Aggregations.Cardinality("cardinality_stack_id")?.Value;
            if (!uniqueStackIdCount.HasValue || uniqueStackIdCount.Value <= 0)
                return;

            // break into batches of 500
            const int batchSize = 500;
            int buckets = (int)uniqueStackIdCount.Value / batchSize;
            buckets = Math.Max(1, buckets);
            int totalOrphanedEventCount = 0;
            int totalStackIds = 0;

            for (int batchNumber = 0; batchNumber < buckets; batchNumber++) {
                await RenewLockAsync(context);

                var stackIdTerms = await _elasticClient.SearchAsync<PersistentEvent>(s => s.Aggregations(a => a
                    .Terms("terms_stack_id", c => c.Field(f => f.StackId).Include(batchNumber, buckets).Size(batchSize * 2))));

                var stackIds = stackIdTerms.Aggregations.Terms("terms_stack_id").Buckets.Select(b => b.Key).ToArray();
                if (stackIds.Length == 0)
                    continue;

                totalStackIds += stackIds.Length;

                var stacks = await _elasticClient.MultiGetAsync(r => r.SourceEnabled(false).GetMany<Stack>(stackIds));
                var missingStackIds = stacks.Hits.Where(h => !h.Found).Select(h => h.Id).ToArray();


                if (missingStackIds.Length == 0) {
                    _logger.LogInformation("{BatchNumber}/{BatchCount}: Did not find any missing stacks out of {StackIdCount}", batchNumber, buckets, stackIds.Length);
                    continue;
                }

                totalOrphanedEventCount += missingStackIds.Length;
                _logger.LogInformation("{BatchNumber}/{BatchCount}: Found {OrphanedEventCount} orphaned events from missing stacks {MissingStackIds} out of {StackIdCount}", batchNumber, buckets, missingStackIds.Length, missingStackIds, stackIds.Length);
                await _elasticClient.DeleteByQueryAsync<PersistentEvent>(r => r.Query(q => q.Terms(t => t.Field(f => f.StackId).Terms(missingStackIds))));
            }

            _logger.LogInformation("Found {OrphanedEventCount} orphaned events from missing stacks out of {StackIdCount}", totalOrphanedEventCount, totalStackIds);
        }

        public async Task DeleteOrphanedEventsByProjectAsync(JobContext context) {
            // get approximate number of unique project ids
            var projectCardinality = await _elasticClient.SearchAsync<PersistentEvent>(s => s.Aggregations(a => a
                .Cardinality("cardinality_project_id", c => c.Field(f => f.ProjectId).PrecisionThreshold(40000))));

            var uniqueProjectIdCount = projectCardinality.Aggregations.Cardinality("cardinality_project_id")?.Value;
            if (!uniqueProjectIdCount.HasValue || uniqueProjectIdCount.Value <= 0)
                return;

            // break into batches of 500
            const int batchSize = 500;
            int buckets = (int)uniqueProjectIdCount.Value / batchSize;
            buckets = Math.Max(1, buckets);
            int totalOrphanedEventCount = 0;
            int totalProjectIds = 0;

            for (int batchNumber = 0; batchNumber < buckets; batchNumber++) {
                await RenewLockAsync(context);

                var projectIdTerms = await _elasticClient.SearchAsync<PersistentEvent>(s => s.Aggregations(a => a
                    .Terms("terms_project_id", c => c.Field(f => f.ProjectId).Include(batchNumber, buckets).Size(batchSize * 2))));

                var projectIds = projectIdTerms.Aggregations.Terms("terms_project_id").Buckets.Select(b => b.Key).ToArray();
                if (projectIds.Length == 0)
                    continue;

                totalProjectIds += projectIds.Length;

                var projects = await _elasticClient.MultiGetAsync(r => r.SourceEnabled(false).GetMany<Project>(projectIds));
                var missingProjectIds = projects.Hits.Where(h => !h.Found).Select(h => h.Id).ToArray();

                if (missingProjectIds.Length == 0) {
                    _logger.LogInformation("{BatchNumber}/{BatchCount}: Did not find any missing projects out of {ProjectIdCount}", batchNumber, buckets, projectIds.Length);
                    continue;
                }

                _logger.LogInformation("{BatchNumber}/{BatchCount}: Found {OrphanedEventCount} orphaned events from missing projects {MissingProjectIds} out of {ProjectIdCount}", batchNumber, buckets, missingProjectIds.Length, missingProjectIds, projectIds.Length);
                await _elasticClient.DeleteByQueryAsync<PersistentEvent>(r => r.Query(q => q.Terms(t => t.Field(f => f.ProjectId).Terms(missingProjectIds))));
            }

            _logger.LogInformation("Found {OrphanedEventCount} orphaned events from missing projects out of {ProjectIdCount}", totalOrphanedEventCount, totalProjectIds);
        }

        public async Task DeleteOrphanedEventsByOrganizationAsync(JobContext context) {
            // get approximate number of unique organization ids
            var organizationCardinality = await _elasticClient.SearchAsync<PersistentEvent>(s => s.Aggregations(a => a
                .Cardinality("cardinality_organization_id", c => c.Field(f => f.OrganizationId).PrecisionThreshold(40000))));

            var uniqueOrganizationIdCount = organizationCardinality.Aggregations.Cardinality("cardinality_organization_id")?.Value;
            if (!uniqueOrganizationIdCount.HasValue || uniqueOrganizationIdCount.Value <= 0)
                return;

            // break into batches of 500
            const int batchSize = 500;
            int buckets = (int)uniqueOrganizationIdCount.Value / batchSize;
            buckets = Math.Max(1, buckets);
            int totalOrphanedEventCount = 0;
            int totalOrganizationIds = 0;

            for (int batchNumber = 0; batchNumber < buckets; batchNumber++) {
                await RenewLockAsync(context);

                var organizationIdTerms = await _elasticClient.SearchAsync<PersistentEvent>(s => s.Aggregations(a => a
                    .Terms("terms_organization_id", c => c.Field(f => f.OrganizationId).Include(batchNumber, buckets).Size(batchSize * 2))));

                var organizationIds = organizationIdTerms.Aggregations.Terms("terms_organization_id").Buckets.Select(b => b.Key).ToArray();
                if (organizationIds.Length == 0)
                    continue;

                totalOrganizationIds += organizationIds.Length;

                var organizations = await _elasticClient.MultiGetAsync(r => r.SourceEnabled(false).GetMany<Organization>(organizationIds));
                var missingOrganizationIds = organizations.Hits.Where(h => !h.Found).Select(h => h.Id).ToArray();

                if (missingOrganizationIds.Length == 0) {
                    _logger.LogInformation("{BatchNumber}/{BatchCount}: Did not find any missing organizations out of {OrganizationIdCount}", batchNumber, buckets, organizationIds.Length);
                    continue;
                }

                _logger.LogInformation("{BatchNumber}/{BatchCount}: Found {OrphanedEventCount} orphaned events from missing organizations {MissingOrganizationIds} out of {OrganizationIdCount}", batchNumber, buckets, missingOrganizationIds.Length, missingOrganizationIds, organizationIds.Length);
                await _elasticClient.DeleteByQueryAsync<PersistentEvent>(r => r.Query(q => q.Terms(t => t.Field(f => f.OrganizationId).Terms(missingOrganizationIds))));
            }

            _logger.LogInformation("Found {OrphanedEventCount} orphaned events from missing organizations out of {OrganizationIdCount}", totalOrphanedEventCount, totalOrganizationIds);
        }

        public async Task FixDuplicateStacks(JobContext context) {
            _logger.LogInformation("Getting duplicate stacks");

            var duplicateStackAgg = await _elasticClient.SearchAsync<Stack>(q => q
                .QueryOnQueryString("is_deleted:false")
                .Size(0)
                .Aggregations(a => a.Terms("stacks", t => t.Field(f => f.DuplicateSignature).MinimumDocumentCount(2).Size(10000))));
            _logger.LogRequest(duplicateStackAgg, LogLevel.Trace);

            var buckets = duplicateStackAgg.Aggregations.Terms("stacks").Buckets;
            int total = buckets.Count;
            int processed = 0;
            int error = 0;
            long totalUpdatedEventCount = 0;
            var lastStatus = SystemClock.Now;
            int batch = 1;

            while (buckets.Count > 0) {
                _logger.LogInformation($"Found {buckets.Count} duplicate stacks in batch #{batch}.");
                await RenewLockAsync(context);

                foreach (var duplicateSignature in buckets) {
                    string projectId = null;
                    string signature = null;
                    try {
                        var parts = duplicateSignature.Key.Split(':');
                        if (parts.Length != 2) {
                            _logger.LogError("Error parsing duplicate signature {DuplicateSignature}", duplicateSignature.Key);
                            continue;
                        }
                        projectId = parts[0];
                        signature = parts[1];

                        var stacks = await _stackRepository.FindAsync(q => q.Project(projectId).FilterExpression($"signature_hash:{signature}"));
                        if (stacks.Documents.Count < 2) {
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
                        if (eventCountBuckets.Count > 0) {
                            var targetStackId = eventCountBuckets.OrderByDescending(b => b.Total).First().Key;
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

                        if (shouldUpdateEvents) {
                            var response = await _elasticClient.UpdateByQueryAsync<PersistentEvent>(u => u
                                .Query(q => q.Bool(b => b.Must(m => m
                                    .Terms(t => t.Field(f => f.StackId).Terms(duplicateStacks.Select(s => s.Id)))
                                )))
                                .Script(s => s.Source($"ctx._source.stack_id = '{targetStack.Id}'").Lang(ScriptLang.Painless))
                                .Conflicts(Elasticsearch.Net.Conflicts.Proceed)
                                .WaitForCompletion(false));
                            _logger.LogRequest(response, LogLevel.Trace);

                            var taskStartedTime = SystemClock.Now;
                            var taskId = response.Task;
                            int attempts = 0;
                            long affectedRecords = 0;
                            do {
                                attempts++;
                                var taskStatus = await _elasticClient.Tasks.GetTaskAsync(taskId);
                                var status = taskStatus.Task.Status;
                                if (taskStatus.Completed) {
                                    // TODO: need to check to see if the task failed or completed successfully. Throw if it failed.
                                    if (SystemClock.Now.Subtract(taskStartedTime) > TimeSpan.FromSeconds(30))
                                        _logger.LogInformation("Script operation task ({TaskId}) completed: Created: {Created} Updated: {Updated} Deleted: {Deleted} Conflicts: {Conflicts} Total: {Total}", taskId, status.Created, status.Updated, status.Deleted, status.VersionConflicts, status.Total);

                                    affectedRecords += status.Created + status.Updated + status.Deleted;
                                    break;
                                }

                                if (SystemClock.Now.Subtract(taskStartedTime) > TimeSpan.FromSeconds(30)) {
                                    await RenewLockAsync(context);
                                    _logger.LogInformation("Checking script operation task ({TaskId}) status: Created: {Created} Updated: {Updated} Deleted: {Deleted} Conflicts: {Conflicts} Total: {Total}", taskId, status.Created, status.Updated, status.Deleted, status.VersionConflicts, status.Total);
                                }

                                var delay = TimeSpan.FromMilliseconds(50);
                                if (attempts > 20)
                                    delay = TimeSpan.FromSeconds(5);
                                else if (attempts > 10)
                                    delay = TimeSpan.FromSeconds(1);
                                else if (attempts > 5)
                                    delay = TimeSpan.FromMilliseconds(250);

                                await Task.Delay(delay);
                            } while (true);

                            _logger.LogInformation("Migrated stack events: Target={TargetId} Events={UpdatedEvents} Dupes={DuplicateIds}", targetStack.Id, affectedRecords, duplicateStacks.Select(s => s.Id));

                            totalUpdatedEventCount += affectedRecords;
                        }

                        if (SystemClock.UtcNow.Subtract(lastStatus) > TimeSpan.FromSeconds(5)) {
                            lastStatus = SystemClock.UtcNow;
                            _logger.LogInformation("Total={Processed}/{Total} Errors={ErrorCount}", processed, total, error);
                            await _cacheClient.RemoveByPrefixAsync(nameof(Stack));
                        }
                    } catch (Exception ex) {
                        error++;
                        _logger.LogError(ex, "Error fixing duplicate stack {ProjectId} {SignatureHash}", projectId, signature);
                    }
                }

                await _elasticClient.Indices.RefreshAsync(_config.Stacks.VersionedName);
                duplicateStackAgg = await _elasticClient.SearchAsync<Stack>(q => q
                    .QueryOnQueryString("is_deleted:false")
                    .Size(0)
                    .Aggregations(a => a.Terms("stacks", t => t.Field(f => f.DuplicateSignature).MinimumDocumentCount(2).Size(10000))));
                _logger.LogRequest(duplicateStackAgg, LogLevel.Trace);

                buckets = duplicateStackAgg.Aggregations.Terms("stacks").Buckets;
                total += buckets.Count;
                batch++;

                _logger.LogInformation("Done de-duping stacks: Total={Processed}/{Total} Errors={ErrorCount}", processed, total, error);
                await _cacheClient.RemoveByPrefixAsync(nameof(Stack));
            }
        }

        private Task RenewLockAsync(JobContext context) {
            _lastRun = SystemClock.UtcNow;
            return context.RenewLockAsync();
        }

        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) {
            if (!_lastRun.HasValue)
                return Task.FromResult(HealthCheckResult.Healthy("Job has not been run yet."));

            if (SystemClock.UtcNow.Subtract(_lastRun.Value) > TimeSpan.FromMinutes(65))
                return Task.FromResult(HealthCheckResult.Unhealthy("Job has not run in the last 65 minutes."));

            return Task.FromResult(HealthCheckResult.Healthy("Job has run in the last 65 minutes."));
        }
    }
}