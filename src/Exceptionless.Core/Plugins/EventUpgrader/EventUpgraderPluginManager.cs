﻿using System;
using System.Collections.Generic;
using System.Linq;
using Foundatio.Metrics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Exceptionless.Core.Plugins.EventUpgrader {
    public class EventUpgraderPluginManager : PluginManagerBase<IEventUpgraderPlugin> {
        public EventUpgraderPluginManager(IServiceProvider serviceProvider, IOptions<AppOptions> options, IMetricsClient metricsClient = null, ILoggerFactory loggerFactory = null) : base(serviceProvider, options, metricsClient, loggerFactory) { }

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
