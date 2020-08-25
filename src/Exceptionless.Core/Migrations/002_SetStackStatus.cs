using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using Foundatio.Repositories.Elasticsearch.Extensions;
using Foundatio.Repositories.Migrations;
using Microsoft.Extensions.Logging;
using Nest;

namespace Exceptionless.Core.Migrations {
    public sealed class SetStackStatus : MigrationBase {
        private readonly IElasticClient _client;
        private readonly ExceptionlessElasticConfiguration _config;

        public SetStackStatus(ExceptionlessElasticConfiguration configuration, ILoggerFactory loggerFactory) : base(loggerFactory) {
            _config = configuration;
            _client = configuration.Client;
            
            MigrationType = MigrationType.VersionedAndResumable;
            Version = 2;
        }

        public override async Task RunAsync(MigrationContext context) {
            _logger.LogInformation("Begin refreshing all indices");
            await _config.Client.Indices.RefreshAsync(Indices.All);
            _logger.LogInformation("Done refreshing all indices");
            
            _logger.LogInformation("Start migrating stacks status");
            var sw = Stopwatch.StartNew();
            const string script = "if (ctx._source.is_regressed == true) ctx._source.status = 'regressed'; else if (ctx._source.is_hidden == true) ctx._source.status = 'ignored'; else if (ctx._source.disable_notifications == true) ctx._source.status = 'ignored'; else if (ctx._source.is_fixed == true) ctx._source.status = 'fixed'; else ctx._source.status = 'open';";
            var stackResponse = await _client.UpdateByQueryAsync<Stack>(x => x.QueryOnQueryString("NOT _exists_:status").Script(script).WaitForCompletion(false));
            _logger.LogTraceRequest(stackResponse, Microsoft.Extensions.Logging.LogLevel.Information);

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

            _logger.LogInformation("Finished adding stack status: Time={Duration:d\\.hh\\:mm} Completed={Completed:N0} Total={Total:N0} Errors={Errors:N0}", sw.Elapsed, stackResponse.Total, stackResponse.Failures.Count);
        }
    }
}