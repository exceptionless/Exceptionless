using System;
using System.Collections.Generic;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Plugins.EventUpgrader;
using Exceptionless.Models;
using Exceptionless.Serializer;
using Newtonsoft.Json;
using NLog.Fluent;

namespace Exceptionless.Core.Plugins.EventParser {
    [Priority(10)]
    public class LegacyErrorParserPlugin : IEventParserPlugin {
        private readonly EventUpgraderPluginManager _manager;

        public LegacyErrorParserPlugin(EventUpgraderPluginManager manager) {
            _manager = manager;
        }

        public List<PersistentEvent> ParseEvents(string input, int apiVersion, string userAgent) {
            if (apiVersion != 1)
                return null;

            try {
                var ctx = new EventUpgraderContext(input);
                _manager.Upgrade(ctx);

                var settings = new JsonSerializerSettings {
                    MissingMemberHandling = MissingMemberHandling.Ignore, 
                    ContractResolver = new ExtensionContractResolver()
                };

                return ctx.Documents.FromJson<PersistentEvent>(settings);
            } catch (Exception ex) {
                Log.Error().Message("Error parsing event: {0}", ex.Message).Exception(ex).Write();
                return null;
            }
        }
    }
}