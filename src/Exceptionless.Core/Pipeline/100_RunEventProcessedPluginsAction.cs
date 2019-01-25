using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Exceptionless.Core.Plugins.EventProcessor;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Exceptionless.Core.Pipeline {
    [Priority(100)]
    public class RunEventProcessedPluginsAction : EventPipelineActionBase {
        private readonly EventPluginManager _pluginManager;

        public RunEventProcessedPluginsAction(EventPluginManager pluginManager, IOptions<AppOptions> options, ILoggerFactory loggerFactory = null) : base(options, loggerFactory) {
            _pluginManager = pluginManager;
            ContinueOnError = true;
        }

        public override Task ProcessBatchAsync(ICollection<EventContext> contexts) {
            return _pluginManager.EventBatchProcessedAsync(contexts);
        }
    }
}