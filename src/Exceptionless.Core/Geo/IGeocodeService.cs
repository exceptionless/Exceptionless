using System;
using System.Threading;
using System.Threading.Tasks;

namespace Exceptionless.Core.Geo {
    public interface IGeocodeService {
        Task<GeoResult> ReverseGeocodeAsync(double latitude, double longitude, CancellationToken cancellationToken = default);
    }
}