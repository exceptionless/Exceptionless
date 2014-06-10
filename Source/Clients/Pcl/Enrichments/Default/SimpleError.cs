using System;
using Exceptionless.Extensions;
using Exceptionless.Logging;
using Exceptionless.Models;

namespace Exceptionless.Enrichments.Default {
    public class SimpleError : IEventEnrichment {
        private readonly IExceptionlessLog _log;
        public SimpleError(IExceptionlessLog log) {
            _log = log;
        }

        public void Enrich(EventEnrichmentContext context, Event ev) {
            if (!context.ContextData.ContainsKey(EventEnrichmentContext.KnownContextDataKeys.Exception))
                return;

            var exception = ev.Data[EventEnrichmentContext.KnownContextDataKeys.Exception] as Exception;
            if (exception == null)
                return;

            ev.SetError(exception.ToSimpleErrorModel(_log));
        }
    }
}