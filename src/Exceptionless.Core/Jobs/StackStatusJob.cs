using System;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Repositories;
using Foundatio.Utility;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Jobs {
    [Job(Description = "Update stack statuses", InitialDelay = "10s", Interval = "30s")]
    public class StackStatusJob : JobWithLockBase, IHealthCheck {
        private readonly IStackRepository _stackRepository;
        private readonly ILockProvider _lockProvider;
        private DateTime? _lastRun;

        public StackStatusJob(IStackRepository stackRepository, ICacheClient cacheClient, ILoggerFactory loggerFactory = null) : base(loggerFactory) {
            _stackRepository = stackRepository;
            _lockProvider = new ThrottlingLockProvider(cacheClient, 1, TimeSpan.FromSeconds(10));
        }

        protected override Task<ILock> GetLockAsync(CancellationToken cancellationToken = default) {
            return _lockProvider.AcquireAsync(nameof(StackStatusJob), TimeSpan.FromSeconds(10), new CancellationToken(true));
        }

        protected override async Task<JobResult> RunInternalAsync(JobContext context) {
            const int LIMIT = 100;
            _lastRun = SystemClock.UtcNow;
            _logger.LogTrace("Start save stack event counts.");
            
            // Get list of stacks where snooze has expired
            var results = await _stackRepository.GetExpiredSnoozedStatuses(SystemClock.UtcNow, o => o.PageLimit(LIMIT)).AnyContext();
            while (results.Documents.Count > 0 && !context.CancellationToken.IsCancellationRequested) {
                foreach (var stack in results.Documents) 
                    stack.MarkOpen();

                await _stackRepository.SaveAsync(results.Documents).AnyContext();
                
                // Sleep so we are not hammering the backend.
                await SystemClock.SleepAsync(TimeSpan.FromSeconds(2.5)).AnyContext();

                if (context.CancellationToken.IsCancellationRequested || !await results.NextPageAsync().AnyContext())
                    break;

                if (results.Documents.Count > 0)
                    await context.RenewLockAsync().AnyContext();
            }
            
            _logger.LogTrace("Finished save stack event counts.");
            return JobResult.Success;
        }

        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) {
            if (!_lastRun.HasValue)
                return Task.FromResult(HealthCheckResult.Healthy("Job has not been run yet."));

            if (SystemClock.UtcNow.Subtract(_lastRun.Value) > TimeSpan.FromSeconds(15))
                return Task.FromResult(HealthCheckResult.Unhealthy("Job has not run in the last 15 seconds."));

            return Task.FromResult(HealthCheckResult.Healthy("Job has run in the last 15 seconds."));
        }
    }
}
