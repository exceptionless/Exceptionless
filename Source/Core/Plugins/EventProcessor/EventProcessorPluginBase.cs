using System;
using System.Threading.Tasks;

namespace Exceptionless.Core.Plugins.EventProcessor {
    public abstract class EventProcessorPluginBase : IEventProcessorPlugin {
        public virtual Task StartupAsync() {
            return Task.FromResult(0);
        }

        public virtual Task EventProcessingAsync(EventContext context) { 
            return Task.FromResult(0);
        }

        public virtual Task EventProcessedAsync(EventContext context) { 
            return Task.FromResult(0);
        }
    }
}
