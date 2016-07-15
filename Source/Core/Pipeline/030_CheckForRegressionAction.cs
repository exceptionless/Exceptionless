using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Plugins.EventProcessor;
using Exceptionless.Core.Repositories;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Logging;
using Foundatio.Queues;
using McSherry.SemanticVersioning;

namespace Exceptionless.Core.Pipeline {
    [Priority(30)]
    public class CheckForRegressionAction : EventPipelineActionBase {
        private readonly IStackRepository _stackRepository;
        private readonly IQueue<WorkItemData> _workItemQueue;
        private readonly InMemoryCacheClient _localCache = new InMemoryCacheClient { MaxItems = 250 };
        private static readonly SemanticVersion _defaultSemanticVersion = new SemanticVersion(0, 0);

        public CheckForRegressionAction(IStackRepository stackRepository, IQueue<WorkItemData> workItemQueue, ILoggerFactory loggerFactory = null) : base(loggerFactory) {
            _stackRepository = stackRepository;
            _workItemQueue = workItemQueue;
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
                        regressedContext = stackGroup.First();
                    } else {
                        var fixedInVersion = await GetSemanticVersionAsync(stack.FixedInVersion).AnyContext();
                        var versions = stackGroup.GroupBy(c => c.Event.GetVersion());
                        foreach (var versionGroup in versions) {
                            var version = await GetSemanticVersionAsync(versionGroup.Key).AnyContext() ?? _defaultSemanticVersion;
                            if (version < fixedInVersion)
                                continue;
                            
                            regressedVersion = version;
                            regressedContext = versionGroup.First();
                            break;
                        }
                    }

                    if (regressedContext == null)
                        return;

                    _logger.Trace("Marking stack and events as regressed in version: {version}", regressedVersion);
                    stack.IsRegressed = true;
                    await _stackRepository.MarkAsRegressedAsync(stack.Id).AnyContext();
                    await _workItemQueue.EnqueueAsync(new StackWorkItem { OrganizationId = stack.OrganizationId, StackId = stack.Id, UpdateIsFixed = true, IsFixed = false }).AnyContext();
                    
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
        
        private async Task<SemanticVersion> GetSemanticVersionAsync(string version) {
            version = version?.Trim();
            if (String.IsNullOrEmpty(version))
                return null;
            
            var cacheValue = await _localCache.GetAsync<SemanticVersion>(version).AnyContext();
            if (cacheValue.HasValue)
                return cacheValue.Value;

            SemanticVersion semanticVersion;
            if (!SemanticVersion.TryParse(version, out semanticVersion))
                _logger.Trace("Unable to parse version: {version}", version);

            await _localCache.SetAsync(version, semanticVersion).AnyContext();
            return semanticVersion;
        }
    }
}