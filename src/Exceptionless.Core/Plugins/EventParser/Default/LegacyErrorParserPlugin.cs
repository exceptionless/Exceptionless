using System;
using System.Collections.Generic;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Plugins.EventUpgrader;
using Exceptionless.Core.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Exceptionless.Core.Plugins.EventParser {
    [Priority(10)]
    public class LegacyErrorParserPlugin : PluginBase, IEventParserPlugin {
        private readonly EventUpgraderPluginManager _manager;
        private readonly JsonSerializerSettings _settings;

        public LegacyErrorParserPlugin(EventUpgraderPluginManager manager, JsonSerializerSettings settings, ILoggerFactory loggerFactory) : base(loggerFactory) {
            _manager = manager;
            _settings = settings;
        }

        public List<PersistentEvent> ParseEvents(string input, int apiVersion, string userAgent) {
            if (apiVersion != 1)
                return null;

            try {
                var ctx = new EventUpgraderContext(input);
                _manager.Upgrade(ctx);

                return ctx.Documents.FromJson<PersistentEvent>(_settings);
            } catch (Exception ex) {
                _logger.LogError(ex, "Error parsing event: {Message}", ex.Message);
                return null;
            }
        }
    }
}