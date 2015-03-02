using System;
using System.Collections.Generic;
using Exceptionless.Core.Geo;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Models;

namespace Exceptionless.Core.Plugins.EventProcessor.Default {
    [Priority(60)]
    public class GeoPlugin : EventProcessorPluginBase {
        private readonly IGeoIPResolver _geoIpResolver;

        public GeoPlugin(IGeoIPResolver geoIpResolver) {
            _geoIpResolver = geoIpResolver;
        }

        public override void EventProcessing(EventContext context) {
            if (String.IsNullOrWhiteSpace(context.Event.Geo)) {
                context.Event.Geo = null;
                return;
            }

            Location location;
            if (Location.TryParse(context.Event.Geo, out location) && location.IsValid()) {
                context.Event.Geo = location.ToString();
                return;
            }

            foreach (var ip in GetIpAddresses(context.Event)) {
                location = _geoIpResolver.ResolveIp(ip);
                if (location == null || !location.IsValid())
                    continue;

                context.Event.Geo = location.ToString();
                return;
            }

            context.Event.Geo = null;
        }

        private IEnumerable<string> GetIpAddresses(PersistentEvent ev) {
            if (ev.Geo.Contains(".") || ev.Geo.Contains(":"))
                yield return ev.Geo;

            var request = ev.GetRequestInfo();
            if (request != null && !String.IsNullOrWhiteSpace(request.ClientIpAddress))
                yield return request.ClientIpAddress;

            var environmentInfo = ev.GetEnvironmentInfo();
            if (environmentInfo == null || String.IsNullOrWhiteSpace(environmentInfo.IpAddress))
                yield break;

            foreach (var ip in environmentInfo.IpAddress.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                yield return ip;
        }
    }
}
