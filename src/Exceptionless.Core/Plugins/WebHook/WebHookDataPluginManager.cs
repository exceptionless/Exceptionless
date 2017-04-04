using System;
using System.Threading.Tasks;
using Exceptionless.Core.Dependency;
using Exceptionless.Core.Extensions;
using Foundatio.Logging;
using Foundatio.Metrics;

namespace Exceptionless.Core.Plugins.WebHook {
    public class WebHookDataPluginManager : PluginManagerBase<IWebHookDataPlugin> {
        public WebHookDataPluginManager(IDependencyResolver dependencyResolver = null, IMetricsClient metricsClient = null, ILoggerFactory loggerFactory = null) : base(dependencyResolver, metricsClient, loggerFactory) {}

        /// <summary>
        /// Runs all of the event plugins create method.
        /// </summary>
        public async Task<object> CreateFromEventAsync(WebHookDataContext context) {
            string metricPrefix = String.Concat(_metricPrefix, nameof(CreateFromEventAsync).ToLower(), ".");
            foreach (var plugin in Plugins.Values) {
                string metricName = String.Concat(metricPrefix, plugin.GetType().Name.ToLower());
                try {
                    object data = null;
                    await _metricsClient.TimeAsync(async () => data = await plugin.CreateFromEventAsync(context).AnyContext(), metricName).AnyContext();
                    if (data == null)
                        continue;

                    return data;
                } catch (Exception ex) {
                    _logger.Error().Exception(ex).Message("Error calling create from event in plugin \"{0}\": {1}", plugin.GetType().FullName, ex.Message).Property("Event", context.Event).Write();
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
                string metricName = String.Concat(metricPrefix, plugin.GetType().Name.ToLower());
                try {
                    object data = null;
                    await _metricsClient.TimeAsync(async () => data = await plugin.CreateFromStackAsync(context).AnyContext(), metricName).AnyContext();
                    if (data == null)
                        continue;

                    return data;
                } catch (Exception ex) {
                    _logger.Error().Exception(ex).Message("Error calling create from stack in plugin \"{0}\": {1}", plugin.GetType().FullName, ex.Message).Property("Stack", context.Stack).Write();
                }
            }

            return null;
        }
    }
}