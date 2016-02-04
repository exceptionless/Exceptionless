using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Plugins.EventParser.Raygun.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Exceptionless.Core.Plugins.EventParser.Raygun {
    [Priority(0)]
    public class RaygunEventParserPlugin : IEventParserPlugin {
        private readonly JsonSerializerSettings _settings;

        public RaygunEventParserPlugin(JsonSerializerSettings settings) {
            _settings = settings;
        }

        public List<PersistentEvent> ParseEvents(string input, int apiVersion, string userAgent) {
            if (!(userAgent == "raygun" && apiVersion == 1))
                return null;

            var events = new List<PersistentEvent>();
            RaygunModel raygunModel;

            if (input.TryFromJson(out raygunModel, null)) {
                PersistentEvent ev = Mappers.PersistentEventMapping.Map(raygunModel);
                events.Add(ev);
            }

            return events.Count > 0 ? events : null;
        }
    }
}
