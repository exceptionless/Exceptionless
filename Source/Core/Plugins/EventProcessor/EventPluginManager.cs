using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Dependency;
using Exceptionless.Core.Extensions;
using Foundatio.Logging;

namespace Exceptionless.Core.Plugins.EventProcessor {
    public class EventPluginManager : PluginManagerBase<IEventProcessorPlugin> {
        public EventPluginManager(IDependencyResolver dependencyResolver = null) : base(dependencyResolver) { }

        /// <summary>
        /// Runs all of the event plugins startup method.
        /// </summary>
        public async Task StartupAsync() {
            foreach (var plugin in Plugins.Values.ToList()) {
                try {
                    await plugin.StartupAsync().AnyContext();
                } catch (Exception ex) {
                    Logger.Error().Exception(ex).Message("Error calling startup in plugin \"{0}\": {1}", plugin.GetType().FullName, ex.Message).Write();
                }
            }
        }

        /// <summary>
        /// Runs all of the event plugins event processing method.
        /// </summary>
        public async Task EventBatchProcessingAsync(ICollection<EventContext> contexts) {
            foreach (var plugin in Plugins.Values) {
                try {
                    await plugin.EventBatchProcessingAsync(contexts.Where(c => c.IsCancelled == false && !c.HasError).ToList()).AnyContext();
                    if (contexts.All(c => c.IsCancelled || c.HasError))
                        break;
                } catch (Exception ex) {
                    Logger.Error().Message("Error calling event processing in plugin \"{0}\": {1}", plugin.GetType().FullName, ex.Message).Exception(ex).Write();
                }
            }
        }

        /// <summary>
        /// Runs all of the event plugins event processed method.
        /// </summary>
        public async Task EventBatchProcessedAsync(ICollection<EventContext> contexts) {
            foreach (var plugin in Plugins.Values) {
                try {
                    await plugin.EventBatchProcessedAsync(contexts.Where(c => c.IsCancelled == false && !c.HasError).ToList()).AnyContext();
                    if (contexts.All(c => c.IsCancelled || c.HasError))
                        break;
                } catch (Exception ex) {
                    Logger.Error().Message("Error calling event processed in plugin \"{0}\": {1}", plugin.GetType().FullName, ex.Message).Exception(ex).Write();
                }
            }
        }
    }
}
