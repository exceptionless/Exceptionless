using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Exceptionless.Core.Plugins.EventProcessor;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Exceptionless.Core.Pipeline {
    [Priority(5)]
    public class RunEventProcessingPluginsAction : EventPipelineActionBase {
        private readonly EventPluginManager _pluginManager;

        public RunEventProcessingPluginsAction(EventPluginManager pluginManager, IOptions<AppOptions> options, ILoggerFactory loggerFactory = null) : base(options, loggerFactory) {
            _pluginManager = pluginManager;
            ContinueOnError = true;
        }

        public override Task ProcessBatchAsync(ICollection<EventContext> contexts) {
            return _pluginManager.EventBatchProcessingAsync(contexts);
        }
    }
}