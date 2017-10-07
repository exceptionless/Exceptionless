using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Exceptionless.Core.Plugins {
    public interface IPlugin {
        string Name { get; }
        bool Enabled { get; }
    }

    public abstract class PluginBase : IPlugin {
        protected readonly ILogger _logger;

        public PluginBase(ILoggerFactory loggerFactory = null) {
            var type = GetType();
            Name = type.Name;
            Enabled = !Settings.Current.DisabledPlugins.Contains(type.Name, StringComparer.InvariantCultureIgnoreCase);
            _logger = loggerFactory?.CreateLogger(type);
        }

        public string Name { get; }
        public bool Enabled { get; }
    }
}