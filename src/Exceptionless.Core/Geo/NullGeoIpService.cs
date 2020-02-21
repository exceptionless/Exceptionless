using System.Threading;
using System.Threading.Tasks;

namespace Exceptionless.Core.Geo {
    public class NullGeoIpService : IGeoIpService {
        public Task<GeoResult> ResolveIpAsync(string ip, CancellationToken cancellationToken = default) {
            return Task.FromResult<GeoResult>(null);
        }
    }
}
