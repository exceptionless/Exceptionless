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
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Plugins.EventProcessor.Default {
    [Priority(60)]
    public sealed class LocationPlugin : EventProcessorPluginBase {
        private readonly ICacheClient _cacheClient;
        private readonly IQueue<WorkItemData> _workItemQueue;

        public LocationPlugin(ICacheClient cacheClient, IQueue<WorkItemData> workItemQueue, ILoggerFactory loggerFactory = null) : base(loggerFactory) {
            _cacheClient = new ScopedCacheClient(cacheClient, "Geo");
            _workItemQueue = workItemQueue;
        }

        public override Task EventBatchProcessingAsync(ICollection<EventContext> contexts) {
            var geoGroups = contexts.Where(c => c.Organization.HasPremiumFeatures && !String.IsNullOrEmpty(c.Event.Geo) && !c.Event.Data.ContainsKey(Event.KnownDataKeys.Location)).GroupBy(c => c.Event.Geo);

            var tasks = new List<Task>();
            foreach (var geoGroup in geoGroups)
                tasks.Add(GetGeoLocationFromCacheAsync(geoGroup));

            return Task.WhenAll(tasks);
        }

        public override Task EventBatchProcessedAsync(ICollection<EventContext> contexts) {
            var contextsToProcess = contexts.Where(c => c.Organization.HasPremiumFeatures && !String.IsNullOrEmpty(c.Event.Geo) && !c.Event.Data.ContainsKey(Event.KnownDataKeys.Location));

            var tasks = new List<Task>();
            foreach (var ctx in contextsToProcess)
                tasks.Add(_workItemQueue.EnqueueAsync(new SetLocationFromGeoWorkItem { EventId = ctx.Event.Id, Geo = ctx.Event.Geo }));

            return Task.WhenAll(tasks);
        }

        private async Task GetGeoLocationFromCacheAsync(IGrouping<string, EventContext> geoGroup) {
            var location = await _cacheClient.GetAsync<Location>(geoGroup.Key, null).AnyContext();
            if (location == null)
                return;

            await _cacheClient.SetExpirationAsync(geoGroup.Key, TimeSpan.FromDays(3)).AnyContext();
            geoGroup.ForEach(c => c.Event.Data[Event.KnownDataKeys.Location] = location);
        }
    }
}