using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Plugins.EventProcessor;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Foundatio.Jobs;
using Foundatio.Queues;
using McSherry.SemanticVersioning;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Pipeline {
    [Priority(30)]
    public class CheckForRegressionAction : EventPipelineActionBase {
        private readonly IStackRepository _stackRepository;
        private readonly IQueue<WorkItemData> _workItemQueue;
        private readonly SemanticVersionParser _semanticVersionParser;

        public CheckForRegressionAction(IStackRepository stackRepository, IQueue<WorkItemData> workItemQueue, SemanticVersionParser semanticVersionParser, ILoggerFactory loggerFactory = null) : base(loggerFactory) {
            _stackRepository = stackRepository;
            _workItemQueue = workItemQueue;
            _semanticVersionParser = semanticVersionParser;
            ContinueOnError = true;
        }

        public override async Task ProcessBatchAsync(ICollection<EventContext> contexts) {
            var stacks = contexts.Where(c => !c.Stack.IsRegressed && c.Stack.DateFixed.HasValue).OrderBy(c => c.Event.Date).GroupBy(c => c.Event.StackId);
            foreach (var stackGroup in stacks) {
                try {
                    var stack = stackGroup.First().Stack;

                    EventContext regressedContext = null;
                    SemanticVersion regressedVersion = null;
                    if (String.IsNullOrEmpty(stack.FixedInVersion)) {
                        regressedContext = stackGroup.FirstOrDefault(c => stack.DateFixed < c.Event.Date.UtcDateTime);
                    } else {
                        var fixedInVersion = await _semanticVersionParser.ParseAsync(stack.FixedInVersion).AnyContext();
                        var versions = stackGroup.GroupBy(c => c.Event.GetVersion());
                        foreach (var versionGroup in versions) {
                            var version = await _semanticVersionParser.ParseAsync(versionGroup.Key).AnyContext() ?? _semanticVersionParser.Default;
                            if (version < fixedInVersion)
                                continue;
                            
                            regressedVersion = version;
                            regressedContext = versionGroup.First();
                            break;
                        }
                    }

                    if (regressedContext == null)
                        return;

                    _logger.LogTrace("Marking stack and events as regressed in version: {Version}", regressedVersion);
                    stack.IsRegressed = true;
                    await _stackRepository.MarkAsRegressedAsync(stack.Id).AnyContext();
                    await _workItemQueue.EnqueueAsync(new StackWorkItem {
                        OrganizationId = stack.OrganizationId,
                        ProjectId = stack.ProjectId,
                        StackId = stack.Id,
                        UpdateIsFixed = true,
                        IsFixed = false
                    }).AnyContext();

                    foreach (var ctx in stackGroup) {
                        ctx.Event.IsFixed = false;
                        ctx.IsRegression = ctx == regressedContext;
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