using System;
using System.Collections.Generic;
using Exceptionless.Models;

namespace Exceptionless.Core.Plugins.EventParser {
    public interface IEventParserPlugin {
        List<Event> ParseEvents(string input);
    }
}
