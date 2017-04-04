using System;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Dependency;
using Exceptionless.Core.Helpers;
using Foundatio.Logging;
using Foundatio.Metrics;

namespace Exceptionless.Core.Plugins {
    public abstract class PluginManagerBase<TPlugin> {
        protected readonly IDependencyResolver _dependencyResolver;
        protected readonly string _metricPrefix;
        protected readonly IMetricsClient _metricsClient;
        protected readonly ILogger _logger;

        public PluginManagerBase(IDependencyResolver dependencyResolver = null, IMetricsClient metricsClient = null, ILoggerFactory loggerFactory = null) {
            _logger = loggerFactory.CreateLogger(GetType());
            _dependencyResolver = dependencyResolver ?? new DefaultDependencyResolver();
            _metricPrefix = String.Concat(GetType().Name.ToLower(), ".");
            _metricsClient = metricsClient ?? new InMemoryMetricsClient(loggerFactory: loggerFactory);
            Plugins = new SortedList<int, TPlugin>();
            LoadDefaultPlugins();
        }

        public SortedList<int, TPlugin> Plugins { get; private set; }

        public void AddPlugin(Type pluginType) {
            var attr = pluginType.GetCustomAttributes(typeof(PriorityAttribute), true).FirstOrDefault() as PriorityAttribute;
            int priority = attr?.Priority ?? 0;

            var plugin = (TPlugin)_dependencyResolver.GetService(pluginType);
            Plugins.Add(priority, plugin);
        }

        private void LoadDefaultPlugins() {
            var pluginTypes = TypeHelper.GetDerivedTypes<TPlugin>(new[] { typeof(Bootstrapper).Assembly });

            foreach (var type in pluginTypes) {
                try {
                    AddPlugin(type);
                } catch (Exception ex) {
                    _logger.Error(ex, "Unable to instantiate plugin of type \"{0}\": {1}", type.FullName, ex.Message);
                }
            }
        }
    }
}