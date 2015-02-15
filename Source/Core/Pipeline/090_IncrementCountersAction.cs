#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using Exceptionless.Core.AppStats;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Plugins.EventProcessor;
using Foundatio.Metrics;

namespace Exceptionless.Core.Pipeline {
    [Priority(90)]
    public class IncrementCountersAction : EventPipelineActionBase {
        private readonly IMetricsClient _stats;

        public IncrementCountersAction(IMetricsClient stats) {
            _stats = stats;
        }

        protected override bool ContinueOnError { get { return true; } }

        public override void Process(EventContext ctx) {
            _stats.Counter(MetricNames.EventsProcessed);
            if (ctx.Organization.PlanId != BillingManager.FreePlan.Id)
                _stats.Counter(MetricNames.EventsPaidProcessed);
        }
    }
}