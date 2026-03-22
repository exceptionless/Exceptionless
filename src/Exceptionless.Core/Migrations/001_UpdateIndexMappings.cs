using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using Foundatio.Repositories.Elasticsearch.Extensions;
using Foundatio.Repositories.Migrations;
using Microsoft.Extensions.Logging;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Mapping;

namespace Exceptionless.Core.Migrations;

public sealed class UpdateIndexMappings : MigrationBase
{
    private readonly ElasticsearchClient _client;
    private readonly ExceptionlessElasticConfiguration _config;

    public UpdateIndexMappings(ExceptionlessElasticConfiguration configuration, ILoggerFactory loggerFactory) : base(loggerFactory)
    {
        _config = configuration;
        _client = configuration.Client;

        MigrationType = MigrationType.VersionedAndResumable;
        Version = 1;
    }

    public override async Task RunAsync(MigrationContext context)
    {
        _logger.LogInformation("Start migration for adding index mappings...");

        _logger.LogInformation("Updating Organization mappings...");
        var response = await _client.Indices.PutMappingAsync<Organization>(d =>
        {
            d.Indices(_config.Organizations.VersionedName);
            d.Properties(p => p
                .Date(s => s.LastEventDateUtc)
                .Boolean(s => s.IsDeleted)
                .FieldAlias("deleted", new FieldAliasProperty { Path = "is_deleted" }));
        });
        _logger.LogRequest(response);

        _logger.LogInformation("Setting Organization is_deleted=false...");
        const string script = "ctx._source.is_deleted = false;";
        await _config.Client.Indices.RefreshAsync(_config.Organizations.VersionedName);
        var updateResponse = await _client.UpdateByQueryAsync<Organization>(x => x.Query(q => q.QueryString(qs => qs.Query("NOT _exists_:deleted"))).Script(s => s.Source(script).Lang(ScriptLanguage.Painless)));
        _logger.LogRequest(updateResponse);

        _logger.LogInformation("Updating Project mappings...");
        response = await _client.Indices.PutMappingAsync<Project>(d =>
        {
            d.Indices(_config.Projects.VersionedName);
            d.Properties(p => p
                .Date(s => s.LastEventDateUtc)
                .Boolean(s => s.IsDeleted)
                .FieldAlias("deleted", new FieldAliasProperty { Path = "is_deleted" }));
        });
        _logger.LogRequest(response);

        _logger.LogInformation("Setting Project is_deleted=false...");
        await _config.Client.Indices.RefreshAsync(_config.Projects.VersionedName);
        updateResponse = await _client.UpdateByQueryAsync<Project>(x => x.Query(q => q.QueryString(qs => qs.Query("NOT _exists_:deleted"))).Script(s => s.Source(script).Lang(ScriptLanguage.Painless)));
        _logger.LogRequest(updateResponse);

        _logger.LogInformation("Updating Stack mappings...");
        response = await _client.Indices.PutMappingAsync<Stack>(d =>
        {
            d.Indices(_config.Stacks.VersionedName);
            d.Properties(p => p
                .Keyword(s => s.Status)
                .Date(s => s.SnoozeUntilUtc)
                .Boolean(s => s.IsDeleted)
                .FieldAlias("deleted", new FieldAliasProperty { Path = "is_deleted" }));
        });
        _logger.LogRequest(response);

        _logger.LogInformation("Setting Stack is_deleted=false...");
        await _config.Client.Indices.RefreshAsync(_config.Stacks.VersionedName);
        updateResponse = await _client.UpdateByQueryAsync<Stack>(x => x.Query(q => q.QueryString(qs => qs.Query("NOT _exists_:deleted"))).Script(s => s.Source(script).Lang(ScriptLanguage.Painless)));
        _logger.LogRequest(updateResponse);

        _logger.LogInformation("Finished adding mappings.");
    }
}
