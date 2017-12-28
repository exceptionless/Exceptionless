using System;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.AppStats;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Geo;
using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Repositories;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Messaging;
using Foundatio.Metrics;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Jobs.WorkItemHandlers {
    public class SetLocationFromGeoWorkItemHandler : WorkItemHandlerBase {
        private readonly ICacheClient _cache;
        private readonly IEventRepository _eventRepository;
        private readonly IGeocodeService _geocodeService;
        private readonly IMetricsClient _metricsClient;
        private readonly ILockProvider _lockProvider;

        public SetLocationFromGeoWorkItemHandler(ICacheClient cacheClient, IEventRepository eventRepository, IGeocodeService geocodeService, IMetricsClient metricsClient, IMessageBus messageBus, ILoggerFactory loggerFactory = null) : base(loggerFactory) {
            _cache = new ScopedCacheClient(cacheClient, "Geo");
            _eventRepository = eventRepository;
            _geocodeService = geocodeService;
            _metricsClient = metricsClient;
            _lockProvider = new CacheLockProvider(cacheClient, messageBus);
        }

        public override Task<ILock> GetWorkItemLockAsync(object workItem, CancellationToken cancellationToken = new CancellationToken()) {
            string cacheKey = $"{nameof(SetLocationFromGeoWorkItemHandler)}:{((SetLocationFromGeoWorkItem)workItem).EventId}";
            return _lockProvider.AcquireAsync(cacheKey, TimeSpan.FromMinutes(15), new CancellationToken(true));
        }

        public override async Task HandleItemAsync(WorkItemContext context) {
            var workItem = context.GetData<SetLocationFromGeoWorkItem>();

            if (!GeoResult.TryParse(workItem.Geo, out GeoResult result))
                return;

            var location = await _cache.GetAsync<Location>(workItem.Geo, null).AnyContext();
            if (location == null) {
                try {
                    result = await _geocodeService.ReverseGeocodeAsync(result.Latitude.GetValueOrDefault(), result.Longitude.GetValueOrDefault()).AnyContext();
                    location = result.ToLocation();
                    _metricsClient.Counter(MetricNames.UsageGeocodingApi);
                } catch (Exception ex) {
                    Log.LogError(ex, "Error occurred looking up reverse geocode: {Geo}", workItem.Geo);
                }
            }
            
            if (location == null)
                return;
            
            await _cache.SetAsync(workItem.Geo, location, TimeSpan.FromDays(3)).AnyContext();

            var ev = await _eventRepository.GetByIdAsync(workItem.EventId).AnyContext();
            if (ev == null)
                return;

            ev.SetLocation(location);
            await _eventRepository.SaveAsync(ev).AnyContext();
        }
    }
}