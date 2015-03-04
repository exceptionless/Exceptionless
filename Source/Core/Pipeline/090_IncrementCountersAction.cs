using System;
using System.Collections.Generic;
using System.Linq;
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

        public override void ProcessBatch(ICollection<EventContext> contexts) {
            _stats.Counter(MetricNames.EventsProcessed, contexts.Count);

            if (contexts.First().Organization.PlanId != BillingManager.FreePlan.Id)
                _stats.Counter(MetricNames.EventsPaidProcessed, contexts.Count);
        }

        public override void Process(EventContext ctx) {}
    }
}