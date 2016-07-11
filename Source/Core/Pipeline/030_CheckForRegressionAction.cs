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
using McSherry.SemanticVersioning;

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
            var stacks = contexts.Where(c => !c.Stack.IsRegressed && c.Stack.FixedInVersion != null).OrderBy(c => c.Event.Date).GroupBy(c => c.Event.StackId);
            foreach (var stackGroup in stacks) {
                try {
                    var stack = stackGroup.First().Stack;
                    var fixedInVersion = GetSemanticVersion(stack.FixedInVersion);

                    SemanticVersion regressedVersion = null;
                    EventContext regressedContext = null;
                    // Group all stack events by version.
                    var versions = stackGroup.GroupBy(c => c.Event.GetVersion());
                    foreach (var versionGroup in versions) {
                        var version = GetSemanticVersion(versionGroup.Key);
                        if (version == null || version <= fixedInVersion)
                            continue;

                        if (regressedVersion != null && regressedVersion >= version)
                            continue;

                        regressedVersion = version;
                        regressedContext = versionGroup.First();
                    }

                    if (regressedVersion == null)
                        return;

                    _logger.Trace("Marking stack and events as regressed in version: {version}", regressedVersion);
                    await _stackRepository.MarkAsRegressedAsync(stack.Id).AnyContext();
                    await _workItemQueue.EnqueueAsync(new StackWorkItem { OrganizationId = stack.OrganizationId, StackId = stack.Id, UpdateIsFixed = true, IsFixed = false }).AnyContext();
                    
                    foreach (var ctx in stackGroup) {
                        ctx.Event.IsFixed = false;
                        ctx.IsRegression = false;
                    }
                    
                    regressedContext.IsRegression = true;
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
        
        private SemanticVersion GetSemanticVersion(string version) {
            if (String.IsNullOrEmpty(version))
                return null;

            SemanticVersion semanticVersion;
            if (SemanticVersion.TryParse(version, out semanticVersion))
                return semanticVersion;

            return null;
        }
    }
}