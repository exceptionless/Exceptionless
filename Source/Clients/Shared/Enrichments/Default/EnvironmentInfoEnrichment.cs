using System;
using Exceptionless.Dependency;
using Exceptionless.Logging;
using Exceptionless.Models;

namespace Exceptionless.Enrichments.Default {
    public class EnvironmentInfoEnrichment : IEventEnrichment {
        public void Enrich(EventEnrichmentContext context, Event ev) {
            if (ev.Type != Event.KnownTypes.SessionStart)
                return;

            try {
                var collector = context.Resolver.GetEnvironmentInfoCollector();
                var info = collector.GetEnvironmentInfo();
                info.InstallId = context.Client.Configuration.GetInstallId();
                ev.Data.Add(Event.KnownDataKeys.EnvironmentInfo, info);
            } catch (Exception ex) {
                context.Resolver.GetLog().FormattedError(typeof(EnvironmentInfoEnrichment), ex, "Error adding environment information: {0}", ex.Message);
            }
        }
    }
}