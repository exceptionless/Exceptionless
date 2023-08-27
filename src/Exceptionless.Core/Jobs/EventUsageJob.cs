using Exceptionless.Core.Services;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Utility;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Jobs;

[Job(Description = "Saves event usages", IsContinuous = true, Interval = "30s")]
public class EventUsageJob : JobWithLockBase, IHealthCheck
{
    private readonly UsageService _usageService;
    private readonly ILockProvider _lockProvider;
    private DateTime? _lastRun;

    public EventUsageJob(UsageService usageService, ILockProvider lockProvider, ILoggerFactory loggerFactory) : base(loggerFactory)
    {
        _usageService = usageService;
        _lockProvider = lockProvider;
    }

    protected override Task<ILock> GetLockAsync(CancellationToken cancellationToken = default)
    {
        return _lockProvider.AcquireAsync(nameof(EventUsageJob), TimeSpan.FromMinutes(4), new CancellationToken(true));
    }

    protected override async Task<JobResult> RunInternalAsync(JobContext context)
    {
        _lastRun = SystemClock.UtcNow;

        _logger.LogInformation("Saving pending event usage");
        await _usageService.SavePendingUsageAsync();
        _logger.LogInformation("Finished saving pending event usage");

        _lastRun = SystemClock.UtcNow;
        return JobResult.Success;
    }

    private Task RenewLockAsync(JobContext context)
    {
        _lastRun = SystemClock.UtcNow;
        return context.RenewLockAsync();
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!_lastRun.HasValue)
            return Task.FromResult(HealthCheckResult.Healthy("Job has not been run yet."));

        if (SystemClock.UtcNow.Subtract(_lastRun.Value) > TimeSpan.FromMinutes(5))
            return Task.FromResult(HealthCheckResult.Unhealthy("Job has not run in the last 5 minutes."));

        return Task.FromResult(HealthCheckResult.Healthy("Job has run in the last 30 minutes."));
    }
}
