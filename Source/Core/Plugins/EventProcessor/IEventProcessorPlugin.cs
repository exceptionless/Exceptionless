using System;
using System.Threading.Tasks;

namespace Exceptionless.Core.Plugins.EventProcessor {
    public interface IEventProcessorPlugin {
        Task StartupAsync();
        Task EventProcessingAsync(EventContext context);
        Task EventProcessedAsync(EventContext context);
    }
}
