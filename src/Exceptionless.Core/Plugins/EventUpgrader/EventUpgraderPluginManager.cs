using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Foundatio.Metrics;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Plugins.EventUpgrader {
    public class EventUpgraderPluginManager : PluginManagerBase<IEventUpgraderPlugin> {
        public EventUpgraderPluginManager(IServiceProvider serviceProvider, IMetricsClient metricsClient = null, ILoggerFactory loggerFactory = null) : base(serviceProvider, metricsClient, loggerFactory) { }

        /// <summary>
        /// Runs all of the event upgrade plugins upgrade method.
        /// </summary>
        public void Upgrade(EventUpgraderContext context) {
            string metricPrefix = String.Concat(_metricPrefix, nameof(Upgrade).ToLower(), ".");
            foreach (var plugin in Plugins.Values.ToList()) {
                string metricName = String.Concat(metricPrefix, plugin.Name.ToLower());
                try {
                   _metricsClient.Time(() => plugin.Upgrade(context), metricName);
                } catch (Exception ex) {
                    using (_logger.BeginScope(new Dictionary<string, object> { { "Context", context } }))
                        _logger.LogError(ex, "Error calling upgrade in plugin {PluginName}: {Message}", plugin.Name, ex.Message);

                    throw;
                }
            }
        }
    }
}
