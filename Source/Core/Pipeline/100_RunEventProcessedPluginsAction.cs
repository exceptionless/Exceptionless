using System;
using System.Threading.Tasks;
using Exceptionless.Core.Plugins.EventProcessor;

namespace Exceptionless.Core.Pipeline {
    [Priority(100)]
    public class RunEventProcessedPluginsAction : EventPipelineActionBase {
        private readonly EventPluginManager _pluginManager;

        public RunEventProcessedPluginsAction(EventPluginManager pluginManager) {
            _pluginManager = pluginManager;
        }

        protected override bool ContinueOnError { get { return true; } }

        public override async Task ProcessAsync(EventContext ctx) {
            await _pluginManager.EventProcessedAsync(ctx);
        }
    }
}