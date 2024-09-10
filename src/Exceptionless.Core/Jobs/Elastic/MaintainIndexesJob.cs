using Exceptionless.Core.Repositories.Configuration;
using Foundatio.Jobs;
using Foundatio.Lock;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Jobs.Elastic;

[Job(Description = "Maintains Elasticsearch index aliases and index retention", IsContinuous = false)]
public class MaintainIndexesJob : Foundatio.Repositories.Elasticsearch.Jobs.MaintainIndexesJob, IHealthCheck
{
    private readonly TimeProvider _timeProvider;
    private DateTime? _lastRun;

    public MaintainIndexesJob(ExceptionlessElasticConfiguration configuration, ILockProvider lockProvider, TimeProvider timeProvider, ILoggerFactory loggerFactory) : base(configuration, lockProvider, loggerFactory)
    {
        _timeProvider = timeProvider;
    }

    public override Task<JobResult> RunAsync(CancellationToken cancellationToken = new())
    {
        _lastRun = _timeProvider.GetUtcNow().UtcDateTime;
        return base.RunAsync(cancellationToken);
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!_lastRun.HasValue)
            return Task.FromResult(HealthCheckResult.Healthy("Job has not been run yet."));

        if (_timeProvider.GetUtcNow().UtcDateTime.Subtract(_lastRun.Value) > TimeSpan.FromMinutes(65))
            return Task.FromResult(HealthCheckResult.Unhealthy("Job has not run in the last 65 minutes."));

        return Task.FromResult(HealthCheckResult.Healthy("Job has run in the last 65 minutes."));
    }
}
