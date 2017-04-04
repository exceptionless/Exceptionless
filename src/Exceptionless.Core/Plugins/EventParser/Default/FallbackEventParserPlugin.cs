using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Models;
using Foundatio.Utility;

namespace Exceptionless.Core.Plugins.EventParser {
    [Priority(Int32.MaxValue)]
    public class FallbackEventParserPlugin : IEventParserPlugin {
        public Task<List<PersistentEvent>> ParseEventsAsync(string input, int apiVersion, string userAgent) {
            var events = input.SplitLines().Select(entry => new PersistentEvent {
                Date = SystemClock.OffsetNow,
                Type = "log",
                Message = entry
            }).ToList();

            return Task.FromResult(events.Count > 0 ? events : null);
        }
    }
}