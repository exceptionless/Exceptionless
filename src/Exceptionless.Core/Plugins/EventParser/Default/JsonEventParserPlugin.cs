using System;
using System.Collections.Generic;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.IO;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Plugins.EventParser {
    [Priority(0)]
    public class JsonEventParserPlugin : PluginBase, IEventParserPlugin {
        private readonly JsonSerializerSettings _settings;
        private readonly JsonSerializer _serializer;

        public JsonEventParserPlugin(IOptions<AppOptions> options, JsonSerializerSettings settings) : base(options) {
            _settings = settings;
            _serializer = JsonSerializer.CreateDefault(_settings);
        }

        public List<PersistentEvent> ParseEvents(string input, int apiVersion, string userAgent) {
            if (apiVersion < 2)
                return null;

            var events = new List<PersistentEvent>();

            var reader = new JsonTextReader(new StringReader(input));
            reader.DateParseHandling = DateParseHandling.None;

            while (reader.Read()) {
                if (reader.TokenType == JsonToken.StartObject) {
                    var ev = JToken.ReadFrom(reader);

                    var data = ev["data"];
                    if (data != null) {
                        foreach (var property in data.Children<JProperty>()) {
                            // strip out large data entries
                            if (property.Value.ToString().Length > 50000) {
                                property.Value = "(Data Too Large)";
                            }
                        }
                    }

                    try {
                        events.Add(ev.ToObject<PersistentEvent>(_serializer));
                    } catch (Exception ex) {
                        _logger.LogError(ex, "Error deserializing event.");
                    }
                }
            }

            return events.Count > 0 ? events : null;
        }
    }
}
