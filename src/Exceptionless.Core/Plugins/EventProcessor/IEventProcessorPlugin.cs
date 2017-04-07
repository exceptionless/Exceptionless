using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Exceptionless.Core.Plugins.EventProcessor {
    public interface IEventProcessorPlugin : IPlugin {
        Task StartupAsync();
        Task EventBatchProcessingAsync(ICollection<EventContext> contexts);
        Task EventBatchProcessedAsync(ICollection<EventContext> contexts);
    }
}
