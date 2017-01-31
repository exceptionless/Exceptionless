using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Geo;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Models;
using Foundatio.Caching;
using Foundatio.Logging;

namespace Exceptionless.Core.Plugins.EventProcessor.Default {
    [Priority(50)]
    public sealed class GeoPlugin : EventProcessorPluginBase {
        private readonly IGeoIpService _geoIpService;
        private readonly InMemoryCacheClient _localCache = new InMemoryCacheClient { MaxItems = 100 };

        public GeoPlugin(IGeoIpService geoIpService, ILoggerFactory loggerFactory = null) : base(loggerFactory) {
            _geoIpService = geoIpService;
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
                    var ips = group.SelectMany(c => c.Event.GetIpAddresses()).Union(new[] { group.First().EventPostInfo?.IpAddress }).Distinct();
                    result = await GetGeoFromIpAddressesAsync(ips).AnyContext();
                    group.ForEach(c => UpdateGeoAndlocation(c.Event, result));
                    continue;
                }

                // Each event could be a different user;
                foreach (var context in group) {
                    var ips = context.Event.GetIpAddresses().Union(new[] { context.EventPostInfo?.IpAddress });
                    result = await GetGeoFromIpAddressesAsync(ips).AnyContext();
                    UpdateGeoAndlocation(context.Event, result);
                }
            }
        }

        private void UpdateGeoAndlocation(PersistentEvent ev, GeoResult result, bool isValidLocation = true) {
            ev.Geo = result?.ToString();

            if (result != null && isValidLocation)
                ev.SetLocation(result.ToLocation());
            else
                ev.Data.Remove(Event.KnownDataKeys.Location);
        }

        private async Task<GeoResult> GetGeoFromIpAddressesAsync(IEnumerable<string> ips) {
            foreach (var ip in ips) {
                if (String.IsNullOrEmpty(ip))
                    continue;

                var cacheValue = await _localCache.GetAsync<GeoResult>(ip).AnyContext();
                if (cacheValue.HasValue && cacheValue.IsNull)
                    continue;

                if (cacheValue.HasValue)
                    return cacheValue.Value;

                var result = await _geoIpService.ResolveIpAsync(ip).AnyContext();
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
