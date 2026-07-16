using Exceptionless.Core.Services;
using Foundatio.Jobs;
using Foundatio.Resilience;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Jobs;

/// <summary>
/// Drains V3 stack-usage settlements without a cluster-wide lock. Redis leases partition work
/// across every job instance, and Elasticsearch settlement sequences make retries idempotent.
/// </summary>
[Job(Description = "Apply V3 event occurrence counts to stacks.", InitialDelay = "2s", Interval = "1s")]
public sealed class IngestionStackEventCountJob : JobBase, IHealthCheck
{
    private readonly StackService _stackService;
    private DateTime? _lastRun;

    public IngestionStackEventCountJob(
        StackService stackService,
        TimeProvider timeProvider,
        IResiliencePolicyProvider resiliencePolicyProvider,
        ILoggerFactory loggerFactory)
        : base(timeProvider, resiliencePolicyProvider, loggerFactory)
    {
        _stackService = stackService;
    }

    protected override async Task<JobResult> RunInternalAsync(JobContext context)
    {
        _lastRun = _timeProvider.GetUtcNow().UtcDateTime;
        _logger.LogTrace("Start applying V3 stack event counts");
        await _stackService.SaveIngestionStackUsagesAsync(cancellationToken: context.CancellationToken);
        _logger.LogTrace("Finished applying V3 stack event counts");
        return JobResult.Success;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!_lastRun.HasValue)
        {
            return Task.FromResult(HealthCheckResult.Healthy("Job has not been run yet."));
        }

        if (_timeProvider.GetUtcNow().UtcDateTime.Subtract(_lastRun.Value) > TimeSpan.FromSeconds(15))
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Job has not run in the last 15 seconds."));
        }

        return Task.FromResult(HealthCheckResult.Healthy("Job has run in the last 15 seconds."));
    }
}
