using System;
using System.Threading;
using System.Threading.Tasks;

namespace Exceptionless.Core.Geo {
    public class NullGeocodeService : IGeocodeService {
        public Task<GeoResult> ReverseGeocodeAsync(double latitude, double longitude, CancellationToken cancellationToken = default) {
            return Task.FromResult<GeoResult>(null);
        }
    }
}