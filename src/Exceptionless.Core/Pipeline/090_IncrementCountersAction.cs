using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.AppStats;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Plugins.EventProcessor;
using Foundatio.Metrics;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Pipeline {
    [Priority(90)]
    public class IncrementCountersAction : EventPipelineActionBase {
        private readonly IMetricsClient _metricsClient;
        private readonly BillingPlans _plans;

        public IncrementCountersAction(IMetricsClient metricsClient, BillingPlans plans, AppOptions options, ILoggerFactory loggerFactory = null) : base(options, loggerFactory) {
            _metricsClient = metricsClient;
            _plans = plans;
            ContinueOnError = true;
        }

        public override Task ProcessBatchAsync(ICollection<EventContext> contexts) {
            try {
                _metricsClient.Counter(MetricNames.EventsProcessed, contexts.Count);

                if (contexts.First().Organization.PlanId != _plans.FreePlan.Id)
                    _metricsClient.Counter(MetricNames.EventsPaidProcessed, contexts.Count);
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

            return Task.CompletedTask;
        }

        public override Task ProcessAsync(EventContext ctx) {
            return Task.CompletedTask;
        }
    }
}