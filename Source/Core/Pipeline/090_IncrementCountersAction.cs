#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using CodeSmith.Core.Component;
using Exceptionless.Core.AppStats;
using Exceptionless.Core.Billing;
using Exceptionless.Core.EventPlugins;

namespace Exceptionless.Core.Pipeline {
    [Priority(90)]
    public class IncrementCountersAction : EventPipelineActionBase {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IAppStatsClient _stats;

        public IncrementCountersAction(IOrganizationRepository organizationRepository, IAppStatsClient stats) {
            _organizationRepository = organizationRepository;
            _stats = stats;
        }

        protected override bool ContinueOnError { get { return true; } }

        public override void Process(EventContext ctx) {
            var organization = _organizationRepository.GetByIdCached(ctx.Event.OrganizationId);

            if (organization == null)
                return;

            _stats.Counter(StatNames.ErrorsProcessed);
            if (organization.PlanId != BillingManager.FreePlan.Id)
                _stats.Counter(StatNames.ErrorsPaidProcessed);
        }
    }
}