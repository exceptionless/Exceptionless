using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Plugins.EventProcessor;
using Exceptionless.Core.Repositories;
using Foundatio.Jobs;
using Foundatio.Logging;
using Foundatio.Queues;

namespace Exceptionless.Core.Pipeline {
    [Priority(30)]
    public class CheckForRegressionAction : EventPipelineActionBase {
        private readonly IStackRepository _stackRepository;
        private readonly IQueue<WorkItemData> _workItemQueue;

        public CheckForRegressionAction(IStackRepository stackRepository, IQueue<WorkItemData> workItemQueue, ILoggerFactory loggerFactory = null) : base(loggerFactory) {
            _stackRepository = stackRepository;
            _workItemQueue = workItemQueue;
            ContinueOnError = true;
        }

        public override async Task ProcessBatchAsync(ICollection<EventContext> contexts) {
            var stacks = contexts.Where(c => c.Stack?.DateFixed != null && c.Stack.DateFixed.Value < c.Event.Date.UtcDateTime).GroupBy(c => c.Event.StackId);
            foreach (var stackGroup in stacks) {
                try {
                    var context = stackGroup.First();
                    _logger.Trace().Message("Marking stack and events as regression.").Write();
                    await _stackRepository.MarkAsRegressedAsync(context.Stack.Id).AnyContext();
                    await _workItemQueue.EnqueueAsync(new StackWorkItem { OrganizationId = context.Event.OrganizationId, StackId = context.Stack.Id, UpdateIsFixed = true, IsFixed = false }).AnyContext();
                    await _stackRepository.InvalidateCacheAsync(context.Event.ProjectId, context.Event.StackId, context.SignatureHash).AnyContext();

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
            return Task.CompletedTask;
        }
    }
}