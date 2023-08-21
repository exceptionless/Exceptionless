﻿using Exceptionless.Core.Helpers;
using Exceptionless.Core.Pipeline;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Plugins;

public abstract class PluginManagerBase<TPlugin> where TPlugin : class, IPlugin
{
    protected readonly IServiceProvider _serviceProvider;
    private readonly AppOptions _options;
    protected readonly string _metricPrefix;
    protected readonly ILogger _logger;

    public PluginManagerBase(IServiceProvider serviceProvider, AppOptions options, ILoggerFactory loggerFactory)
    {
        var type = GetType();
        _metricPrefix = "";
        _logger = loggerFactory.CreateLogger(type);
        _serviceProvider = serviceProvider;
        _options = options;

        Plugins = new SortedList<int, TPlugin>();
        LoadDefaultPlugins();
    }

    public SortedList<int, TPlugin> Plugins { get; private set; }

    public void AddPlugin(Type pluginType)
    {
        var attr = pluginType.GetCustomAttributes(typeof(PriorityAttribute), true).FirstOrDefault() as PriorityAttribute;
        int priority = attr?.Priority ?? 0;

        var plugin = _serviceProvider.GetService(pluginType) as TPlugin;
        Plugins.Add(priority, plugin ?? throw new ArgumentException($"Unable to resolve service for plugin type: {pluginType.Name}"));
    }

    private void LoadDefaultPlugins()
    {
        var pluginTypes = TypeHelper.GetDerivedTypes<TPlugin>();

        foreach (var type in pluginTypes)
        {
            if (_options.DisabledPlugins.Contains(type.Name, StringComparer.InvariantCultureIgnoreCase))
            {
                _logger.LogWarning("Plugin {TypeName} is currently disabled and won't be executed", type.Name);
                continue;
            }

            try
            {
                AddPlugin(type);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to instantiate plugin of type {TypeFullName}: {Message}", type.FullName, ex.Message);
                throw;
            }
        }
    }
}
