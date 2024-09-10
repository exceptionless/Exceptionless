using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Repositories;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Jobs;

[Job(Description = "Update stack statuses", InitialDelay = "10s", Interval = "30s")]
public class StackStatusJob : JobWithLockBase, IHealthCheck
{
    private readonly IStackRepository _stackRepository;
    private readonly TimeProvider _timeProvider;
    private readonly ILockProvider _lockProvider;
    private DateTime? _lastRun;

    public StackStatusJob(IStackRepository stackRepository, ICacheClient cacheClient, TimeProvider timeProvider, ILoggerFactory loggerFactory) : base(loggerFactory)
    {
        _stackRepository = stackRepository;
        _timeProvider = timeProvider;
        _lockProvider = new ThrottlingLockProvider(cacheClient, 1, TimeSpan.FromSeconds(10));
    }

    protected override Task<ILock> GetLockAsync(CancellationToken cancellationToken = default)
    {
        return _lockProvider.AcquireAsync(nameof(StackStatusJob), TimeSpan.FromSeconds(10), new CancellationToken(true));
    }

    protected override async Task<JobResult> RunInternalAsync(JobContext context)
    {
        const int LIMIT = 100;
        _lastRun = _timeProvider.GetUtcNow().UtcDateTime;
        _logger.LogTrace("Start save stack event counts");

        // Get list of stacks where snooze has expired
        var results = await _stackRepository.GetExpiredSnoozedStatuses(_timeProvider.GetUtcNow().UtcDateTime, o => o.PageLimit(LIMIT));
        while (results.Documents.Count > 0 && !context.CancellationToken.IsCancellationRequested)
        {
            foreach (var stack in results.Documents)
                stack.MarkOpen();

            await _stackRepository.SaveAsync(results.Documents);

            // Sleep so we are not hammering the backend.
            await Task.Delay(TimeSpan.FromSeconds(2.5));

            if (context.CancellationToken.IsCancellationRequested || !await results.NextPageAsync())
                break;

            if (results.Documents.Count > 0)
                await context.RenewLockAsync();
        }

        _logger.LogTrace("Finished save stack event counts");
        return JobResult.Success;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!_lastRun.HasValue)
            return Task.FromResult(HealthCheckResult.Healthy("Job has not been run yet."));

        if (_timeProvider.GetUtcNow().UtcDateTime.Subtract(_lastRun.Value) > TimeSpan.FromMinutes(1))
            return Task.FromResult(HealthCheckResult.Unhealthy("Job has not run in the last minute."));

        return Task.FromResult(HealthCheckResult.Healthy("Job has run in the last minute."));
    }
}
