using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using Foundatio.Repositories.Elasticsearch.Extensions;
using Foundatio.Repositories.Migrations;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Migrations;

/// <summary>Adds native telemetry mappings to retained event partitions without reindexing.</summary>
public sealed class AddEventTelemetryMappings(ExceptionlessElasticConfiguration configuration, ILoggerFactory loggerFactory) : MigrationBase(loggerFactory)
{
    public override int? Version => 10;

    public override async Task RunAsync(MigrationContext context)
    {
        var response = await configuration.Client.Indices.PutMappingAsync<PersistentEvent>(d => d
            .Indices($"{configuration.Events.Name}-v*-*")
            .AllowNoIndices(true)
            .IgnoreUnavailable(true)
            .Properties(p => p.AddTelemetry()), context.CancellationToken);
        _logger.LogRequest(response);
        if (!response.IsValidResponse)
            throw new InvalidOperationException("Unable to add event telemetry mappings: " + response.DebugInformation);
    }
}
