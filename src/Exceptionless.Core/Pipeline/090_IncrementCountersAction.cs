using Exceptionless.Core.AppStats;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Plugins.EventProcessor;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Pipeline;

[Priority(90)]
public class IncrementCountersAction : EventPipelineActionBase {
    private readonly BillingPlans _plans;

    public IncrementCountersAction(BillingPlans plans, AppOptions options, ILoggerFactory loggerFactory = null) : base(options, loggerFactory) {
        _plans = plans;
        ContinueOnError = true;
    }

    public override Task ProcessBatchAsync(ICollection<EventContext> contexts) {
        try {
            AppDiagnostics.EventsProcessed.Add(contexts.Count);

            if (contexts.First().Organization.PlanId != _plans.FreePlan.Id)
                AppDiagnostics.EventsPaidProcessed.Add(contexts.Count);
        }
        catch (Exception ex) {
            foreach (var context in contexts) {
                bool cont = false;
                try {
                    cont = HandleError(ex, context);
                }
                catch { }

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
