using System;
using System.Collections.Generic;
using CodeSmith.Core.Component;
using Exceptionless.Core.Plugins.EventUpgrader;
using Exceptionless.Models;
using Exceptionless.Serializer;
using Newtonsoft.Json;

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

            var ctx = new EventUpgraderContext(input);
            _manager.Upgrade(ctx);

            PersistentEvent ev;
            try { 
                var serializerSettings = new JsonSerializerSettings {
                    MissingMemberHandling = MissingMemberHandling.Ignore, 
                    ContractResolver = new ExtensionContractResolver()
                };

                ev = ctx.Document.ToObject<PersistentEvent>(JsonSerializer.CreateDefault(serializerSettings));
            } catch (Exception) {
                return null;
            }
            return new List<PersistentEvent> { ev };
        }
    }
}