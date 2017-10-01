using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Plugins.EventProcessor;
using Foundatio.Jobs;
using Foundatio.Queues;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Pipeline {
    [Priority(50)]
    public class MarkProjectConfiguredAction : EventPipelineActionBase {
        private readonly IQueue<WorkItemData> _workItemQueue;

        public MarkProjectConfiguredAction(IQueue<WorkItemData> workItemQueue, ILoggerFactory loggerFactory = null) : base(loggerFactory) {
            _workItemQueue = workItemQueue;
            ContinueOnError = true;
        }
        
        public override async Task ProcessBatchAsync(ICollection<EventContext> contexts) {
            var projectIds = contexts.Where(c => !c.Project.IsConfigured.GetValueOrDefault()).Select(c => c.Project.Id).Distinct().ToList();
            if (projectIds.Count == 0)
                return;
            
            try {
                foreach (var projectId in projectIds) {
                    await _workItemQueue.EnqueueAsync(new SetProjectIsConfiguredWorkItem {
                        ProjectId = projectId,
                        IsConfigured = true
                    }).AnyContext();
                }
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
            return Task.CompletedTask;
        }
    }
}