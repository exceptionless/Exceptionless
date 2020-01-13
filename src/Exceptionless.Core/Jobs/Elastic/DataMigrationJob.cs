using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Elasticsearch.Net;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.DateTimeExtensions;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Parsers.ElasticQueries.Extensions;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Foundatio.Repositories.Elasticsearch.Extensions;
using Foundatio.Utility;
using Microsoft.Extensions.Logging;
using Nest;

namespace Exceptionless.Core.Jobs.Elastic {
    [Job(Description = "Migrate data to new format.", IsContinuous = false)]
    public class DataMigrationJob : JobBase {
        private readonly ExceptionlessElasticConfiguration _configuration;
        private const string MIGRATE_VERSION_SCRIPT = "if (ctx._source.version instanceof String == false) { ctx._source.version = 'v' + ctx._source.version.major; }";

        public DataMigrationJob(
            ExceptionlessElasticConfiguration configuration,
            ILoggerFactory loggerFactory
        ) : base(loggerFactory) {
            _configuration = configuration;
        }

        protected override async Task<JobResult> RunInternalAsync(JobContext context) {
            var elasticOptions = _configuration.Options;
            if (elasticOptions.ElasticsearchToMigrate == null)
                return JobResult.CancelledWithMessage($"Please configure the connection string EX_{nameof(elasticOptions.ElasticsearchToMigrate)}.");

            var retentionPeriod = _configuration.Events.MaxIndexAge.GetValueOrDefault(TimeSpan.FromDays(180));
            string sourceScope = elasticOptions.ElasticsearchToMigrate.Scope;
            string scope = elasticOptions.ScopePrefix;
            var cutOffDate = elasticOptions.ReindexCutOffDate;

            var client = _configuration.Client;
            await _configuration.ConfigureIndexesAsync().AnyContext();

            var workItemQueue = new Queue<ReindexWorkItem>();
            workItemQueue.Enqueue(new ReindexWorkItem($"{sourceScope}organizations-v1", "organization", $"{scope}organizations-v1", "updated_utc"));
            workItemQueue.Enqueue(new ReindexWorkItem($"{sourceScope}organizations-v1", "project", $"{scope}projects-v1", "updated_utc"));
            workItemQueue.Enqueue(new ReindexWorkItem($"{sourceScope}organizations-v1", "token", $"{scope}tokens-v1", "updated_utc"));
            workItemQueue.Enqueue(new ReindexWorkItem($"{sourceScope}organizations-v1", "user", $"{scope}users-v1", "updated_utc"));
            workItemQueue.Enqueue(new ReindexWorkItem($"{sourceScope}organizations-v1", "webhook", $"{scope}webhooks-v1", "created_utc", script: MIGRATE_VERSION_SCRIPT));
            workItemQueue.Enqueue(new ReindexWorkItem($"{sourceScope}stacks-v1", "stacks", $"{scope}stacks-v1", "last_occurrence"));

            // create the new indexes, don't migrate yet
            foreach (var index in _configuration.Indexes.OfType<DailyIndex>()) {
                for (int day = 0; day <= retentionPeriod.Days; day++) {
                    var date = day == 0 ? SystemClock.UtcNow : SystemClock.UtcNow.SubtractDays(day);
                    string indexToCreate = $"{scope}events-v1-{date:yyyy.MM.dd}";
                    workItemQueue.Enqueue(new ReindexWorkItem($"{sourceScope}events-v1-{date:yyyy.MM.dd}", "events", indexToCreate, "updated_utc", () => index.EnsureIndexAsync(date)));
                }
            }

            // Reset the alias cache
            var aliasCache = new ScopedCacheClient(_configuration.Cache, "alias");
            await aliasCache.RemoveAllAsync().AnyContext();

            var started = SystemClock.UtcNow;
            var lastProgress = SystemClock.UtcNow;
            int retriesCount = 0;
            int totalTasks = workItemQueue.Count;
            var workingTasks = new List<ReindexWorkItem>();
            var completedTasks = new List<ReindexWorkItem>();
            var failedTasks = new List<ReindexWorkItem>();
            while (true) {
                if (workingTasks.Count == 0 && workItemQueue.Count == 0)
                    break;

                if (workingTasks.Count < 10 && workItemQueue.TryDequeue(out var dequeuedWorkItem)) {
                    if (dequeuedWorkItem.CreateIndex != null) {
                        try {
                            await dequeuedWorkItem.CreateIndex().AnyContext();
                        } catch (Exception ex) {
                            _logger.LogError(ex, "Failed to create index for {TargetIndex}", dequeuedWorkItem.TargetIndex);
                            continue;
                        }
                    }

                    int batchSize = 1000;
                    if (dequeuedWorkItem.Attempts == 1)
                        batchSize = 500;
                    else if (dequeuedWorkItem.Attempts >= 2)
                        batchSize = 250;

                    var response = await client.ReindexOnServerAsync(r => r
                        .Source(s => s
                            .Remote(ConfigureRemoteElasticSource)
                            .Index(dequeuedWorkItem.SourceIndex)
                            .Size(batchSize)
                            .Query<object>(q => {
                                var container = q.Term("_type", dequeuedWorkItem.SourceIndexType);
                                if (!String.IsNullOrEmpty(dequeuedWorkItem.DateField))
                                    container &= q.DateRange(d => d.Field(dequeuedWorkItem.DateField).GreaterThanOrEquals(cutOffDate));
                                
                                return container;
                            })
                            .Sort<object>(f => f.Field(dequeuedWorkItem.DateField ?? "id", SortOrder.Ascending)))
                        .Destination(d => d
                            .Index(dequeuedWorkItem.TargetIndex))
                            .Conflicts(Conflicts.Proceed)
                            .WaitForCompletion(false)
                        .Script(s => {
                            if (!String.IsNullOrEmpty(dequeuedWorkItem.Script))
                                return s.Source(dequeuedWorkItem.Script);

                            return null;
                        })).AnyContext();

                    dequeuedWorkItem.Attempts += 1;
                    dequeuedWorkItem.TaskId = response.Task;
                    workingTasks.Add(dequeuedWorkItem);

                    _logger.LogInformation("STARTED - {TargetIndex} A:{Attempts} ({TaskId})...", dequeuedWorkItem.TargetIndex, dequeuedWorkItem.Attempts, dequeuedWorkItem.TaskId);
                    
                    continue;
                }

                double highestProgress = 0;
                foreach (var workItem in workingTasks.ToArray()) {
                    var taskStatus = await client.Tasks.GetTaskAsync(workItem.TaskId, t => t.WaitForCompletion(false)).AnyContext();
                    _logger.LogTraceRequest(taskStatus);

                    var status = taskStatus?.Task?.Status;
                    if (status == null) {
                        _logger.LogWarning(taskStatus?.OriginalException, "Error getting task status for {TargetIndex} {TaskId}: {Message}", workItem.TargetIndex, workItem.TaskId, taskStatus.GetErrorMessage());
                        if (taskStatus?.ServerError?.Status == 429)
                            await Task.Delay(TimeSpan.FromSeconds(1));

                        continue;
                    }

                    var duration = TimeSpan.FromMilliseconds(taskStatus.Task.RunningTimeInNanoseconds * 0.000001);
                    double progress = status.Total > 0 ? (status.Created + status.Updated + status.Deleted + status.VersionConflicts * 1.0) / status.Total : 0;
                    highestProgress = Math.Max(highestProgress, progress);

                    if (!taskStatus.IsValid) {
                        _logger.LogWarning(taskStatus.OriginalException, "Error getting task status for {TargetIndex} ({TaskId}): {Message}", workItem.TargetIndex, workItem.TaskId, taskStatus.GetErrorMessage());
                        workItem.ConsecutiveStatusErrors++;
                        if (taskStatus.Completed || workItem.ConsecutiveStatusErrors > 5) {
                            workingTasks.Remove(workItem);
                            workItem.LastTaskInfo = taskStatus.Task;

                            if (taskStatus.Completed && workItem.Attempts < 3) {
                                _logger.LogWarning("FAILED RETRY - {TargetIndex} in {Duration:hh\\:mm} C:{Created} U:{Updated} D:{Deleted} X:{Conflicts} T:{Total} A:{Attempts} ID:{TaskId}", workItem.TargetIndex, duration, status.Created, status.Updated, status.Deleted, status.VersionConflicts, status.Total, workItem.Attempts, workItem.TaskId);
                                workItem.ConsecutiveStatusErrors = 0;
                                workItemQueue.Enqueue(workItem);
                                totalTasks++;
                                retriesCount++;
                                await Task.Delay(TimeSpan.FromSeconds(15)).AnyContext();
                            } else {
                                _logger.LogCritical("FAILED - {TargetIndex} in {Duration:hh\\:mm} C:{Created} U:{Updated} D:{Deleted} X:{Conflicts} T:{Total} A:{Attempts} ID:{TaskId}", workItem.TargetIndex, duration, status.Created, status.Updated, status.Deleted, status.VersionConflicts, status.Total, workItem.Attempts, workItem.TaskId);
                                failedTasks.Add(workItem);
                            }
                        }

                        continue;
                    }

                    if (!taskStatus.Completed)
                        continue;

                    workingTasks.Remove(workItem);
                    workItem.LastTaskInfo = taskStatus.Task;
                    completedTasks.Add(workItem);
                    var targetCount = await client.CountAsync<object>(d => d.Index(workItem.TargetIndex)).AnyContext();

                    _logger.LogInformation("COMPLETED - {TargetIndex} ({TargetCount}) in {Duration:hh\\:mm} C:{Created} U:{Updated} D:{Deleted} X:{Conflicts} T:{Total} A:{Attempts} ID:{TaskId}", workItem.TargetIndex, targetCount.Count, duration, status.Created, status.Updated, status.Deleted, status.VersionConflicts, status.Total, workItem.Attempts, workItem.TaskId);
                }
                if (SystemClock.UtcNow.Subtract(lastProgress) > TimeSpan.FromMinutes(5)) {
                    _logger.LogInformation("STATUS - I:{Completed}/{Total} P:{Progress:F0}% T:{Duration:d\\.hh\\:mm} W:{Working} F:{Failed} R:{Retries}", completedTasks.Count, totalTasks, highestProgress * 100, SystemClock.UtcNow.Subtract(started), workingTasks.Count, failedTasks.Count, retriesCount);
                    lastProgress = SystemClock.UtcNow;
                }
                await Task.Delay(TimeSpan.FromSeconds(2));
            }

            _logger.LogInformation("----- REINDEX COMPLETE", completedTasks.Count, totalTasks, SystemClock.UtcNow.Subtract(started), failedTasks.Count, retriesCount);
            foreach (var task in completedTasks) {
                var status = task.LastTaskInfo.Status;
                var duration = TimeSpan.FromMilliseconds(task.LastTaskInfo.RunningTimeInNanoseconds * 0.000001);
                double progress = status.Total > 0 ? (status.Created + status.Updated + status.Deleted + status.VersionConflicts * 1.0) / status.Total : 0;

                var targetCount = await client.CountAsync<object>(d => d.Index(task.TargetIndex)).AnyContext();
                _logger.LogInformation("SUCCESS - {TargetIndex} ({TargetCount}) in {Duration:hh\\:mm} C:{Created} U:{Updated} D:{Deleted} X:{Conflicts} T:{Total} A:{Attempts} ID:{TaskId}", task.TargetIndex, targetCount.Count, duration, status.Created, status.Updated, status.Deleted, status.VersionConflicts, status.Total, task.Attempts, task.TaskId);
            }

            foreach (var task in failedTasks) {
                var status = task.LastTaskInfo.Status;
                var duration = TimeSpan.FromMilliseconds(task.LastTaskInfo.RunningTimeInNanoseconds * 0.000001);
                double progress = status.Total > 0 ? (status.Created + status.Updated + status.Deleted + status.VersionConflicts * 1.0) / status.Total : 0;

                var targetCount = await client.CountAsync<object>(d => d.Index(task.TargetIndex));
                _logger.LogCritical("FAILED - {TargetIndex} ({TargetCount}) in {Duration:hh\\:mm} C:{Created} U:{Updated} D:{Deleted} X:{Conflicts} T:{Total} A:{Attempts} ID:{TaskId}", task.TargetIndex, targetCount.Count, duration, status.Created, status.Updated, status.Deleted, status.VersionConflicts, status.Total, task.Attempts, task.TaskId);
            }
            _logger.LogInformation("----- SUMMARY - I:{Completed}/{Total} T:{Duration:d\\.hh\\:mm} F:{Failed} R:{Retries}", completedTasks.Count, totalTasks, SystemClock.UtcNow.Subtract(started), failedTasks.Count, retriesCount);

            _logger.LogInformation("Updating aliases");
            await _configuration.MaintainIndexesAsync();
            _logger.LogInformation("Updated aliases");
            return JobResult.Success;
        }

        private IRemoteSource ConfigureRemoteElasticSource(RemoteSourceDescriptor rsd) {
            var elasticOptions = _configuration.Options.ElasticsearchToMigrate;
            if (!String.IsNullOrEmpty(elasticOptions.UserName) && !String.IsNullOrEmpty(elasticOptions.Password))
                rsd.Username(elasticOptions.UserName).Password(elasticOptions.Password);

            return rsd.Host(new Uri(elasticOptions.ServerUrl));
        }
    }

    public class ReindexWorkItem {
        public ReindexWorkItem(string sourceIndex, string sourceIndexType, string targetIndex, string dateField, Func<Task> createIndex = null, string script = null) {
            SourceIndex = sourceIndex;
            SourceIndexType = sourceIndexType;
            TargetIndex = targetIndex;
            DateField = dateField;
            CreateIndex = createIndex;
            Script = script;
        }

        public string SourceIndex { get; set; }
        public string SourceIndexType { get; set; }
        public string TargetIndex { get; set; }
        public string DateField { get; set; }
        public Func<Task> CreateIndex { get; set; }
        public string Script { get; set; }
        public TaskId TaskId { get; set; }
        public TaskInfo LastTaskInfo { get; set; }
        public int ConsecutiveStatusErrors { get; set; }
        public int Attempts { get; set; }
    }
}
