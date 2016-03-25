﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Models;
using Exceptionless.DateTimeExtensions;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Logging;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Jobs {
    public class RetentionLimitsJob : JobWithLockBase {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IEventRepository _eventRepository;
        private readonly ILockProvider _lockProvider;

        public RetentionLimitsJob(IOrganizationRepository organizationRepository, IEventRepository eventRepository, ICacheClient cacheClient, ILoggerFactory loggerFactory = null) : base(loggerFactory) {
            _organizationRepository = organizationRepository;
            _eventRepository = eventRepository;
            _lockProvider = new ThrottlingLockProvider(cacheClient, 1, TimeSpan.FromDays(1));
        }

        protected override Task<ILock> GetLockAsync(CancellationToken cancellationToken = default(CancellationToken)) {
            return _lockProvider.AcquireAsync(nameof(RetentionLimitsJob), TimeSpan.FromHours(2), new CancellationToken(true));
        }

        protected override async Task<JobResult> RunInternalAsync(JobContext context) {
            var results = await _organizationRepository.GetByRetentionDaysEnabledAsync(new PagingOptions().WithPage(1).WithLimit(100)).AnyContext();
            while (results.Documents.Count > 0 && !context.CancellationToken.IsCancellationRequested) {
                foreach (var organization in results.Documents) {
                    await EnforceEventCountLimitsAsync(organization).AnyContext();

                    // Sleep so we are not hammering the backend.
                    await Task.Delay(TimeSpan.FromSeconds(5)).AnyContext();
                }

                await results.NextPageAsync().AnyContext();
                if (results.Documents.Count > 0)
                    await context.RenewLockAsync().AnyContext();
            }

            return JobResult.Success;
        }

        private async Task EnforceEventCountLimitsAsync(Organization organization) {
            _logger.Info("Enforcing event count limits for organization '{0}' with Id: '{1}'", organization.Name, organization.Id);

            try {
                int retentionDays = organization.RetentionDays;

                var nextPlan = BillingManager.GetBillingPlanByUpsellingRetentionPeriod(organization.RetentionDays);
                if (nextPlan != null)
                    retentionDays = nextPlan.RetentionDays;

                DateTime cutoff = DateTime.UtcNow.Date.SubtractDays(retentionDays);
                await _eventRepository.RemoveAllByDateAsync(organization.Id, cutoff).AnyContext();
            } catch (Exception ex) {
                _logger.Error(ex, "Error enforcing limits: org={0} id={1} message=\"{2}\"", organization.Name, organization.Id, ex.Message);
            }
        }
    }
}
