using System;
using System.Linq;
using Exceptionless.Core.Dependency;
using NLog.Fluent;

namespace Exceptionless.Core.Plugins.EventProcessor {
    public class EventPluginManager : PluginManagerBase<IEventProcessorPlugin> {
        public EventPluginManager(IDependencyResolver dependencyResolver = null) : base(dependencyResolver) { }

        /// <summary>
        /// Runs all of the event plugins startup method.
        /// </summary>
        public void Startup() {
            foreach (var plugin in Plugins.Values.ToList()) {
                try {
                    plugin.Startup();
                } catch (Exception ex) {
                    Log.Error().Exception(ex).Message("Error calling startup in plugin \"{0}\": {1}", plugin.GetType().FullName, ex.Message).Write();
                }
            }
        }

        /// <summary>
        /// Runs all of the event plugins event processing method.
        /// </summary>
        public void EventProcessing(EventContext context) {
            foreach (var plugin in Plugins.Values) {
                try {
                    plugin.EventProcessing(context);
                } catch (Exception ex) {
                    Log.Error().Message("Error calling event processing in plugin \"{0}\": {1}", plugin.GetType().FullName, ex.Message).Exception(ex).Write();
                }
            }
        }

        /// <summary>
        /// Runs all of the event plugins event processed method.
        /// </summary>
        public void EventProcessed(EventContext context) {
            foreach (var plugin in Plugins.Values) {
                try {
                    plugin.EventProcessed(context);
                } catch (Exception ex) {
                    Log.Error().Message("Error calling event processed in plugin \"{0}\": {1}", plugin.GetType().FullName, ex.Message).Exception(ex).Write();
                }
            }
        }
    }
}
