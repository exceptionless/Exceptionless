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
        public List<Event> ParseEvents(string input) {
            foreach (var plugin in Plugins.Values.ToList()) {
                try {
                    var events = plugin.ParseEvents(input);
                    if (events == null)
                        continue;

                    // Set required event properties
                    events.ForEach(e => {
                        if (e.Date == DateTimeOffset.MinValue)
                            e.Date = DateTimeOffset.Now;
                        if (String.IsNullOrWhiteSpace(e.Type))
                            e.Type = e.Data.ContainsKey(Event.KnownDataKeys.Error) || e.Data.ContainsKey(Event.KnownDataKeys.SimpleError) ? Event.KnownTypes.Error : Event.KnownTypes.Log;
                    });

                    return events;
                } catch (Exception ex) {
                    Log.Error().Exception(ex).Message("Error calling ParseEvents in plugin \"{0}\": {1}", plugin.GetType().FullName, ex.Message).Write();
                }
            }

            return new List<Event>();
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
