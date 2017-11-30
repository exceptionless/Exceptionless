using System;
using System.Collections.Generic;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Newtonsoft.Json;

namespace Exceptionless.Core.Plugins.EventParser {
    [Priority(0)]
    public class JsonEventParserPlugin : PluginBase, IEventParserPlugin {
        private readonly JsonSerializerSettings _settings;

        public JsonEventParserPlugin(JsonSerializerSettings settings) {
            _settings = settings;
        }

        public List<PersistentEvent> ParseEvents(string input, int apiVersion, string userAgent) {
            if (apiVersion < 2)
                return null;

            var events = new List<PersistentEvent>();
            switch (input.GetJsonType()) {
                case JsonType.Object: {
                        if (input.TryFromJson(out PersistentEvent ev, _settings))
                            events.Add(ev);
                        break;
                }
                case JsonType.Array: {
                        if (input.TryFromJson(out PersistentEvent[] parsedEvents, _settings))
                            events.AddRange(parsedEvents);

                        break;
                }
            }

            return events.Count > 0 ? events : null;
        }
    }
}