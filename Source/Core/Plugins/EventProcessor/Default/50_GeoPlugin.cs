using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Geo;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Models;

namespace Exceptionless.Core.Plugins.EventProcessor.Default {
    [Priority(50)]
    public class GeoPlugin : EventProcessorPluginBase {
        private readonly IGeoIPResolver _geoIpResolver;

        public GeoPlugin(IGeoIPResolver geoIpResolver) {
            _geoIpResolver = geoIpResolver;
        }

        public override async Task EventBatchProcessingAsync(ICollection<EventContext> contexts) {
            var geoGroups = contexts.GroupBy(c => c.Event.Geo);
            foreach (var geoGroup in geoGroups) {
                Location location;
                if (Location.TryParse(geoGroup.Key, out location) && location.IsValid()) {
                    geoGroup.ForEach(c => c.Event.Geo = location.ToString());
                    continue;
                }

                foreach (var context in geoGroup) {
                    foreach (var ip in GetIpAddresses(context.Event)) {
                        location = await _geoIpResolver.ResolveIpAsync(ip).AnyContext();
                        if (location == null || !location.IsValid())
                            continue;

                        context.Event.Geo = location.ToString();
                        return;
                    }

                    context.Event.Geo = null;
                }
            }
        }

        private IEnumerable<string> GetIpAddresses(PersistentEvent ev) {
            if (!String.IsNullOrEmpty(ev.Geo) && (ev.Geo.Contains(".") || ev.Geo.Contains(":")))
                yield return ev.Geo;

            var request = ev.GetRequestInfo();
            if (!String.IsNullOrWhiteSpace(request?.ClientIpAddress))
                yield return request.ClientIpAddress;

            var environmentInfo = ev.GetEnvironmentInfo();
            if (String.IsNullOrWhiteSpace(environmentInfo?.IpAddress))
                yield break;

            foreach (var ip in environmentInfo.IpAddress.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                yield return ip;
        }
    }
}
