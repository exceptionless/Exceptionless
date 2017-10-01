using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Dependency;
using Exceptionless.Core.Extensions;
using Foundatio.Metrics;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Plugins.EventUpgrader {
    public class EventUpgraderPluginManager : PluginManagerBase<IEventUpgraderPlugin> {
        public EventUpgraderPluginManager(IDependencyResolver dependencyResolver = null, IMetricsClient metricsClient = null, ILoggerFactory loggerFactory = null) : base(dependencyResolver, metricsClient, loggerFactory) { }

        /// <summary>
        /// Runs all of the event upgrade plugins upgrade method.
        /// </summary>
        public async Task UpgradeAsync(EventUpgraderContext context) {
            string metricPrefix = String.Concat(_metricPrefix, nameof(UpgradeAsync).ToLower(), ".");
            foreach (var plugin in Plugins.Values.ToList()) {
                string metricName = String.Concat(metricPrefix, plugin.Name.ToLower());
                try {
                   await _metricsClient.TimeAsync(() => plugin.Upgrade(context), metricName).AnyContext();
                } catch (Exception ex) {
                    using (_logger.BeginScope(new Dictionary<string, object> { { "Context", context } }))
                        _logger.LogError(ex, "Error calling upgrade in plugin \"{PluginName}\": {Message}", plugin.Name, ex.Message);

                    throw;
                }
            }
        }
    }
}
