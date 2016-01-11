using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Pipeline;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Queues;

namespace Exceptionless.Core.Plugins.EventProcessor.Default {
    [Priority(60)]
    public class LocationPlugin : EventProcessorPluginBase {
        private readonly ICacheClient _cacheClient;
        private readonly IQueue<WorkItemData> _workItemQueue;

        public LocationPlugin(ICacheClient cacheClient, IQueue<WorkItemData> workItemQueue) {
            _cacheClient = new ScopedCacheClient(cacheClient, "geo");
            _workItemQueue = workItemQueue;
        }

        public override async Task EventBatchProcessingAsync(ICollection<EventContext> contexts) {
            var geoGroups = contexts.Where(c => c.Organization.HasPremiumFeatures && !String.IsNullOrEmpty(c.Event.Geo)).GroupBy(c => c.Event.Geo);
            foreach (var geoGroup in geoGroups) {
                var location = await _cacheClient.GetAsync<Location>(geoGroup.Key, null).AnyContext();
                if (location == null)
                    continue;

                await _cacheClient.SetExpirationAsync(geoGroup.Key, TimeSpan.FromDays(30)).AnyContext();
                geoGroup.ForEach(c => c.Event.Data[Event.KnownDataKeys.Location] = location);
            }
        }

        public override async Task EventBatchProcessedAsync(ICollection<EventContext> contexts) {
            foreach (var ctx in contexts.Where(c => c.Organization.HasPremiumFeatures && !String.IsNullOrEmpty(c.Event.Geo) && !c.Event.Data.ContainsKey(Event.KnownDataKeys.Location))) {
                await _workItemQueue.EnqueueAsync(new SetLocationFromGeoWorkItem {
                    EventId = ctx.Event.Id,
                    Geo = ctx.Event.Geo
                }).AnyContext();
            }
        }
    }
}