using System;
using System.Collections.Generic;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Plugins.EventParser.Raygun.Models;

namespace Exceptionless.Core.Plugins.EventParser.Raygun {
    [Priority(0)]
    public class RaygunEventParserPlugin : IEventParserPlugin {
        public List<PersistentEvent> ParseEvents(string input, int apiVersion, string userAgent) {
            if (userAgent != "raygun" && apiVersion != 1)
                return null;
            
            // TODO: We need to support batch here.
            var events = new List<PersistentEvent>();
            RaygunModel model;
            if (input.TryFromJson(out model))
                events.Add(Mappers.PersistentEventMapping.Map(model));
            
            return events.Count > 0 ? events : null;
        }
    }
}
