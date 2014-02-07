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
using CodeSmith.Core.Scheduler;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Models.Billing;
using Exceptionless.Models;
using MongoDB.Driver.Builders;
using NLog.Fluent;

namespace Exceptionless.Core.Jobs {
    public class EnforceRetentionLimitsJob : JobBase {
        private readonly OrganizationRepository _organizationRepository;
        private readonly ErrorRepository _errorRepository;

        public EnforceRetentionLimitsJob(OrganizationRepository organizationRepository, ErrorRepository errorRepository) {
            _organizationRepository = organizationRepository;
            _errorRepository = errorRepository;
        }

        public override JobResult Run(JobContext context) {
            Log.Info().Message("Enforce retention limits job starting").Write();

            int skip = 0;
            var organizations = _organizationRepository.Collection.FindAs<Organization>(Query.Null)
                .SetFields(OrganizationRepository.FieldNames.Id, OrganizationRepository.FieldNames.Name, OrganizationRepository.FieldNames.RetentionDays)
                .SetLimit(100).SetSkip(skip).ToList();

            while (organizations.Count > 0) {
                // TODO: Need to add overage days to the org when they went over their limit for the day.
                foreach (var organization in organizations)
                    EnforceErrorCountLimits(organization);

                skip += 100;
                organizations = _organizationRepository.Collection.FindAs<Organization>(Query.Null)
                    .SetFields(OrganizationRepository.FieldNames.Id, OrganizationRepository.FieldNames.Name, OrganizationRepository.FieldNames.RetentionDays)
                    .SetLimit(100).SetSkip(skip).ToList();
            }

            return new JobResult {
                Result = "Successfully enforced all retention limits."
            };
        }

        private void EnforceErrorCountLimits(Organization organization) {
            if (organization.RetentionDays <= 0)
                return;

            Log.Info().Message("Enforcing error count limits for organization '{0}' with Id: '{1}'", organization.Name, organization.Id).Write();

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
                _errorRepository.RemoveAllByDate(organization.Id, cutoff);
            } catch (Exception ex) {
                ex.ToExceptionless().MarkAsCritical().AddTags("Enforce Limits").AddObject(organization).Submit();
            }
        }
    }
}