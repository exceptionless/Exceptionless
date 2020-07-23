using System;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.DateTimeExtensions;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Repositories;
using Foundatio.Utility;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

#pragma warning disable 1998

namespace Exceptionless.Core.Jobs {
    [Job(Description = "Deletes soft deleted data and retention data.", InitialDelay = "15m", Interval = "1h")]
    public class CleanupDataJob : JobWithLockBase, IHealthCheck {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly AppOptions _appOptions;
        private readonly ILockProvider _lockProvider;
        private DateTime? _lastRun;

        public CleanupDataJob(
            IOrganizationRepository organizationRepository,
            ICacheClient cacheClient, 
            AppOptions appOptions,
            ILoggerFactory loggerFactory = null
        ) : base(loggerFactory) {
            _organizationRepository = organizationRepository;
            _appOptions = appOptions;
            _lockProvider = new ThrottlingLockProvider(cacheClient, 1, TimeSpan.FromDays(1));
        }

        protected override Task<ILock> GetLockAsync(CancellationToken cancellationToken = default) {
            return _lockProvider.AcquireAsync(nameof(StackEventCountJob), TimeSpan.FromHours(2), new CancellationToken(true));
        }

        protected override async Task<JobResult> RunInternalAsync(JobContext context) {
            _lastRun = SystemClock.UtcNow;
            _logger.LogTrace("Processing event deletion: id={0}", context.QueueEntry.Id);

            var ed = context.QueueEntry.Value;
            try {
                long removed = await _eventRepository.RemoveAllAsync(ed.OrganizationIds, ed.ProjectIds, ed.StackIds, ed.EventIds, ed.ClientIpAddress, ed.UtcStartDate, ed.UtcEndDate).AnyContext();
                _logger.LogInformation("Processed event deletion: id={Id}, removed={Removed}", context.QueueEntry.Id, removed);
            } catch (Exception ex) {
                _logger.LogError(ex, "Error processing event deletion {Id}: {Message}", context.QueueEntry.Id, ex.Message);
                return JobResult.FromException(ex);
            }

            _lastRun = SystemClock.UtcNow;
            var results = await _organizationRepository.GetByRetentionDaysEnabledAsync(o => o.SnapshotPaging().PageLimit(100)).AnyContext();
            while (results.Documents.Count > 0 && !context.CancellationToken.IsCancellationRequested) {
                foreach (var organization in results.Documents) {
                    using (_logger.BeginScope(new ExceptionlessState().Organization(organization.Id))) {
                        await EnforceEventCountLimitsAsync(organization).AnyContext();

                        // Sleep so we are not hammering the backend.
                        await SystemClock.SleepAsync(TimeSpan.FromSeconds(5)).AnyContext();
                    }
                }

                if (context.CancellationToken.IsCancellationRequested || !await results.NextPageAsync().AnyContext())
                    break;

                if (results.Documents.Count > 0) {
                    await context.RenewLockAsync().AnyContext();
                    _lastRun = SystemClock.UtcNow;
                }
            }

            return JobResult.Success;
        }
        
        private async Task EnforceEventCountLimitsAsync(Organization organization) {
            try {
                int retentionDays = organization.RetentionDays;
                if (_appOptions.MaximumRetentionDays > 0 && retentionDays > _appOptions.MaximumRetentionDays)
                    retentionDays = _appOptions.MaximumRetentionDays;

                var cutoff = SystemClock.UtcNow.Date.SubtractDays(retentionDays);
                _logger.LogInformation("Enforcing event count limits older than {RetentionPeriod:g} for organization {OrganizationName} ({OrganizationId}).", cutoff, organization.Name, organization.Id);
                
                //await _eventDeletionQueue.EnqueueAsync(new EventDeletion {
                //    OrganizationIds = new []{ organization.Id },
                //    UtcEndDate = cutoff
                //});
            } catch (Exception ex) {
                _logger.LogError(ex, "Error enforcing limits: org={OrganizationName} id={organization} message={Message}", organization.Name, organization.Id, ex.Message);
            }
        }
        
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) {
            if (!_lastRun.HasValue)
                return Task.FromResult(HealthCheckResult.Healthy("Job has not been run yet."));

            if (SystemClock.UtcNow.Subtract(_lastRun.Value) > TimeSpan.FromMinutes(65))
                return Task.FromResult(HealthCheckResult.Unhealthy("Job has not run in the last 65 minutes."));

            return Task.FromResult(HealthCheckResult.Healthy("Job has run in the last 65 minutes."));
        }
    }
}