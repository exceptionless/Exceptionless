using System;
using System.Threading.Tasks;
using Exceptionless.Core.Component;

namespace Exceptionless.Core.Plugins.EventProcessor {
    public abstract class EventProcessorPluginBase : IEventProcessorPlugin {
        public virtual Task StartupAsync() {
            return TaskHelper.Completed();
        }

        public virtual Task EventProcessingAsync(EventContext context) { 
            return TaskHelper.Completed();
        }

        public virtual Task EventProcessedAsync(EventContext context) { 
            return TaskHelper.Completed();
        }
    }
}
