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
            try {
                _stats.Counter(MetricNames.EventsProcessed, contexts.Count);

                if (contexts.First().Organization.PlanId != BillingManager.FreePlan.Id)
                    _stats.Counter(MetricNames.EventsPaidProcessed, contexts.Count);
            } catch (Exception ex) {
                foreach (var context in contexts) {
                    bool cont = false;
                    try {
                        cont = HandleError(ex, context);
                    } catch {}

                    if (!cont)
                        context.SetError(ex.Message, ex);
                }
            }
        }

        public override void Process(EventContext ctx) {}
    }
}