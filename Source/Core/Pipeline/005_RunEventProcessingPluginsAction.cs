using System;
using System.Threading.Tasks;
using Exceptionless.Core.Plugins.EventProcessor;

namespace Exceptionless.Core.Pipeline {
    [Priority(5)]
    public class RunEventProcessingPluginsAction : EventPipelineActionBase {
        private readonly EventPluginManager _pluginManager;

        public RunEventProcessingPluginsAction(EventPluginManager pluginManager) {
            _pluginManager = pluginManager;
        }

        protected override bool ContinueOnError { get { return true; } }

        public override async Task ProcessAsync(EventContext ctx) {
            await _pluginManager.EventProcessingAsync(ctx);
        }
    }
}