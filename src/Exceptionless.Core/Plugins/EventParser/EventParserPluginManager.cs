using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Dependency;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Foundatio.Logging;
using Foundatio.Metrics;
using Foundatio.Utility;

namespace Exceptionless.Core.Plugins.EventParser {
    public class EventParserPluginManager : PluginManagerBase<IEventParserPlugin> {
        public EventParserPluginManager(IDependencyResolver dependencyResolver = null, IMetricsClient metricsClient = null, ILoggerFactory loggerFactory = null) : base(dependencyResolver, metricsClient, loggerFactory){}

        /// <summary>
        /// Runs through the formatting plugins to calculate an html summary for the stack based on the event data.
        /// </summary>
        public async Task<List<PersistentEvent>> ParseEventsAsync(string input, int apiVersion, string userAgent) {
            string metricPrefix = String.Concat(_metricPrefix, nameof(ParseEventsAsync).ToLower(), ".");
            foreach (var plugin in Plugins.Values.ToList()) {
                string metricName = String.Concat(metricPrefix, plugin.Name.ToLower());

                try {
                    List<PersistentEvent> events = null;
                    await _metricsClient.TimeAsync(async () => events = await plugin.ParseEventsAsync(input, apiVersion, userAgent).AnyContext(), metricName).AnyContext();
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
                    _logger.Error(ex, "Error calling ParseEvents in plugin \"{0}\": {1}", plugin.Name, ex.Message);
                }
            }

            return new List<PersistentEvent>();
        }
    }
}
