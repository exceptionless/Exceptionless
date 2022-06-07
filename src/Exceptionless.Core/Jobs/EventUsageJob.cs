using System.IO.Compression;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Services;
using Exceptionless.DateTimeExtensions;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Storage;
using Foundatio.Utility;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Jobs;

[Job(Description = "Saves Event Usages", IsContinuous = false)]
public class EventUsageJob : JobWithLockBase, IHealthCheck {
    private readonly UsageService _usageService;
    private readonly ILockProvider _lockProvider;
    private DateTime? _lastRun;

    public EventUsageJob(UsageService usageService, ICacheClient cacheClient, ILoggerFactory loggerFactory = null) : base(loggerFactory) {
        _usageService = usageService;
        _lockProvider = new ThrottlingLockProvider(cacheClient, 1, TimeSpan.FromMinutes(4));
    }

    protected override Task<ILock> GetLockAsync(CancellationToken cancellationToken = default) {
        return _lockProvider.AcquireAsync(nameof(EventUsageJob), TimeSpan.FromMinutes(4), new CancellationToken(true));
    }

    protected override async Task<JobResult> RunInternalAsync(JobContext context) {
        _lastRun = SystemClock.UtcNow;

        _logger.LogInformation("Saving pending organization usage info");
        await _usageService.SavePendingOrganizationUsageInfo();

        await RenewLockAsync(context);

        _logger.LogInformation("Saving pending project usage info");
        await _usageService.SavePendingProjectUsageInfo();

        _logger.LogInformation("Finished saving pending usage info");

        _lastRun = SystemClock.UtcNow;
        return JobResult.Success;
    }

    private Task RenewLockAsync(JobContext context) {
        _lastRun = SystemClock.UtcNow;
        return context.RenewLockAsync();
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) {
        if (!_lastRun.HasValue)
            return Task.FromResult(HealthCheckResult.Healthy("Job has not been run yet."));

        if (SystemClock.UtcNow.Subtract(_lastRun.Value) > TimeSpan.FromMinutes(30))
            return Task.FromResult(HealthCheckResult.Unhealthy("Job has not run in the last 30 minutes."));

        return Task.FromResult(HealthCheckResult.Healthy("Job has run in the last 30 minutes."));
    }
}
