using System;
using Exceptionless.Models;

namespace Exceptionless.Enrichments.Default {
    public class SubmissionMethodEnrichment : IEventEnrichment {
        public void Enrich(EventEnrichmentContext context, Event ev) {
            string submissionMethod = context.Data.GetSubmissionMethod();
            if (!String.IsNullOrEmpty(submissionMethod))
                ev.AddObject(submissionMethod, Event.KnownDataKeys.SubmissionMethod);
        }
    }
}