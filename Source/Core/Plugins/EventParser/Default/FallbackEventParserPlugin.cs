using System;
using System.Collections.Generic;
using System.Linq;
using CodeSmith.Core.Component;
using CodeSmith.Core.Extensions;
using Exceptionless.Models;

namespace Exceptionless.Core.Plugins.EventParser {
    [Priority(Int32.MaxValue)]
    public class FallbackEventParserPlugin : IEventParserPlugin {
        public List<PersistentEvent> ParseEvents(string input) {
            var events = new List<PersistentEvent>();
            foreach (var entry in input.SplitAndTrim(new[] { Environment.NewLine }).Where(line => !String.IsNullOrWhiteSpace(line))) {
                events.Add(new PersistentEvent {
                    Date = DateTimeOffset.Now,
                    Type = "log",
                    Message = entry
                });
            }

            return events.Count > 0 ? events : null;
        }
    }
}