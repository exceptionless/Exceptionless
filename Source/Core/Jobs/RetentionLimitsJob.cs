using System;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Models;
using Exceptionless.DateTimeExtensions;
using Foundatio.Jobs;
using Foundatio.Lock;
using NLog.Fluent;

namespace Exceptionless.Core.Jobs {
    public class RetentionLimitsJob : JobBase {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IEventRepository _eventRepository;
        private readonly ILockProvider _lockProvider;

        public RetentionLimitsJob(IOrganizationRepository organizationRepository, IEventRepository eventRepository, ILockProvider lockProvider) {
            _organizationRepository = organizationRepository;
            _eventRepository = eventRepository;
            _lockProvider = lockProvider;
        }
        
        protected override Task<IDisposable> GetJobLockAsync() {
            return _lockProvider.AcquireLockAsync("RetentionLimitsJob");
        }

        protected override async Task<JobResult> RunInternalAsync(CancellationToken token) {
            var page = 1;
            var organizations = (await _organizationRepository.GetByRetentionDaysEnabledAsync(new PagingOptions().WithLimit(100)).AnyContext()).Documents;
            while (organizations.Count > 0 && !token.IsCancellationRequested) {
                foreach (var organization in organizations)
                    await EnforceEventCountLimitsAsync(organization).AnyContext();

                organizations = (await _organizationRepository.GetByRetentionDaysEnabledAsync(new PagingOptions().WithPage(++page).WithLimit(100)).AnyContext()).Documents;
            }

            return JobResult.Success;
        }

        private async Task EnforceEventCountLimitsAsync(Organization organization) {
            Log.Info().Message("Enforcing event count limits for organization '{0}' with Id: '{1}'", organization.Name, organization.Id).Write();

            try {
                int retentionDays = organization.RetentionDays;

                var nextPlan = BillingManager.GetBillingPlanByUpsellingRetentionPeriod(organization.RetentionDays);
                if (nextPlan != null)
                    retentionDays = nextPlan.RetentionDays;

                DateTime cutoff = DateTime.UtcNow.Date.SubtractDays(retentionDays);
                await _eventRepository.RemoveAllByDateAsync(organization.Id, cutoff).AnyContext();
            } catch (Exception ex) {
                Log.Error().Message("Error enforcing limits: org={0} id={1} message=\"{2}\"", organization.Name, organization.Id, ex.Message).Exception(ex).Write();
            }
        }
    }
}