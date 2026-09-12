using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using Foundatio.Repositories.Elasticsearch.Extensions;
using Foundatio.Repositories.Migrations;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Migrations;

public sealed class AddEventEnvironment : MigrationBase
{
    private readonly ExceptionlessElasticConfiguration _configuration;

    public AddEventEnvironment(ExceptionlessElasticConfiguration configuration, ILoggerFactory loggerFactory) : base(loggerFactory)
    {
        _configuration = configuration;
        MigrationType = MigrationType.VersionedAndResumable;
        Version = 10;
    }

    public override async Task RunAsync(MigrationContext context)
    {
        var response = await _configuration.Client.Indices.PutMappingAsync<PersistentEvent>(mapping => mapping
            .Indices($"{_configuration.Events.Name}-v*-*")
            .AllowNoIndices(true)
            .IgnoreUnavailable(true)
            .Properties(properties => properties.Text(ev => ev.Environment,
                text => text.Analyzer(EventIndex.LOWER_KEYWORD_ANALYZER)
                    .Fields(fields => fields.Keyword("keyword", keyword => keyword.Normalizer("lowercase"))))), context.CancellationToken);
        _logger.LogRequest(response);
        if (!response.IsValidResponse)
        {
            throw new InvalidOperationException("Unable to add the event environment mapping: " + response.DebugInformation);
        }
    }
}
