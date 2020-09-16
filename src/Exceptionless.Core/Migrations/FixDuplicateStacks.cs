using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Configuration;
using Foundatio.Caching;
using Foundatio.Repositories;
using Foundatio.Repositories.Elasticsearch.Extensions;
using Foundatio.Repositories.Migrations;
using Foundatio.Repositories.Models;
using Foundatio.Utility;
using Microsoft.Extensions.Logging;
using Nest;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Exceptionless.Core.Migrations {
    public sealed class FixDuplicateStacks : MigrationBase {
        private readonly IElasticClient _client;
        private readonly ICacheClient _cache;
        private readonly IStackRepository _stackRepository;
        private readonly IEventRepository _eventRepository;
        private readonly ExceptionlessElasticConfiguration _config;

        public FixDuplicateStacks(ExceptionlessElasticConfiguration configuration, IStackRepository stackRepository, IEventRepository eventRepository, ILoggerFactory loggerFactory) : base(loggerFactory) {
            _config = configuration;
            _client = configuration.Client;
            _cache = configuration.Cache;
            _stackRepository = stackRepository;
            _eventRepository = eventRepository;

            MigrationType = MigrationType.Repeatable;
        }

        public override async Task RunAsync(MigrationContext context) {
            _logger.LogInformation("Getting duplicate stacks");

            var duplicateStackAgg = await _client.SearchAsync<Stack>(q => q
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
                _logger.LogInformation($"Found {total} duplicate stacks in batch #{batch}.");

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
                            var response = await _client.UpdateByQueryAsync<PersistentEvent>(u => u
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
                                var taskStatus = await _client.Tasks.GetTaskAsync(taskId);
                                var status = taskStatus.Task.Status;
                                if (taskStatus.Completed) {
                                    // TODO: need to check to see if the task failed or completed successfully. Throw if it failed.
                                    if (SystemClock.Now.Subtract(taskStartedTime) > TimeSpan.FromSeconds(30))
                                        _logger.LogInformation("Script operation task ({TaskId}) completed: Created: {Created} Updated: {Updated} Deleted: {Deleted} Conflicts: {Conflicts} Total: {Total}", taskId, status.Created, status.Updated, status.Deleted, status.VersionConflicts, status.Total);

                                    affectedRecords += status.Created + status.Updated + status.Deleted;
                                    break;
                                }

                                if (SystemClock.Now.Subtract(taskStartedTime) > TimeSpan.FromSeconds(30))
                                    _logger.LogInformation("Checking script operation task ({TaskId}) status: Created: {Created} Updated: {Updated} Deleted: {Deleted} Conflicts: {Conflicts} Total: {Total}", taskId, status.Created, status.Updated, status.Deleted, status.VersionConflicts, status.Total);

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
                            await _cache.RemoveByPrefixAsync(nameof(Stack));
                        }
                    } catch (Exception ex) {
                        error++;
                        _logger.LogError(ex, "Error fixing duplicate stack {ProjectId} {SignatureHash}", projectId, signature);
                    }
                }

                await _client.Indices.RefreshAsync(_config.Stacks.VersionedName);
                duplicateStackAgg = await _client.SearchAsync<Stack>(q => q
                    .QueryOnQueryString("is_deleted:false")
                    .Size(0)
                    .Aggregations(a => a.Terms("stacks", t => t.Field(f => f.DuplicateSignature).MinimumDocumentCount(2).Size(10000))));
                _logger.LogRequest(duplicateStackAgg, LogLevel.Trace);

                buckets = duplicateStackAgg.Aggregations.Terms("stacks").Buckets;
                total += buckets.Count;
                batch++;

                _logger.LogInformation("Done de-duping stacks: Total={Processed}/{Total} Errors={ErrorCount}", processed, total, error);
                await _cache.RemoveByPrefixAsync(nameof(Stack));
            }
        }
    }
}