using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Exceptionless.Core.Models;

namespace Exceptionless.Core.Plugins.EventParser {
    public interface IEventParserPlugin : IPlugin {
        Task<List<PersistentEvent>> ParseEventsAsync(string input, int apiVersion, string userAgent);
    }
}