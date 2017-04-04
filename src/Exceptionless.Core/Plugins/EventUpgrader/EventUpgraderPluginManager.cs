using System;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Dependency;
using Exceptionless.Core.Extensions;
using Foundatio.Logging;
using Foundatio.Metrics;

namespace Exceptionless.Core.Plugins.EventUpgrader {
    public class EventUpgraderPluginManager : PluginManagerBase<IEventUpgraderPlugin> {
        public EventUpgraderPluginManager(IDependencyResolver dependencyResolver = null, IMetricsClient metricsClient = null, ILoggerFactory loggerFactory = null) : base(dependencyResolver, metricsClient, loggerFactory) { }

        /// <summary>
        /// Runs all of the event upgrade plugins upgrade method.
        /// </summary>
        public async Task UpgradeAsync(EventUpgraderContext context) {
            string metricPrefix = String.Concat(_metricPrefix, nameof(UpgradeAsync).ToLower(), ".");
            foreach (var plugin in Plugins.Values.ToList()) {
                string metricName = String.Concat(metricPrefix, plugin.GetType().Name.ToLower());
                try {
                   await _metricsClient.TimeAsync(() => plugin.Upgrade(context), metricName).AnyContext();
                } catch (Exception ex) {
                    _logger.Error().Exception(ex).Message("Error calling upgrade in plugin \"{0}\": {1}", plugin.GetType().FullName, ex.Message).Property("Context", context).Write();
                    throw;
                }
            }
        }
    }
}
