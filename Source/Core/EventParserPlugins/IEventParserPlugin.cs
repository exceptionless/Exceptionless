using System;
using Exceptionless.Models;

namespace Exceptionless.Core.EventParserPlugins {
    public interface IEventParserPlugin {
        Event[] ParseEvents(string input);
    }
}
