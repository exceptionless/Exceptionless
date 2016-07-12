﻿using System;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.Core.Dependency;
using Exceptionless.Core.Models;
using Foundatio.Logging;

namespace Exceptionless.Core.Plugins.EventParser {
    public class EventParserPluginManager : PluginManagerBase<IEventParserPlugin> {
        public EventParserPluginManager(IDependencyResolver dependencyResolver = null, ILoggerFactory loggerFactory = null) : base(dependencyResolver, loggerFactory){}

        /// <summary>
        /// Runs through the formatting plugins to calculate an html summary for the stack based on the event data.
        /// </summary>
        public List<PersistentEvent> ParseEvents(string input, int apiVersion, string userAgent) {
            foreach (var plugin in Plugins.Values.ToList()) {
                try {
                    var events = plugin.ParseEvents(input, apiVersion, userAgent);
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
                    _logger.Error(ex, "Error calling ParseEvents in plugin \"{0}\": {1}", plugin.GetType().FullName, ex.Message);
                }
            }

            return new List<PersistentEvent>();
        }
    }
}
