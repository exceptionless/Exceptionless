using Exceptionless.Core.Migrations;
using Foundatio.Jobs;
using Foundatio.Resilience;

namespace Exceptionless.Job;

public record RerunMigrationJobOptions(string MigrationId);

[Job(Description = "Reruns an explicitly supported completed migration.", IsContinuous = false)]
public sealed class RerunMigrationJob : JobBase
{
    private readonly MigrationRerunService _migrationRerunService;
    private readonly RerunMigrationJobOptions _options;

    public RerunMigrationJob(
        MigrationRerunService migrationRerunService,
        RerunMigrationJobOptions options,
        TimeProvider timeProvider,
        IResiliencePolicyProvider resiliencePolicyProvider,
        ILoggerFactory loggerFactory)
        : base(timeProvider, resiliencePolicyProvider, loggerFactory)
    {
        _migrationRerunService = migrationRerunService;
        _options = options;
    }

    protected override async Task<JobResult> RunInternalAsync(JobContext context)
    {
        var operation = await _migrationRerunService.QueueAsync(
            _options.MigrationId,
            MigrationRerunSource.CommandLine,
            null,
            context.CancellationToken);
        await _migrationRerunService.RunAsync(operation.Id, context.CancellationToken);
        return JobResult.Success;
    }
}
