using System;

namespace Exceptionless.Core.Plugins.EventPipeline {
    public interface IEventPlugin {
        void Startup();
        void EventProcessing(EventContext context);
        void EventProcessed(EventContext context);
    }
}
