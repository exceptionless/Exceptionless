using System;
using System.Collections.Generic;
using CodeSmith.Core.Component;
using CodeSmith.Core.Extensions;
using Exceptionless.Core.Extensions;
using Exceptionless.Models;
using Exceptionless.Serializer;
using Newtonsoft.Json;

namespace Exceptionless.Core.Plugins.EventParser {
    [Priority(0)]
    public class JsonEventParserPlugin : IEventParserPlugin {
        public List<PersistentEvent> ParseEvents(string input, int apiVersion, string userAgent) {
            if (apiVersion < 2)
                return null;

            var events = new List<PersistentEvent>();
            var serializerSettings = new JsonSerializerSettings {
                MissingMemberHandling = MissingMemberHandling.Ignore, 
                ContractResolver = new ExceptionlessContractResolver()
            };
            serializerSettings.AddModelConverters();

            switch (input.GetJsonType()) {
                case JsonType.Object: {
                    PersistentEvent ev;
                    if (input.TryFromJson(out ev, serializerSettings))
                        events.Add(ev);
                    break;
                }
                case JsonType.Array: {
                    PersistentEvent[] parsedEvents;
                    if (input.TryFromJson(out parsedEvents, serializerSettings))
                        events.AddRange(parsedEvents);
                    
                    break;
                }
            }

            return events.Count > 0 ? events : null;
        }
    }
}