using System;
using Exceptionless.Models;

namespace Exceptionless.Enrichments {
    public class PrivateInformationEnrichment : IEventEnrichment {
        public void Enrich(EventEnrichmentContext context, Event ev) {
            if (!context.Client.Configuration.IncludePrivateInformation)
                return;

            var user = ev.GetUserIdentity();
            if (user == null || String.IsNullOrEmpty(user.Identity))
                ev.SetUserIdentity(Environment.UserName);
        }
    }
}