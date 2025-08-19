using Exceptionless.Core.Services;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Resilience;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Jobs;

[Job(Description = "Update event occurrence count for stacks.", InitialDelay = "2s", Interval = "5s")]
public class StackEventCountJob : JobWithLockBase, IHealthCheck
{
    private readonly StackService _stackService;
    private readonly ILockProvider _lockProvider;
    private DateTime? _lastRun;

    public StackEventCountJob(StackService stackService, ICacheClient cacheClient,
        TimeProvider timeProvider,
        IResiliencePolicyProvider resiliencePolicyProvider,
        ILoggerFactory loggerFactory
    ) : base(timeProvider, resiliencePolicyProvider, loggerFactory)
    {
        _stackService = stackService;
        _lockProvider = new ThrottlingLockProvider(cacheClient, 1, TimeSpan.FromSeconds(5), timeProvider, resiliencePolicyProvider, loggerFactory);
    }

    protected override Task<ILock> GetLockAsync(CancellationToken cancellationToken = default)
    {
        return _lockProvider.AcquireAsync(nameof(StackEventCountJob), TimeSpan.FromSeconds(5), new CancellationToken(true));
    }

    protected override async Task<JobResult> RunInternalAsync(JobContext context)
    {
        _lastRun = _timeProvider.GetUtcNow().UtcDateTime;
        _logger.LogTrace("Start save stack event counts");
        await _stackService.SaveStackUsagesAsync(cancellationToken: context.CancellationToken);
        _logger.LogTrace("Finished save stack event counts");
        return JobResult.Success;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!_lastRun.HasValue)
            return Task.FromResult(HealthCheckResult.Healthy("Job has not been run yet."));

        if (_timeProvider.GetUtcNow().UtcDateTime.Subtract(_lastRun.Value) > TimeSpan.FromSeconds(15))
            return Task.FromResult(HealthCheckResult.Unhealthy("Job has not run in the last 15 seconds."));

        return Task.FromResult(HealthCheckResult.Healthy("Job has run in the last 15 seconds."));
    }
}
