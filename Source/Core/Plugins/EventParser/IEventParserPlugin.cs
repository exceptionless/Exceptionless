using System;
using System.Collections.Generic;
using Exceptionless.Core.Models;

namespace Exceptionless.Core.Plugins.EventParser {
    public interface IEventParserPlugin {
        List<PersistentEvent> ParseEvents(string input, int apiVersion, string userAgent);
    }
}
