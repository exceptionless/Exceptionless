using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Geo;
using Geocoding.Google;

namespace Exceptionless.Insulation.Geo {
    public class GoogleGeocodeService : IGeocodeService {
        private readonly GoogleGeocoder _geocoder;
        public GoogleGeocodeService(string apiKey) {
            if (String.IsNullOrEmpty(apiKey))
                throw new ArgumentNullException(nameof(apiKey));

            _geocoder = new GoogleGeocoder(apiKey);
        }

        public async Task<GeoResult> ReverseGeocodeAsync(double latitude, double longitude, CancellationToken cancellationToken = default) {
            var addresses = await _geocoder.ReverseGeocodeAsync(latitude, longitude).AnyContext();
            var address = addresses.FirstOrDefault();
            if (address == null)
                return null;

            return new GeoResult {
                Country = address[GoogleAddressType.Country]?.ShortName,
                Level1 = address[GoogleAddressType.AdministrativeAreaLevel1]?.ShortName,
                Level2 = address[GoogleAddressType.AdministrativeAreaLevel2]?.ShortName,
                Locality = address[GoogleAddressType.Locality]?.ShortName,
                Latitude = latitude,
                Longitude = longitude
            };
        }
    }
}
