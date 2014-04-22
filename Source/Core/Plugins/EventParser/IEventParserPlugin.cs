using System;
using System.Collections.Generic;
using Exceptionless.Models;

namespace Exceptionless.Core.Plugins.EventParser {
    public interface IEventParserPlugin {
        List<PersistentEvent> ParseEvents(string input);
    }
}
