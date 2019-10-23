using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Elasticsearch.Net;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories.Configuration;
using Foundatio.Jobs;
using Foundatio.Parsers.ElasticQueries.Extensions;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Foundatio.Repositories.Elasticsearch.Extensions;
using Microsoft.Extensions.Logging;
using Nest;

namespace Exceptionless.Core.Jobs.Elastic {
    [Job(Description = "Migrate data to new format.", IsContinuous = false)]
    public class DataMigrationJob : JobBase {
        private readonly ExceptionlessElasticConfiguration _configuration;

        public DataMigrationJob(ILoggerFactory loggerFactory, ExceptionlessElasticConfiguration configuration)
            : base(loggerFactory) {

            _configuration = configuration;
        }

        protected override async Task<JobResult> RunInternalAsync(JobContext context) {
            var elasticOptions = _configuration.Options;
            if (elasticOptions.ElasticsearchToMigrate == null)
                return JobResult.CancelledWithMessage($"Please configure the connection string EX_{nameof(elasticOptions.ElasticsearchToMigrate)}.");

            string scope = elasticOptions.ScopePrefix;
            var cutOffDate = DateTime.MinValue;
            bool shouldUpdateAliases = true;
            
            var client = _configuration.Client;
            
            // create the new indexes, don't migrate yet
            foreach (var index in _configuration.Indexes) {
                if (!(index is VersionedIndex versionedIndex))
                    continue;
                
                var existsResponse = await client.Indices.ExistsAsync(Indices.Parse(versionedIndex.VersionedName)).AnyContext();
                _logger.LogTraceRequest(existsResponse);
                if (existsResponse.Exists)
                    continue;

                string indexName = versionedIndex.VersionedName;
                if (String.IsNullOrEmpty(elasticOptions.ScopePrefix))
                    indexName = scope + indexName;
                else
                    indexName = scope + indexName.Substring(elasticOptions.ScopePrefix.Length);

                var indexSettings = new CreateIndexDescriptor(indexName);
                versionedIndex.ConfigureIndex(indexSettings);
                indexSettings.Aliases(m => new AliasesDescriptor());
                
                var createIndexResponse = await client.Indices.CreateAsync(indexName, d => indexSettings).AnyContext();
                _logger.LogTraceRequest(createIndexResponse);
            }
            
            var indexMap = new Dictionary<string, (string Index, string IndexType, string IndexAlias)> {
                { $"{scope}events-v1", ( $"{scope}events-v1", "event", $"{scope}events" ) },
                { $"{scope}organizations-v1", ( $"{scope}organizations-v1", "organization", $"{scope}organizations" ) },
                { $"{scope}projects-v1", ( $"{scope}organizations-v1", "project", $"{scope}projects" ) },
                { $"{scope}stacks-v1", ( $"{scope}stacks-v1", "stack", $"{scope}stacks" ) },
                { $"{scope}tokens-v1", ( $"{scope}organizations-v1", "token", $"{scope}tokens" ) },
                { $"{scope}users-v1", ( $"{scope}organizations-v1", "user", $"{scope}users" ) },
                { $"{scope}webhooks-v1", ( $"{scope}organizations-v1", "webhooks", $"{scope}webhooks" ) },
                { $"{scope}migrations", ( $"{scope}migrations", "migration", $"{scope}migrations" ) }
            };
            
            var reindexTasks = new List<(TaskId TaskId, string SourceIndex, string SourceType, string TargetIndex)>();
            var completedTasks = new List<(TaskId TaskId, string SourceIndex, string SourceType, string TargetIndex, Nest.TaskStatus Status)>();
            
            foreach (var kvp in indexMap) {
                var response = await client.ReindexOnServerAsync(r => r
                    .Source(s => s
                        .Remote(ConfigureRemoteElasticSource)
                        .Index(kvp.Value.Index)
                        .Query<object>(q => q.DateRange(d => d.Field(kvp.Value.IndexType == "event" ? "created_utc" : "updated_utc").GreaterThanOrEquals(cutOffDate))))
                    .Destination(d => d
                        .Index(kvp.Key))
                    .Conflicts(Conflicts.Proceed)
                    .WaitForCompletion(false)).AnyContext();
                
                _logger.LogInformation("{SourceIndex}/{SourceType} -> {TargetIndex}: {TaskId}", kvp.Value.Index, kvp.Value.IndexType, kvp.Key, response.Task);
                _logger.LogInformation(response.GetRequest());
                
                reindexTasks.Add((response.Task, kvp.Value.Index, kvp.Value.IndexType, kvp.Key));
            }

            while (reindexTasks.Count > 0) {
                foreach (var task in reindexTasks.ToArray()) {
                    var taskStatus = await client.Tasks.GetTaskAsync(task.TaskId, t => t.WaitForCompletion(false)).AnyContext();
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
