using System;
using Exceptionless.Extras;
using Exceptionless.Logging;
using Exceptionless.Models;

namespace Exceptionless.Enrichments {
    public class ErrorEnrichment : IEventEnrichment {
        private readonly IExceptionlessLog _log;

        public ErrorEnrichment(IExceptionlessLog log) {
            _log = log;
        }

        public void Enrich(EventEnrichmentContext context, Event ev) {
            var exception = context.Data.GetException();
            if (exception == null)
                return;

            ev.Type = Event.KnownTypes.Error;
            ev.Data[Event.KnownDataKeys.Error] = exception.ToErrorModel(_log);
        }
    }
}