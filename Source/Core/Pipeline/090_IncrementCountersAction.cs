using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.AppStats;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Plugins.EventProcessor;
using Foundatio.Metrics;

namespace Exceptionless.Core.Pipeline {
    [Priority(90)]
    public class IncrementCountersAction : EventPipelineActionBase {
        private readonly IMetricsClient _metricsClient;

        public IncrementCountersAction(IMetricsClient metricsClient) {
            _metricsClient = metricsClient;
        }

        protected override bool ContinueOnError { get { return true; } }

        public override async Task ProcessBatchAsync(ICollection<EventContext> contexts) {
            try {
                await _metricsClient.CounterAsync(MetricNames.EventsProcessed, contexts.Count).AnyContext();

                if (contexts.First().Organization.PlanId != BillingManager.FreePlan.Id)
                    await _metricsClient.CounterAsync(MetricNames.EventsPaidProcessed, contexts.Count).AnyContext();
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

        public override Task ProcessAsync(EventContext ctx) {
            return Task.FromResult(0);
        }
    }
}