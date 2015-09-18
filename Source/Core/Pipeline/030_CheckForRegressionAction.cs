using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Component;
using Exceptionless.Core.Plugins.EventProcessor;
using Exceptionless.Core.Repositories;
using NLog.Fluent;

namespace Exceptionless.Core.Pipeline {
    [Priority(30)]
    public class CheckForRegressionAction : EventPipelineActionBase {
        private readonly IStackRepository _stackRepository;
        private readonly IEventRepository _eventRepository;

        public CheckForRegressionAction(IStackRepository stackRepository, IEventRepository eventRepository) {
            _stackRepository = stackRepository;
            _eventRepository = eventRepository;
        }

        protected override bool ContinueOnError => true;

        public override async Task ProcessBatchAsync(ICollection<EventContext> contexts) {
            var stacks = contexts.Where(c => c.Stack != null && c.Stack.DateFixed.HasValue && c.Stack.DateFixed.Value < c.Event.Date.UtcDateTime).GroupBy(c => c.Event.StackId);
            foreach (var stackGroup in stacks) {
                try {
                    var context = stackGroup.First();
                    Log.Trace().Message("Marking stack and events as regression.").Write();
                    _stackRepository.MarkAsRegressed(context.Stack.Id);
                    _eventRepository.MarkAsRegressedByStack(context.Event.OrganizationId, context.Stack.Id);

                    _stackRepository.InvalidateCache(context.Event.ProjectId, context.Event.StackId, context.SignatureHash);

                    bool isFirstEvent = true;
                    foreach (var ctx in stackGroup) {
                        ctx.Event.IsFixed = false;

                        // Only mark the first event context as regressed.
                        ctx.IsRegression = isFirstEvent;
                        isFirstEvent = false;
                    }
                } catch (Exception ex) {
                    foreach (var context in stackGroup) {
                        bool cont = false;
                        try {
                            cont = HandleError(ex, context);
                        } catch {}

                        if (!cont)
                            context.SetError(ex.Message, ex);
                    }
                }
            }
        }

        public override Task ProcessAsync(EventContext ctx) {
            return TaskHelper.Completed();
        }
    }
}