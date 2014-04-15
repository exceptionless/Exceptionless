using System;
using System.Collections.Generic;
using System.Linq;
using CodeSmith.Core.Component;
using CodeSmith.Core.Dependency;
using CodeSmith.Core.Helpers;
using NLog.Fluent;

namespace Exceptionless.Core.EventPlugins {
    public class EventPluginManager {
        private readonly IDependencyResolver _dependencyResolver;

        public EventPluginManager(IDependencyResolver dependencyResolver = null) {
            _dependencyResolver = dependencyResolver ?? new DefaultDependencyResolver();
            Plugins = new SortedList<int, IEventPlugin>();
            LoadDefaultEventPlugins();
        }

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
                    Log.Error().Exception(ex).Message("Error calling startup in plugin \"{0}\": {1}", plugin.GetType().FullName, ex.Message).Write();
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
                    Log.Error().Exception(ex).Message("Error calling startup in plugin \"{0}\": {1}", plugin.GetType().FullName, ex.Message).Write();
                }
            }
        }

        public SortedList<int, IEventPlugin> Plugins { get; private set; }

        public void AddPlugin(Type pluginType) {
            var attr = pluginType.GetCustomAttributes(typeof(PriorityAttribute), true).FirstOrDefault() as PriorityAttribute;
            int priority = attr != null ? attr.Priority : 0;

            var plugin = _dependencyResolver.GetService(pluginType) as IEventPlugin;
            Plugins.Add(priority, plugin);
        }

        private void LoadDefaultEventPlugins() {
            var pluginTypes = TypeHelper.GetDerivedTypes<IEventPlugin>();

            foreach (var type in pluginTypes) {
                try {
                    AddPlugin(type);
                } catch (Exception ex) {
                    Log.Error().Exception(ex).Message("Unable to instantiate plugin of type \"{0}\": {1}", type.FullName, ex.Message).Write();
                }
            }
        }
    }
}
