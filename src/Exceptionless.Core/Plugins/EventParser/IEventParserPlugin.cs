﻿using Exceptionless.Core.Models;

namespace Exceptionless.Core.Plugins.EventParser;

public interface IEventParserPlugin : IPlugin
{
    List<PersistentEvent> ParseEvents(string input, int apiVersion, string userAgent);
}
