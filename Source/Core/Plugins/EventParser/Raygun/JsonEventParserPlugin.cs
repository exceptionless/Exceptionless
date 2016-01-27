using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Pipeline;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Exceptionless.Core.Plugins.EventParser.Raygun {
    [Priority(20)]
    public class JsonEventParserPlugin : IEventParserPlugin {
        private readonly JsonSerializerSettings _settings;

        public JsonEventParserPlugin(JsonSerializerSettings settings) {
            _settings = settings;
        }

        public List<PersistentEvent> ParseEvents(string input, int apiVersion, string userAgent) {
            return null;
        }
    }
}
