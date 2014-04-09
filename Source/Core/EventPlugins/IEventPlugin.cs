using System;

namespace Exceptionless.Core.EventPlugins {
    public interface IEventPlugin {
        void Startup();
        void EventProcessing(EventContext context);
        void EventProcessed(EventContext context);
    }
}
