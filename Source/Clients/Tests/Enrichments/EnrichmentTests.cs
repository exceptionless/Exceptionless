using System;
using Exceptionless;
using Exceptionless.Enrichments;
using Exceptionless.Enrichments.Default;
using Exceptionless.Models;
using Xunit;
using Xunit.Extensions;

namespace Client.Tests.Enrichments {
    public class EnrichmentTests {
        [Fact]
        public void ConfigurationDefaults_EnsureNoDuplicateTagsOrData() {
            var client = new ExceptionlessClient();
            var context = new EventEnrichmentContext(client);
            var ev = new Event();

            var enrichment = new ConfigurationDefaultsEnrichment();
            enrichment.Enrich(context, ev);
            Assert.Equal(0, ev.Tags.Count);

            client.Configuration.DefaultTags.Add(Event.KnownTags.Critical);
            enrichment.Enrich(context, ev);
            Assert.Equal(1, ev.Tags.Count);
            Assert.Equal(0, ev.Data.Count);

            client.Configuration.DefaultData.Add("Message", new { Exceptionless = "Is Awesome!" });
            for (int index = 0; index < 2; index++) {
                enrichment.Enrich(context, ev);
                Assert.Equal(1, ev.Tags.Count);
                Assert.Equal(1, ev.Data.Count);
            }
        }

        [Theory(Skip = "TODO: This needs to be skipped until the client is sending session start and end.")]
        [InlineData(Event.KnownTypes.Error)]
        [InlineData(Event.KnownTypes.FeatureUsage)]
        [InlineData(Event.KnownTypes.Log)]
        [InlineData(Event.KnownTypes.NotFound)]
        [InlineData(Event.KnownTypes.SessionEnd)]
        public void EnvironmentInfo_IncorrectEventType(string eventType) {
            var client = new ExceptionlessClient();
            var context = new EventEnrichmentContext(client);
            var ev = new Event { Type = eventType };

            var enrichment = new EnvironmentInfoEnrichment();
            enrichment.Enrich(context, ev);
            Assert.Equal(0, ev.Data.Count);
        }

        [Fact]
        public void EnvironmentInfo_ShouldAddSessionStart() {
            var client = new ExceptionlessClient();
            var context = new EventEnrichmentContext(client);
            var ev = new Event { Type = Event.KnownTypes.SessionStart };

            var enrichment = new EnvironmentInfoEnrichment();
            enrichment.Enrich(context, ev);
            Assert.Equal(1, ev.Data.Count);
            Assert.NotNull(ev.Data[Event.KnownDataKeys.EnvironmentInfo]);
        }
    }
}