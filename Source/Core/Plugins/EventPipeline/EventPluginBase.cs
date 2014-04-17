using System;

namespace Exceptionless.Core.Plugins.EventPipeline {
    public abstract class EventPluginBase : IEventPlugin {
        public virtual void Startup() {}

        public virtual void EventProcessing(EventContext context) { }

        public virtual void EventProcessed(EventContext context) { }
    }
}
