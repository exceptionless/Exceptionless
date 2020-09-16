using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using Foundatio.Caching;
using Foundatio.Repositories.Elasticsearch.Extensions;
using Foundatio.Repositories.Migrations;
using Microsoft.Extensions.Logging;
using Nest;

namespace Exceptionless.Core.Migrations {
    public sealed class SetStackDuplicateSignature : MigrationBase {
        private readonly IElasticClient _client;
        private readonly ExceptionlessElasticConfiguration _config;
        private readonly ICacheClient _cache;

        public SetStackDuplicateSignature(ExceptionlessElasticConfiguration configuration, ILoggerFactory loggerFactory) : base(loggerFactory) {
            _config = configuration;
            _client = configuration.Client;
            _cache = configuration.Cache;
            
            MigrationType = MigrationType.Repeatable;
        }

        public override async Task RunAsync(MigrationContext context) {
            _logger.LogInformation("Begin refreshing all indices");
            await _config.Client.Indices.RefreshAsync(Indices.All);
            _logger.LogInformation("Done refreshing all indices");

            _logger.LogInformation("Updating Stack mappings...");
            var response = await _client.MapAsync<Stack>(d => {
                d.Index(_config.Stacks.VersionedName);
                d.Properties(p => p.Keyword(f => f.Name(s => s.DuplicateSignature)));

                return d;
            });
            _logger.LogRequest(response);

            _logger.LogInformation("Start populating stack duplicate signature");
            var sw = Stopwatch.StartNew();
            const string script = "ctx._source.duplicate_signature = ctx._source.project_id + ':' + ctx._source.signature_hash;";
            var stackResponse = await _client.UpdateByQueryAsync<Stack>(x => x
                .QueryOnQueryString("NOT _exists_:duplicate_signature")
                .Script(s => s.Source(script).Lang(ScriptLang.Painless))
                .Conflicts(Elasticsearch.Net.Conflicts.Proceed)
                .WaitForCompletion(false));

            _logger.LogRequest(stackResponse, Microsoft.Extensions.Logging.LogLevel.Information);

            var taskId = stackResponse.Task;
            int attempts = 0;
            long affectedRecords = 0;
            do {
                attempts++;
                var taskStatus = await _client.Tasks.GetTaskAsync(taskId);
                var status = taskStatus.Task.Status;
                if (taskStatus.Completed) {
                    // TODO: need to check to see if the task failed or completed successfully. Throw if it failed.
                    _logger.LogInformation("Script operation task ({TaskId}) completed: Created: {Created} Updated: {Updated} Deleted: {Deleted} Conflicts: {Conflicts} Total: {Total}", taskId, status.Created, status.Updated, status.Deleted, status.VersionConflicts, status.Total);
                    affectedRecords += status.Created + status.Updated + status.Deleted;
                    break;
                }

                _logger.LogInformation("Checking script operation task ({TaskId}) status: Created: {Created} Updated: {Updated} Deleted: {Deleted} Conflicts: {Conflicts} Total: {Total}", taskId, status.Created, status.Updated, status.Deleted, status.VersionConflicts, status.Total);
                var delay = TimeSpan.FromSeconds(attempts <= 5 ? 1 : 5);
                await Task.Delay(delay);
            } while (true);

            _logger.LogInformation("Finished adding stack duplicate signature: Time={Duration:d\\.hh\\:mm} Completed={Completed:N0} Total={Total:N0} Errors={Errors:N0}", sw.Elapsed, affectedRecords, stackResponse.Total, stackResponse.Failures.Count);
            
            _logger.LogInformation("Invalidating Stack Cache");
            await _cache.RemoveByPrefixAsync(nameof(Stack));
            _logger.LogInformation("Invalidating Stack Cache");
        }
    }
}