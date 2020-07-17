using System.Diagnostics;
using System.Threading.Tasks;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Configuration;
using Foundatio.Repositories.Elasticsearch.Extensions;
using Foundatio.Repositories.Migrations;
using Microsoft.Extensions.Logging;
using Nest;

namespace Exceptionless.Core.Migrations {
    public class SetStackStatus : MigrationBase {
        private readonly IElasticClient _client;
        private readonly ExceptionlessElasticConfiguration _config;

        public SetStackStatus(ExceptionlessElasticConfiguration configuration, ILoggerFactory loggerFactory) : base(loggerFactory) {
            _config = configuration;
            _client = configuration.Client;
        }

        public override int? Version => 2;

        public override async Task RunAsync() {
            _logger.LogInformation("Begin refreshing all indices");
            await _config.Client.Indices.RefreshAsync(Indices.All);
            _logger.LogInformation("Done refreshing all indices");
            
            _logger.LogInformation("Start migrating stacks status");
            var sw = Stopwatch.StartNew();
            const string script = "if (ctx._source.is_regressed == true) ctx._source.status = 'regressed'; else if (ctx._source.is_hidden == true) ctx._source.status = 'ignored'; else if (ctx._source.disable_notifications == true) ctx._source.status = 'ignored'; else if (ctx._source.is_fixed == true) ctx._source.status = 'fixed'; else ctx._source.status = 'open';";
            var stackResponse = await _client.UpdateByQueryAsync<Stack>(x => x.QueryOnQueryString("_missing_:status").Script(script));
            _logger.LogTraceRequest(stackResponse);
            _logger.LogInformation("Finished adding stack status: Time={Duration:d\\.hh\\:mm} Completed={Completed:N0} Total={Total:N0} Errors={Errors:N0}", sw.Elapsed, stackResponse.Total, stackResponse.Failures.Count);
        }
    }
}