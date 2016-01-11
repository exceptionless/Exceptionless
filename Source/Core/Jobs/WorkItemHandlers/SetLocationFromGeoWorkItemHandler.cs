using System;
using System.Threading.Tasks;
using Exceptionless.Core.AppStats;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Geo;
using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Repositories;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Logging;
using Foundatio.Metrics;

namespace Exceptionless.Core.Jobs.WorkItemHandlers {
    public class SetLocationFromGeoWorkItemHandler : WorkItemHandlerBase {
        private readonly ICacheClient _cacheClient;
        private readonly IEventRepository _eventRepository;
        private readonly IGeocodeService _geocodeService;
        private readonly IMetricsClient _metricsClient;

        public SetLocationFromGeoWorkItemHandler(ICacheClient cacheClient, IEventRepository eventRepository, IGeocodeService geocodeService, IMetricsClient metricsClient) {
            _cacheClient = new ScopedCacheClient(cacheClient, "geo");
            _eventRepository = eventRepository;
            _geocodeService = geocodeService;
            _metricsClient = metricsClient;
        }

        public override async Task HandleItemAsync(WorkItemContext context) {
            var workItem = context.GetData<SetLocationFromGeoWorkItem>();

            GeoResult result;
            if (!GeoResult.TryParse(workItem.Geo, out result))
                return;
            
            var location = await _cacheClient.GetAsync<Location>(workItem.Geo, null).AnyContext();
            if (location == null) {
                try {
                    result = await _geocodeService.ReverseGeocodeAsync(result.Latitude.GetValueOrDefault(), result.Longitude.GetValueOrDefault()).AnyContext();
                    location = result.ToLocation();
                    await _metricsClient.CounterAsync(MetricNames.UsageGeocodingApi).AnyContext();
                } catch (Exception ex) {
                    Logger.Error().Exception(ex).Message($"Error occurred looking up reverse geocode: {workItem.Geo}").Write();
                }
            }
            
            if (location == null)
                return;
            
            await _cacheClient.SetAsync(workItem.Geo, location, TimeSpan.FromDays(30)).AnyContext();

            var ev = await _eventRepository.GetByIdAsync(workItem.EventId).AnyContext();
            if (ev == null)
                return;

            ev.SetLocation(location);
            await _eventRepository.SaveAsync(ev).AnyContext();
        }
    }
}