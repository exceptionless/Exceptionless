using System;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.DateTimeExtensions;
using Foundatio.Caching;
using Foundatio.Logging;
using Foundatio.Storage;
using MaxMind.GeoIP2;
using MaxMind.GeoIP2.Exceptions;

namespace Exceptionless.Core.Geo {
    public class MaxMindGeoIpService : IGeoIpService, IDisposable {
        internal const string GEO_IP_DATABASE_PATH = "GeoLite2-City.mmdb";

        private readonly InMemoryCacheClient _localCache = new InMemoryCacheClient { MaxItems = 250 };
        private readonly IFileStorage _storage;
        private readonly ILogger _logger;
        private DatabaseReader _database;
        private DateTime? _databaseLastChecked;
        

        public MaxMindGeoIpService(IFileStorage storage, ILogger<MaxMindGeoIpService> logger) {
            _storage = storage;
            _logger = logger;
        }

        public async Task<GeoResult> ResolveIpAsync(string ip, CancellationToken cancellationToken = new CancellationToken()) {
            if (String.IsNullOrWhiteSpace(ip) || (!ip.Contains(".") && !ip.Contains(":")))
                return null;

            ip = ip.Trim();

            var cacheValue = await _localCache.GetAsync<GeoResult>(ip).AnyContext();
            if (cacheValue.HasValue)
                return cacheValue.Value;

            GeoResult result = null;

            if (ip.IsPrivateNetwork())
                return null;

            var database = await GetDatabaseAsync(cancellationToken).AnyContext();
            if (database == null)
                return null;

            try {
                var city = database.City(ip);
                if (city?.Location != null) {
                    result = new GeoResult {
                        Latitude = city.Location.Latitude,
                        Longitude = city.Location.Longitude,
                        Country = city.Country.IsoCode,
                        Level1 = city.MostSpecificSubdivision.IsoCode,
                        Locality = city.City.Name
                    };
                }

                await _localCache.SetAsync(ip, result).AnyContext();
                return result;
            } catch (Exception ex) {
                if (ex is AddressNotFoundException || ex is GeoIP2Exception) {
                    _logger.Trace().Message(ex.Message).Write();
                    await _localCache.SetAsync<GeoResult>(ip, null).AnyContext();
                } else {
                    _logger.Error().Exception(ex).Message("Unable to resolve geo location for ip: " + ip).Write();
                }

                return null;
            }
        }

        private async Task<DatabaseReader> GetDatabaseAsync(CancellationToken cancellationToken) {
            // Try to load the new database from disk if the current one is an hour old.
            if (_database != null && _databaseLastChecked.HasValue && _databaseLastChecked.Value < DateTime.UtcNow.SubtractHours(1)) {
                _database.Dispose();
                _database = null;
            }

            if (_database != null)
                return _database;

            if (_databaseLastChecked.HasValue && _databaseLastChecked.Value >= DateTime.UtcNow.SubtractSeconds(30))
                return null;

            _databaseLastChecked = DateTime.UtcNow;

            if (!await _storage.ExistsAsync(GEO_IP_DATABASE_PATH).AnyContext()) {
                _logger.Warn().Message("No GeoIP database was found.").Write();
                return null;
            }

            _logger.Info().Message("Loading GeoIP database.").Write();
            try {
                using (var stream = await _storage.GetFileStreamAsync(GEO_IP_DATABASE_PATH, cancellationToken).AnyContext())
                    _database = new DatabaseReader(stream);
            } catch (Exception ex) {
                _logger.Error().Exception(ex).Message("Unable to open GeoIP database.").Write();
            }

            return _database;
        }
        
        public void Dispose() {
            if (_database == null)
                return;

            _database.Dispose();
            _database = null;
        }
    }
}
