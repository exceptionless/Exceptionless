using System;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.Core.Plugins.EventProcessor;
using Exceptionless.Core.Repositories;

namespace Exceptionless.Core.Pipeline {
    [Priority(60)]
    public class UpdateStatsAction : EventPipelineActionBase {
        private readonly IStackRepository _stackRepository;

        public UpdateStatsAction(IStackRepository stackRepository) {
            _stackRepository = stackRepository;
        }

        protected override bool IsCritical { get { return true; } }

        public override void Process(EventContext ctx) {}

        public override void ProcessBatch(ICollection<EventContext> contexts) {
            var stacks = contexts.Where(c => !c.IsNew).GroupBy(c => c.Event.StackId);
            foreach (var stackGroup in stacks) {
                int count = stackGroup.Count();
                DateTime minDate = stackGroup.Min(s => s.Event.Date.UtcDateTime);
                DateTime maxDate = stackGroup.Max(s => s.Event.Date.UtcDateTime);
                _stackRepository.IncrementEventCounter(stackGroup.First().Event.OrganizationId, stackGroup.First().Event.ProjectId, stackGroup.Key, minDate, maxDate, count);

                // update stacks in memory since they are used in notifications
                foreach (var ctx in stackGroup) {
                    if (ctx.Stack.FirstOccurrence > minDate)
                        ctx.Stack.FirstOccurrence = minDate;
                    if (ctx.Stack.LastOccurrence < maxDate)
                        ctx.Stack.LastOccurrence = maxDate;
                    ctx.Stack.TotalOccurrences += count;
                }
            }
        }
    }
}