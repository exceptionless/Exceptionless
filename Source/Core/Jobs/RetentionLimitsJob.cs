using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Models;
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

        protected override IDisposable GetJobLock() {
            return _lockProvider.AcquireLock("RetentionLimitsJob");
        }

        protected override Task<JobResult> RunInternalAsync(CancellationToken token) {
            var page = 1;
            var organizations = _organizationRepository.GetByRetentionDaysEnabled(new PagingOptions().WithLimit(100));
            while (organizations.Count > 0 && !token.IsCancellationRequested) {
                foreach (var organization in organizations)
                    EnforceEventCountLimits(organization);

                organizations = _organizationRepository.GetByRetentionDaysEnabled(new PagingOptions().WithPage(++page).WithLimit(100));
            }

            return Task.FromResult(JobResult.Success);
        }

        private void EnforceEventCountLimits(Organization organization) {
            Log.Info().Message("Enforcing event count limits for organization '{0}' with Id: '{1}'", organization.Name, organization.Id).Write();

            try {
                // use the next higher plans retention days to enable us to upsell them
                var nextPlan = BillingManager.Plans
                    .Where(p => p.RetentionDays > organization.RetentionDays)
                    .OrderByDescending(p => p.RetentionDays)
                    .FirstOrDefault();

                int retentionDays = organization.RetentionDays;
                if (nextPlan != null)
                    retentionDays = nextPlan.RetentionDays;

                DateTime cutoff = DateTime.UtcNow.Date.AddDays(-retentionDays);
                _eventRepository.RemoveAllByDate(organization.Id, cutoff);
            } catch (Exception ex) {
                Log.Error().Message("Error enforcing limits: org={0} id={1} message=\"{2}\"", organization.Name, organization.Id, ex.Message).Exception(ex).Write();
            }
        }
    }
}