using System;
using System.Collections.Generic;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Pipeline;

namespace Exceptionless.Core.Plugins.EventParser.Raygun {
    [Priority(0)]
    public class RaygunEventParserPlugin : IEventParserPlugin {
        private readonly RaygunEventMapper _mapper = new RaygunEventMapper();

        public List<PersistentEvent> ParseEvents(string input, int apiVersion, string userAgent) {
            if (userAgent != "raygun" && apiVersion != 1)
                return null;
            
            var events = new List<PersistentEvent>();
            switch (input.GetJsonType()) {
                case JsonType.Object: {
                        RaygunModel model;
                        if (input.TryFromJson(out model)) {
                            PersistentEvent ev = _mapper.Map(model);
                            events.Add(ev);
                        }
                        
                        break;
                    }
                case JsonType.Array: {
                        RaygunModel[] models;
                        if (input.TryFromJson(out models)) {
                            PersistentEvent[] parsedEvents = _mapper.Map(models);
                            events.AddRange(parsedEvents);
                        }

                        break;
                    }
            }

            return events.Count > 0 ? events : null;
        }
    }
}
