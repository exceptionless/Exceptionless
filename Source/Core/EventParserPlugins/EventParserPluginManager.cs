using System;
using System.Collections.Generic;
using System.Linq;
using CodeSmith.Core.Component;
using CodeSmith.Core.Dependency;
using CodeSmith.Core.Helpers;
using Exceptionless.Models;
using NLog.Fluent;

namespace Exceptionless.Core.EventParserPlugins {
    public class EventParserPluginManager {
        private readonly IDependencyResolver _dependencyResolver;

        public EventParserPluginManager(IDependencyResolver dependencyResolver = null) {
            _dependencyResolver = dependencyResolver ?? new DefaultDependencyResolver();
            Plugins = new SortedList<int, IEventParserPlugin>();
            LoadDefaultEventParserPlugins();
        }

        /// <summary>
        /// Runs through the formatting plugins to calculate an html summary for the stack based on the event data.
        /// </summary>
        public Event[] ParseEvents(string input) {
            foreach (var plugin in Plugins.Values.ToList()) {
                try {
                    Event[] result = plugin.ParseEvents(input);
                    if (result != null)
                        return result;
                } catch (Exception ex) {
                    Log.Error().Exception(ex).Message("Error calling ParseEvents in plugin \"{0}\": {1}", plugin.GetType().FullName, ex.Message).Write();
                }
            }

            return null;
        }

        public SortedList<int, IEventParserPlugin> Plugins { get; private set; }

        public void AddPlugin(Type pluginType) {
            var attr = pluginType.GetCustomAttributes(typeof(PriorityAttribute), true).FirstOrDefault() as PriorityAttribute;
            int priority = attr != null ? attr.Priority : 0;

            var plugin = _dependencyResolver.GetService(pluginType) as IEventParserPlugin;
            Plugins.Add(priority, plugin);
        }

        private void LoadDefaultEventParserPlugins() {
            var pluginTypes = TypeHelper.GetDerivedTypes<IEventParserPlugin>();

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
