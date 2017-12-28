using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Foundatio.Metrics;
using Foundatio.Utility;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Plugins.EventParser {
    public class EventParserPluginManager : PluginManagerBase<IEventParserPlugin> {
        public EventParserPluginManager(IServiceProvider serviceProvider, IMetricsClient metricsClient = null, ILoggerFactory loggerFactory = null) : base(serviceProvider, metricsClient, loggerFactory){}

        /// <summary>
        /// Runs through the formatting plugins to calculate an html summary for the stack based on the event data.
        /// </summary>
        public List<PersistentEvent> ParseEvents(string input, int apiVersion, string userAgent) {
            string metricPrefix = String.Concat(_metricPrefix, nameof(ParseEvents).ToLower(), ".");
            foreach (var plugin in Plugins.Values.ToList()) {
                string metricName = String.Concat(metricPrefix, plugin.Name.ToLower());

                try {
                    List<PersistentEvent> events = null;
                    _metricsClient.Time(() => events = plugin.ParseEvents(input, apiVersion, userAgent), metricName);
                    if (events == null)
                        continue;

                    // Set required event properties
                    events.ForEach(e => {
                        if (e.Date == DateTimeOffset.MinValue)
                            e.Date = SystemClock.OffsetNow;

                        if (String.IsNullOrWhiteSpace(e.Type))
                            e.Type = e.Data.ContainsKey(Event.KnownDataKeys.Error) || e.Data.ContainsKey(Event.KnownDataKeys.SimpleError) ? Event.KnownTypes.Error : Event.KnownTypes.Log;
                    });

                    return events;
                } catch (Exception ex) {
                    _logger.LogError(ex, "Error calling ParseEvents in plugin {PluginName}: {Message}", plugin.Name, ex.Message);
                }
            }

            return new List<PersistentEvent>();
        }
    }
}
