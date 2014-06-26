using System;

namespace Exceptionless.Core.Plugins.EventProcessor {
    public interface IEventProcessorPlugin {
        void Startup();
        void EventProcessing(EventContext context);
        void EventProcessed(EventContext context);
    }
}
