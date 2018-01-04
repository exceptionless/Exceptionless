using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Plugins.EventProcessor;
using Exceptionless.Core.Services;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Pipeline {
    [Priority(60)]
    public class UpdateStatsAction : EventPipelineActionBase {
        private readonly StackService _stackService;

        public UpdateStatsAction(StackService stackService, ILoggerFactory loggerFactory = null) : base(loggerFactory) {
            _stackService = stackService;
        }

        protected override bool IsCritical => true;

        public override Task ProcessAsync(EventContext ctx) {
            return Task.CompletedTask;
        }

        public override async Task ProcessBatchAsync(ICollection<EventContext> contexts) {
            var stacks = contexts.Where(c => !c.IsNew).GroupBy(c => c.Event.StackId);

            foreach (var stackGroup in stacks)
                await IncrementEventCountersAsync(stackGroup).AnyContext();
        }

        private async Task IncrementEventCountersAsync(IGrouping<string, EventContext> stackGroup) {
            var stackContexts = stackGroup.ToList();

            try {
                int count = stackContexts.Count;
                DateTime minDate = stackContexts.Min(s => s.Event.Date.UtcDateTime);
                DateTime maxDate = stackContexts.Max(s => s.Event.Date.UtcDateTime);
                await _stackService.IncrementStackUsageAsync(stackContexts[0].Event.OrganizationId, stackContexts[0].Event.ProjectId, stackGroup.Key, minDate, maxDate, count).AnyContext();

                // Update stacks in memory since they are used in notifications.
                foreach (var ctx in stackContexts) {
                    if (ctx.Stack.FirstOccurrence > minDate)
                        ctx.Stack.FirstOccurrence = minDate;

                    if (ctx.Stack.LastOccurrence < maxDate)
                        ctx.Stack.LastOccurrence = maxDate;

                    ctx.Stack.TotalOccurrences += count;
                }
            } catch (Exception ex) {
                foreach (var context in stackContexts) {
                    bool cont = false;
                    try {
                        cont = HandleError(ex, context);
                    } catch { }

                    if (!cont)
                        context.SetError(ex.Message, ex);
                }
            }
        }
    }
}