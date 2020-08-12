using System.Threading.Tasks;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using Foundatio.Repositories.Elasticsearch.Extensions;
using Foundatio.Repositories.Migrations;
using Microsoft.Extensions.Logging;
using Nest;

namespace Exceptionless.Core.Migrations {
    public sealed class UpdateIndexMappings : MigrationBase {
        private readonly IElasticClient _client;
        private readonly ExceptionlessElasticConfiguration _config;

        public UpdateIndexMappings(ExceptionlessElasticConfiguration configuration, ILoggerFactory loggerFactory) : base(loggerFactory) {
            _config = configuration;
            _client = configuration.Client;

            MigrationType = MigrationType.VersionedAndResumable;
            Version = 1;
        }

        public override async Task RunAsync(MigrationContext context) {
            _logger.LogInformation("Start migration for adding index mappings...");
            var response = await _client.MapAsync<Organization>(d => {
                d.Index(_config.Organizations.VersionedName);
                d.Properties(p => p
                    .Date(f => f.Name(s => s.LastEventDateUtc))
                    .Boolean(f => f.Name(s => s.IsDeleted)).FieldAlias(a => a.Path(p1 => p1.IsDeleted).Name("deleted")));
                    
                return d;
            });
            _logger.LogTraceRequest(response); 
            
            response = await _client.MapAsync<Project>(d => {
                d.Index(_config.Projects.VersionedName);
                d.Properties(p => p
                    .Date(f => f.Name(s => s.LastEventDateUtc))
                    .Boolean(f => f.Name(s => s.IsDeleted)).FieldAlias(a => a.Path(p1 => p1.IsDeleted).Name("deleted")));
                    
                return d;
            });
            _logger.LogTraceRequest(response);
            
            response = await _client.MapAsync<Stack>(d => {
                    d.Index(_config.Stacks.VersionedName);
                    d.Properties(p => p
                        .Keyword(f => f.Name(s => s.Status))
                        .Date(f => f.Name(s => s.SnoozeUntilUtc))
                        .Boolean(f => f.Name(s => s.IsDeleted)).FieldAlias(a => a.Path(p1 => p1.IsDeleted).Name("deleted")));
                    
                return d;
            });
            _logger.LogTraceRequest(response);
            _logger.LogInformation("Finished adding mappings.");
        }
    }
}