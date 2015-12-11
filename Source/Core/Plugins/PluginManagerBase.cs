using System;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Dependency;
using Exceptionless.Core.Helpers;
using Foundatio.Logging;

namespace Exceptionless.Core.Plugins {
    public abstract class PluginManagerBase<TPlugin> {
        protected readonly IDependencyResolver _dependencyResolver;

        public PluginManagerBase(IDependencyResolver dependencyResolver = null) {
            _dependencyResolver = dependencyResolver ?? new DefaultDependencyResolver();
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
                    Logger.Error().Exception(ex).Message("Unable to instantiate plugin of type \"{0}\": {1}", type.FullName, ex.Message).Write();
                }
            }
        }
    }
}