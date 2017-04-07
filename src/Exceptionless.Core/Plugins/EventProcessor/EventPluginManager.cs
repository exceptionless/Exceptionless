using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Dependency;
using Exceptionless.Core.Extensions;
using Foundatio.Logging;
using Foundatio.Metrics;

namespace Exceptionless.Core.Plugins.EventProcessor {
    public class EventPluginManager : PluginManagerBase<IEventProcessorPlugin> {
        public EventPluginManager(IDependencyResolver dependencyResolver = null, IMetricsClient metricsClient = null, ILoggerFactory loggerFactory = null) : base(dependencyResolver, metricsClient, loggerFactory) { }

        /// <summary>
        /// Runs all of the event plugins startup method.
        /// </summary>
        public async Task StartupAsync() {
            string metricPrefix = String.Concat(_metricPrefix, nameof(StartupAsync).ToLower(), ".");
            foreach (var plugin in Plugins.Values.ToList()) {
                try {
                    string metricName = String.Concat(metricPrefix, plugin.Name.ToLower());
                    await _metricsClient.TimeAsync(() => plugin.StartupAsync(), metricName).AnyContext();
                } catch (Exception ex) {
                    _logger.Error(ex, "Error calling startup in plugin \"{0}\": {1}", plugin.Name, ex.Message);
                }
            }
        }

        /// <summary>
        /// Runs all of the event plugins event processing method.
        /// </summary>
        public async Task EventBatchProcessingAsync(ICollection<EventContext> contexts) {
            string metricPrefix = String.Concat(_metricPrefix, nameof(EventBatchProcessingAsync).ToLower(), ".");
            foreach (var plugin in Plugins.Values) {
                var contextsToProcess = contexts.Where(c => c.IsCancelled == false && !c.HasError).ToList();
                if (contextsToProcess.Count == 0)
                    break;

                string metricName = String.Concat(metricPrefix, plugin.Name.ToLower());
                try {
                    await _metricsClient.TimeAsync(() => plugin.EventBatchProcessingAsync(contextsToProcess), metricName).AnyContext();
                    if (contextsToProcess.All(c => c.IsCancelled || c.HasError))
                        break;
                } catch (Exception ex) {
                    _logger.Error(ex, "Error calling event processing in plugin \"{0}\": {1}", plugin.Name, ex.Message);
                }
            }
        }

        /// <summary>
        /// Runs all of the event plugins event processed method.
        /// </summary>
        public async Task EventBatchProcessedAsync(ICollection<EventContext> contexts) {
            string metricPrefix = String.Concat(_metricPrefix, nameof(EventBatchProcessedAsync).ToLower(), ".");
            foreach (var plugin in Plugins.Values) {
                var contextsToProcess = contexts.Where(c => c.IsCancelled == false && !c.HasError).ToList();
                if (contextsToProcess.Count == 0)
                    break;

                string metricName = String.Concat(metricPrefix, plugin.Name.ToLower());
                try {
                    await _metricsClient.TimeAsync(() => plugin.EventBatchProcessedAsync(contextsToProcess), metricName).AnyContext();
                    if (contextsToProcess.All(c => c.IsCancelled || c.HasError))
                        break;
                } catch (Exception ex) {
                    _logger.Error(ex, "Error calling event processed in plugin \"{0}\": {1}", plugin.Name, ex.Message);
                }
            }
        }
    }
}
