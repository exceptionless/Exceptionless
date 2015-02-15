using System;
using System.Collections.Generic;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Extensions;
using Exceptionless.Models;
using Exceptionless.Serializer;
using Newtonsoft.Json;

namespace Exceptionless.Core.Plugins.EventParser {
    [Priority(0)]
    public class JsonEventParserPlugin : IEventParserPlugin {
        private readonly JsonSerializerSettings _settings = new JsonSerializerSettings {
                MissingMemberHandling = MissingMemberHandling.Ignore, 
                ContractResolver = new ExceptionlessContractResolver()
            };

        public JsonEventParserPlugin() {
            _settings.AddModelConverters();
        }

        public List<PersistentEvent> ParseEvents(string input, int apiVersion, string userAgent) {
            if (apiVersion < 2)
                return null;

            var events = new List<PersistentEvent>();
            switch (input.GetJsonType()) {
                case JsonType.Object: {
                    PersistentEvent ev;
                    if (input.TryFromJson(out ev, _settings))
                        events.Add(ev);
                    break;
                }
                case JsonType.Array: {
                    PersistentEvent[] parsedEvents;
                    if (input.TryFromJson(out parsedEvents, _settings))
                        events.AddRange(parsedEvents);
                    
                    break;
                }
            }

            return events.Count > 0 ? events : null;
        }
    }
}