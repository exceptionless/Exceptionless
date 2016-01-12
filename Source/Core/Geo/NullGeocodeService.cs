using System;
using System.Threading;
using System.Threading.Tasks;

namespace Exceptionless.Core.Geo {
    public class NullGeocodeService : IGeocodeService {
        public async Task<GeoResult> ReverseGeocodeAsync(double latitude, double longitude, CancellationToken cancellationToken = new CancellationToken()) {
            return null;
        }
    }
}