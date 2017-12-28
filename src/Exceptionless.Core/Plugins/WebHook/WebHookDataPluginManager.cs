using System;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Foundatio.Metrics;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Plugins.WebHook {
    public class WebHookDataPluginManager : PluginManagerBase<IWebHookDataPlugin> {
        public WebHookDataPluginManager(IServiceProvider serviceProvider, IMetricsClient metricsClient = null, ILoggerFactory loggerFactory = null) : base(serviceProvider, metricsClient, loggerFactory) {}

        /// <summary>
        /// Runs all of the event plugins create method.
        /// </summary>
        public async Task<object> CreateFromEventAsync(WebHookDataContext context) {
            string metricPrefix = String.Concat(_metricPrefix, nameof(CreateFromEventAsync).ToLower(), ".");
            foreach (var plugin in Plugins.Values) {
                string metricName = String.Concat(metricPrefix, plugin.Name.ToLower());
                try {
                    object data = null;
                    await _metricsClient.TimeAsync(async () => data = await plugin.CreateFromEventAsync(context).AnyContext(), metricName).AnyContext();
                    if (data == null)
                        continue;

                    return data;
                } catch (Exception ex) {
                    _logger.LogError(ex, "Error calling create from event {id} in plugin {PluginName}: {Message}", context.Event.Id, plugin.Name, ex.Message);
                }
            }

            return null;
        }

        /// <summary>
        /// Runs all of the event plugins create method.
        /// </summary>
        public async Task<object> CreateFromStackAsync(WebHookDataContext context) {
            string metricPrefix = String.Concat(_metricPrefix, nameof(CreateFromStackAsync).ToLower(), ".");
            foreach (var plugin in Plugins.Values) {
                string metricName = String.Concat(metricPrefix, plugin.Name.ToLower());
                try {
                    object data = null;
                    await _metricsClient.TimeAsync(async () => data = await plugin.CreateFromStackAsync(context).AnyContext(), metricName).AnyContext();
                    if (data == null)
                        continue;

                    return data;
                } catch (Exception ex) {
                    _logger.LogError(ex, "Error calling create from stack {stack} in plugin {PluginName}: {Message}", context.Stack.Id, plugin.Name, ex.Message);
                }
            }

            return null;
        }
    }
}