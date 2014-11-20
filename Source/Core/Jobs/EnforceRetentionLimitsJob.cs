#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Lock;
using Exceptionless.Core.Models.Billing;
using Exceptionless.Core.Repositories;
using Exceptionless.Models;
using NLog.Fluent;

namespace Exceptionless.Core.Jobs {
    public class EnforceRetentionLimitsJob : JobBase {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IEventRepository _eventRepository;

        public EnforceRetentionLimitsJob(IOrganizationRepository organizationRepository, IEventRepository eventRepository, ILockProvider lockProvider) {
            _organizationRepository = organizationRepository;
            _eventRepository = eventRepository;
            LockProvider = lockProvider;
        }

        protected override Task<JobResult> RunInternalAsync(CancellationToken token) {
            Log.Info().Message("Enforce retention limits job starting").Write();

            var page = 1;
            var organizations = _organizationRepository.GetByRetentionDaysEnabled(new PagingOptions().WithLimit(100));
            while (organizations.Count > 0) {
                foreach (var organization in organizations)
                    EnforceEventCountLimits(organization);

                organizations = _organizationRepository.GetByRetentionDaysEnabled(new PagingOptions().WithPage(++page).WithLimit(100));
            }

            return Task.FromResult(new JobResult { Message = "Successfully enforced all retention limits." });
        }

        private void EnforceEventCountLimits(Organization organization) {
            Log.Info().Message("Enforcing event count limits for organization '{0}' with Id: '{1}'", organization.Name, organization.Id).Write();

            try {
                // use the next higher plans retention days to enable us to upsell them
                BillingPlan nextPlan = BillingManager.Plans
                    .Where(p => p.RetentionDays > organization.RetentionDays)
                    .OrderByDescending(p => p.RetentionDays)
                    .FirstOrDefault();

                int retentionDays = organization.RetentionDays;
                if (nextPlan != null)
                    retentionDays = nextPlan.RetentionDays;

                DateTime cutoff = DateTime.UtcNow.Date.AddDays(-retentionDays);
                _eventRepository.RemoveAllByDate(organization.Id, cutoff);
            } catch (Exception ex) {
                ex.ToExceptionless().MarkAsCritical().AddTags("Enforce Limits").AddObject(organization).Submit();
            }
        }
    }
}