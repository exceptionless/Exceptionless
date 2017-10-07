using System;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Helpers;
using Foundatio.Metrics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Exceptionless.Core.Plugins {
    public abstract class PluginManagerBase<TPlugin> where TPlugin : class, IPlugin {
        protected readonly IServiceProvider _serviceProvider;
        protected readonly string _metricPrefix;
        protected readonly IMetricsClient _metricsClient;
        protected readonly ILogger _logger;

        public PluginManagerBase(IServiceProvider serviceProvider, IMetricsClient metricsClient = null, ILoggerFactory loggerFactory = null) {
            var type = GetType();
            _metricPrefix = String.Concat(type.Name.ToLower(), ".");
            _metricsClient = metricsClient ?? new InMemoryMetricsClient(new InMemoryMetricsClientOptions { LoggerFactory = loggerFactory });
            _logger = loggerFactory?.CreateLogger(type);
            _serviceProvider = serviceProvider;

            Plugins = new SortedList<int, TPlugin>();
            LoadDefaultPlugins();
        }

        public SortedList<int, TPlugin> Plugins { get; private set; }

        public void AddPlugin(Type pluginType) {
            var attr = pluginType.GetCustomAttributes(typeof(PriorityAttribute), true).FirstOrDefault() as PriorityAttribute;
            int priority = attr?.Priority ?? 0;

            var plugin = (TPlugin)_serviceProvider.GetService(pluginType);
            Plugins.Add(priority, plugin);
        }

        private void LoadDefaultPlugins() {
            var pluginTypes = TypeHelper.GetDerivedTypes<TPlugin>(new[] { typeof(Bootstrapper).Assembly });

            foreach (var type in pluginTypes) {
                if (Settings.Current.DisabledPlugins.Contains(type.Name, StringComparer.InvariantCultureIgnoreCase)) {
                    _logger.LogWarning("Plugin {TypeName} is currently disabled and won't be executed.", type.Name);
                    continue;
                }

                try {
                    AddPlugin(type);
                } catch (Exception ex) {
                    _logger.LogError(ex, "Unable to instantiate plugin of type {TypeFullName}: {Message}", type.FullName, ex.Message);
                    throw;
                }
            }
        }
    }
}