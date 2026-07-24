using System.Diagnostics;
using Elastic.Clients.Elasticsearch;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using Foundatio.Repositories.Elasticsearch.Extensions;
using Foundatio.Repositories.Migrations;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Migrations;

public sealed class BackfillParentReferences : MigrationBase
{
    private readonly ElasticsearchClient _client;
    private readonly ExceptionlessElasticConfiguration _config;
    private readonly TimeProvider _timeProvider;

    public BackfillParentReferences(ExceptionlessElasticConfiguration configuration, TimeProvider timeProvider, ILoggerFactory loggerFactory) : base(loggerFactory)
    {
        _config = configuration;
        _client = configuration.Client;
        _timeProvider = timeProvider;

        MigrationType = MigrationType.VersionedAndResumable;
        Version = 3;
    }

    public override async Task RunAsync(MigrationContext context)
    {
        string referenceKey = $"@ref:{Event.KnownReferenceNames.Parent}";
        string indexKey = $"{Event.KnownReferenceNames.Parent}-r";
        string script = $"if (ctx._source.data != null && ctx._source.data.containsKey('{referenceKey}') && ctx._source.data['{referenceKey}'] != null) {{ if (ctx._source.idx == null) ctx._source.idx = [:]; ctx._source.idx['{indexKey}'] = ctx._source.data['{referenceKey}']; }} else {{ ctx.op = 'noop'; }}";

        _logger.LogInformation("Backfilling retained event parent references");
        var stopwatch = Stopwatch.StartNew();
        var response = await _client.UpdateByQueryAsync<PersistentEvent>(request => request
            .Indices($"{_config.Events.VersionedName}-*")
            .Query(query => query.Bool(filter => filter.MustNot(mustNot => mustNot.Exists(exists => exists.Field($"idx.{indexKey}")))))
            .Script(value => value.Source(script).Lang(ScriptLanguage.Painless))
            .Conflicts(Conflicts.Proceed)
            .WaitForCompletion(false));
        _logger.LogRequest(response, LogLevel.Information);

        if (!response.IsValidResponse || response.Task is null)
            throw new ApplicationException($"Unable to start parent-reference backfill: {response.DebugInformation}");

        int attempts = 0;
        while (!context.CancellationToken.IsCancellationRequested)
        {
            var taskStatus = await _client.Tasks.GetAsync(response.Task.FullyQualifiedId, context.CancellationToken);
            if (!taskStatus.IsValidResponse)
                throw new ApplicationException($"Unable to monitor parent-reference backfill: {taskStatus.DebugInformation}");

            if (taskStatus.Completed)
            {
                _logger.LogInformation("Finished parent-reference backfill: Duration={Duration}", stopwatch.Elapsed);
                return;
            }

            attempts++;
            await context.Lock.RenewAsync();
            await Task.Delay(TimeSpan.FromSeconds(attempts <= 5 ? 1 : 5), _timeProvider, context.CancellationToken);
        }

        context.CancellationToken.ThrowIfCancellationRequested();
    }
}
