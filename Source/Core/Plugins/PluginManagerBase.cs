using System;
using System.Collections.Generic;
using System.Linq;
using CodeSmith.Core.Component;
using CodeSmith.Core.Dependency;
using CodeSmith.Core.Helpers;
using NLog.Fluent;

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
            int priority = attr != null ? attr.Priority : 0;

            var plugin = (TPlugin)_dependencyResolver.GetService(pluginType);
            Plugins.Add(priority, plugin);
        }

        private void LoadDefaultPlugins() {
            var pluginTypes = TypeHelper.GetDerivedTypes<TPlugin>();

            foreach (var type in pluginTypes) {
                try {
                    AddPlugin(type);
                } catch (Exception ex) {
                    Log.Error().Exception(ex).Message("Unable to instantiate plugin of type \"{0}\": {1}", type.FullName, ex.Message).Write();
                }
            }
        }
    }
}