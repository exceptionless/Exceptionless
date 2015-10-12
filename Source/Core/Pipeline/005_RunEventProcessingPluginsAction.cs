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

        protected override bool ContinueOnError => true;

        public override Task ProcessAsync(EventContext ctx) {
            return _pluginManager.EventProcessingAsync(ctx);
        }
    }
}