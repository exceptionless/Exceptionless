using System;

namespace Exceptionless.Core.Plugins.EventProcessor {
    public abstract class EventProcessorPluginBase : IEventProcessorPlugin {
        public virtual void Startup() {}

        public virtual void EventProcessing(EventContext context) { }

        public virtual void EventProcessed(EventContext context) { }
    }
}
