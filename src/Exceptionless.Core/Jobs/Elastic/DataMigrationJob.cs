using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Elasticsearch.Net;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.DateTimeExtensions;
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
            string scope = elasticOptions.ScopePrefix;
            var cutOffDate = elasticOptions.ReindexCutOffDate;
            bool shouldUpdateAliases = true;
            
            var client = _configuration.Client;
            await _configuration.ConfigureIndexesAsync().AnyContext();
            
            var indexMap = new Dictionary<string, (string Index, string IndexType, string IndexAlias, string DateField)> {
                { $"{scope}organizations-v1", ( $"{scope}organizations-v1", "organization", $"{scope}organizations", "updated_utc" ) },
                { $"{scope}projects-v1", ( $"{scope}organizations-v1", "project", $"{scope}projects", "updated_utc" ) },
                { $"{scope}stacks-v1", ( $"{scope}stacks-v1", "stacks", $"{scope}stacks", "last_occurrence" ) },
                { $"{scope}tokens-v1", ( $"{scope}organizations-v1", "token", $"{scope}tokens", "updated_utc" ) },
                { $"{scope}users-v1", ( $"{scope}organizations-v1", "user", $"{scope}users", "updated_utc" ) },
                { $"{scope}webhooks-v1", ( $"{scope}organizations-v1", "webhook", $"{scope}webhooks", "created_utc") }
            };
            
            // create the new indexes, don't migrate yet
            foreach (var index in _configuration.Indexes.OfType<DailyIndex>()) {
                for (int day = 0; day <= retentionPeriod.Days; day++) {
                    var date = day == 0 ? SystemClock.UtcNow : SystemClock.UtcNow.SubtractDays(day);
                    string indexToCreate = $"{scope}events-v1-{date:yyyy.MM.dd}";
                    indexMap.Add(indexToCreate, ( $"{scope}events-v1-{date:yyyy.MM.dd}", "events", $"{scope}events", "updated_utc" ));
                    
                    await index.EnsureIndexAsync(date).AnyContext();
                }
            }
            
            var reindexTasks = new List<(TaskId TaskId, string SourceIndex, string SourceType, string TargetIndex)>();
            var completedTasks = new List<(TaskId TaskId, string SourceIndex, string SourceType, string TargetIndex, Nest.TaskStatus Status)>();

            foreach (var indexes in indexMap.Page(3)) {
                foreach (var kvp in indexes) {
                    var response = String.IsNullOrEmpty(kvp.Value.DateField)
                        ? await client.ReindexOnServerAsync(r => r.Source(s => s.Remote(ConfigureRemoteElasticSource).Index(kvp.Value.Index).Query<object>(q => q.Term("_type", kvp.Value.IndexType)).Sort<object>(f => f.Field("id", SortOrder.Ascending))).Destination(d => d.Index(kvp.Key)).Conflicts(Conflicts.Proceed).WaitForCompletion(false)).AnyContext()
                        : await client.ReindexOnServerAsync(r => r.Source(s => s.Remote(ConfigureRemoteElasticSource).Index(kvp.Value.Index).Query<object>(q => q.Term("_type", kvp.Value.IndexType) && q.DateRange(d => d.Field(kvp.Value.DateField).GreaterThanOrEquals(cutOffDate))).Sort<object>(f => f.Field(kvp.Value.DateField, SortOrder.Ascending))).Destination(d => d.Index(kvp.Key)).Conflicts(Conflicts.Proceed).WaitForCompletion(false)).AnyContext();

                    _logger.LogInformation("{SourceIndex}/{SourceType} -> {TargetIndex}: {TaskId}", kvp.Value.Index, kvp.Value.IndexType, kvp.Key, response.Task);
                    _logger.LogInformation(response.GetRequest());

                    reindexTasks.Add((response.Task, kvp.Value.Index, kvp.Value.IndexType, kvp.Key));
                }

                while (reindexTasks.Count > 0) {
                    foreach (var task in reindexTasks.ToArray()) {
                        var taskStatus = await client.Tasks.GetTaskAsync(task.TaskId, t => t.WaitForCompletion(false)).AnyContext();
                        _logger.LogTraceRequest(taskStatus);

                        if (!taskStatus.IsValid) {
                            if (taskStatus.ServerError?.Status == 404) {
                                _logger.LogInformation("Checking task status {TaskId} for {TargetIndex}: Task isn't running and hasn't stored its result", task.TaskId, task.TargetIndex);
                                continue;
                            }

                            _logger.LogWarning(taskStatus.OriginalException, "Error getting task status {TaskId} for {TargetIndex}: {Message}", task.TaskId, task.TargetIndex, taskStatus.GetErrorMessage());
                            if (taskStatus.ServerError?.Status == 429)
                                await Task.Delay(TimeSpan.FromSeconds(1));

                            continue;
                        }

                        if (!taskStatus.Completed) {
                            _logger.LogInformation("Checking task status {TaskId} for {TargetIndex} - Created: {Created} Updated: {Updated} Deleted: {Deleted} Conflicts: {Conflicts} Total: {Total}", task.TaskId, task.TargetIndex, taskStatus.Task.Status.Created, taskStatus.Task.Status.Updated, taskStatus.Task.Status.Deleted, taskStatus.Task.Status.VersionConflicts, taskStatus.Task.Status.Total);
                            continue;
                        }

                        reindexTasks.Remove(task);
                        completedTasks.Add((task.TaskId, task.SourceIndex, task.SourceType, task.TargetIndex, taskStatus.Task.Status));
                        var sourceCount = await client.CountAsync<object>(d => d.Index(task.SourceIndex)).AnyContext();
                        var targetCount = await client.CountAsync<object>(d => d.Index(task.TargetIndex)).AnyContext();
                        _logger.LogInformation("Reindex completed: {SourceIndex}/{SourceType}: {SourceCount} {TargetIndex}: {TargetCount} - Created: {Created} Updated: {Updated} Deleted: {Deleted} Conflicts: {Conflicts} Total: {Total}", task.SourceIndex, task.SourceType, sourceCount.Count, task.TargetIndex, targetCount.Count, taskStatus.Task.Status.Created, taskStatus.Task.Status.Updated, taskStatus.Task.Status.Deleted, taskStatus.Task.Status.VersionConflicts, taskStatus.Task.Status.Total);
                    }

                    await Task.Delay(TimeSpan.FromSeconds(10));
                }
            }

            _logger.LogInformation("All reindex tasks completed.");
            foreach (var task in completedTasks) {
                var sourceCount = await client.CountAsync<object>(d => d.Index(task.SourceIndex)).AnyContext();
                var targetCount = await client.CountAsync<object>(d => d.Index(task.TargetIndex)).AnyContext();
                _logger.LogInformation("Reindex completed: {SourceIndex}/{SourceType}: {SourceCount} {TargetIndex}: {TargetCount} - Created: {Created} Updated: {Updated} Deleted: {Deleted} Conflicts: {Conflicts} Total: {Total}", task.SourceIndex, task.SourceType, sourceCount.Count, task.TargetIndex, targetCount.Count, task.Status.Created, task.Status.Updated, task.Status.Deleted, task.Status.VersionConflicts, task.Status.Total);
            }

            if (shouldUpdateAliases) {
                foreach (var kvp in indexMap) {
                    var aliasResponse = await client.Indices.BulkAliasAsync(a => a.Add(d => d.Alias(kvp.Value.IndexAlias).Index(kvp.Key)).Remove(d => d.Alias(kvp.Value.IndexAlias).Index(kvp.Value.Index))).AnyContext();
                    _logger.LogInformation("Updated alias {IndexAlias} to point to {NewIndex}", kvp.Value.IndexAlias, kvp.Key);
                    _logger.LogInformation(aliasResponse.GetRequest());
                }
            }

            return JobResult.Success;
        }

        private IRemoteSource ConfigureRemoteElasticSource(RemoteSourceDescriptor rsd) {
            var elasticOptions = _configuration.Options.ElasticsearchToMigrate;
            if (!String.IsNullOrEmpty(elasticOptions.UserName) && !String.IsNullOrEmpty(elasticOptions.Password))
                rsd.Username(elasticOptions.UserName).Password(elasticOptions.Password);
                
            return rsd.Host(new Uri(elasticOptions.ServerUrl));
        }
    }
}
