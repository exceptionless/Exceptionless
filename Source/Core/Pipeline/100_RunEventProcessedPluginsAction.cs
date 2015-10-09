using System;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Plugins.EventProcessor;

namespace Exceptionless.Core.Pipeline {
    [Priority(100)]
    public class RunEventProcessedPluginsAction : EventPipelineActionBase {
        private readonly EventPluginManager _pluginManager;

        public RunEventProcessedPluginsAction(EventPluginManager pluginManager) {
            _pluginManager = pluginManager;
        }

        protected override bool ContinueOnError => true;

        public override Task ProcessAsync(EventContext ctx) {
            return _pluginManager.EventProcessedAsync(ctx);
        }
    }
}