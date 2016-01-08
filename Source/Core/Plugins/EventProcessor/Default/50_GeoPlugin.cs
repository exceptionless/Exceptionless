using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Geo;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Models;
using Foundatio.Caching;

namespace Exceptionless.Core.Plugins.EventProcessor.Default {
    [Priority(50)]
    public class GeoPlugin : EventProcessorPluginBase {
        private readonly IGeoIPResolver _geoIpResolver;
        private readonly InMemoryCacheClient _localCache = new InMemoryCacheClient { MaxItems = 100 };

        public GeoPlugin(IGeoIPResolver geoIpResolver) {
            _geoIpResolver = geoIpResolver;
        }

        public override async Task EventBatchProcessingAsync(ICollection<EventContext> contexts) {
            var geoGroups = contexts.GroupBy(c => c.Event.Geo);
            foreach (var group in geoGroups) {
                GeoResult result;
                if (GeoResult.TryParse(group.Key, out result) && result.IsValid()) {
                    group.ForEach(c => UpdateGeoAndlocation(c.Event, result, false));
                    continue;
                }

                // The geo coordinates are all the same, set the location from the result of any of the ip addresses.
                if (!String.IsNullOrEmpty(group.Key)) {
                    result = await GetGeoFromIPAddressesAsync(group.SelectMany(c => c.Event.GetIpAddresses()).Distinct()).AnyContext();
                    group.ForEach(c => UpdateGeoAndlocation(c.Event, result));
                    continue;
                }
                
                // Each event could be a different user;
                foreach (var context in group) {
                    result = await GetGeoFromIPAddressesAsync(context.Event.GetIpAddresses()).AnyContext();
                    UpdateGeoAndlocation(context.Event, result);
                }
            }
        }

        private void UpdateGeoAndlocation(PersistentEvent ev, GeoResult result, bool isValidLocation = true) {
            ev.Geo = result?.ToString();
            
            if (result != null && isValidLocation)
                ev.SetLocation(result.Country, result.Level1, result.Level2, result.Locality);
            else
                ev.Data.Remove(Event.KnownDataKeys.Location);
        }

        private async Task<GeoResult> GetGeoFromIPAddressesAsync(IEnumerable<string> ips) {
            foreach (var ip in ips) {
                if (String.IsNullOrEmpty(ip))
                    continue;

                var cacheValue = await _localCache.GetAsync<GeoResult>(ip).AnyContext();
                if (cacheValue.HasValue && cacheValue.IsNull)
                    continue;

                if (cacheValue.HasValue)
                    return cacheValue.Value;

                var result = await _geoIpResolver.ResolveIpAsync(ip).AnyContext();
                if (result == null || !result.IsValid()) {
                    await _localCache.SetAsync<GeoResult>(ip, null).AnyContext();
                    continue;
                }
                
                await _localCache.SetAsync(ip, result).AnyContext();
                return result;
            }

            return null;
        }
    }
}
