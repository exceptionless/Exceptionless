using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Exceptionless.Core.Plugins {
    public interface IPlugin {
        string Name { get; }
        bool Enabled { get; }
    }

    public abstract class PluginBase : IPlugin {
        protected readonly ILogger _logger;
        protected readonly IOptions<AppOptions> _options;

        public PluginBase(IOptions<AppOptions> options, ILoggerFactory loggerFactory = null) {
            _options = options;
            var type = GetType();
            Name = type.Name;
            Enabled = !_options.Value.DisabledPlugins.Contains(type.Name, StringComparer.InvariantCultureIgnoreCase);
            _logger = loggerFactory?.CreateLogger(type);
        }

        public string Name { get; }
        public bool Enabled { get; }
    }
}