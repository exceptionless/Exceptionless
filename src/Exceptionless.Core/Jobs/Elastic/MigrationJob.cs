using Exceptionless.Core.Repositories.Configuration;
using Foundatio.Jobs;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Foundatio.Repositories.Migrations;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Jobs.Elastic;

[Job(Description = "Runs any pending document migrations.", IsContinuous = false)]
public class MigrationJob : JobBase
{
    private readonly MigrationManager _migrationManager;
    private readonly ExceptionlessElasticConfiguration _configuration;

    public MigrationJob(MigrationManager migrationManager, ExceptionlessElasticConfiguration configuration, TimeProvider timeProvider, ILoggerFactory loggerFactory)
        : base(timeProvider, loggerFactory)
    {

        _migrationManager = migrationManager;
        _configuration = configuration;
    }

    protected override async Task<JobResult> RunInternalAsync(JobContext context)
    {
        await _configuration.ConfigureIndexesAsync(null, false);
        await _migrationManager.RunMigrationsAsync();

        var tasks = _configuration.Indexes.OfType<VersionedIndex>().Select(ReindexIfNecessary);
        await Task.WhenAll(tasks);

        return JobResult.Success;
    }

    private async Task ReindexIfNecessary(VersionedIndex index)
    {
        if (index.Version != await index.GetCurrentVersionAsync())
            await index.ReindexAsync();
    }
}
