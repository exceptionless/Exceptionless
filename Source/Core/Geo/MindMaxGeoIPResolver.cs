using System;
using System.IO;
using Exceptionless.Core.Utility;
using Exceptionless.DateTimeExtensions;
using Foundatio.Caching;
using MaxMind.Db;
using MaxMind.GeoIP2;
using MaxMind.GeoIP2.Exceptions;
using NLog.Fluent;

namespace Exceptionless.Core.Geo {
    public class MindMaxGeoIPResolver : IGeoIPResolver {
        private readonly InMemoryCacheClient _cache = new InMemoryCacheClient { MaxItems = 50 };
        private DatabaseReader _database;
        private DateTime? _databaseLastChecked;
        
        public Location ResolveIp(string ip) {
            if (String.IsNullOrWhiteSpace(ip) || (!ip.Contains(".") && !ip.Contains(":")))
                return null;

            ip = ip.Trim();

            Location location;
            if (_cache.TryGet(ip, out location))
                return location;

            if (IsPrivateNetwork(ip))
                return null;

            var database = GetDatabase();
            if (database == null)
                return null;

            try {
                var city = database.City(ip);
                if (city != null && city.Location != null)
                    location = new Location { Latitude = city.Location.Latitude, Longitude = city.Location.Longitude };

                _cache.Set(ip, location);
                return location;
            } catch (Exception ex) {
                if (ex is AddressNotFoundException || ex is GeoIP2Exception) {
                    Log.Info().Message(ex.Message).Write();
                    _cache.Set<Location>(ip, null);
                } else {
                    Log.Error().Exception(ex).Message("Unable to resolve geo location for ip: " + ip).Write();
                }

                return null;
            }
        }

        private DatabaseReader GetDatabase() {
            // Try to load the new database from disk if the current one is an hour old.
            if (_database != null && _databaseLastChecked.HasValue && _databaseLastChecked.Value < DateTime.UtcNow.SubtractHours(1)) {
                _database.Dispose();
                _database = null;
            }

            if (_database != null)
                return _database;

            if (_databaseLastChecked.HasValue && _databaseLastChecked.Value >= DateTime.UtcNow.SubtractSeconds(10))
                return null;

            _databaseLastChecked = DateTime.UtcNow;

            string databasePath = PathHelper.ExpandPath(Settings.Current.GeoIPDatabasePath);
            if (!Path.IsPathRooted(databasePath))
                databasePath = Path.GetFullPath(databasePath);

            if (!File.Exists(databasePath)) {
                Log.Warn().Message("No GeoIP database was found.").Write();
                return null;
            }

            Log.Info().Message("Loading GeoIP database from \"{0}\"", databasePath).Write();
            try {
                _database = new DatabaseReader(databasePath, FileAccessMode.Memory);
            } catch (Exception ex) {
                Log.Error().Exception(ex).Message("Unable to open GeoIP database.").Write();
            }

            return _database;
        }
        
        private bool IsPrivateNetwork(string ip) {
            if (String.Equals(ip, "::1") || String.Equals(ip, "127.0.0.1"))
                return true;

            // 10.0.0.0 – 10.255.255.255 (Class A)
            if (ip.StartsWith("10."))
                return true;

            // 172.16.0.0 – 172.31.255.255 (Class B)
            if (ip.StartsWith("172.")) {
                for (var range = 16; range < 32; range++) {
                    if (ip.StartsWith("172." + range + "."))
                        return true;
                }
            }

            // 192.168.0.0 – 192.168.255.255 (Class C)
            return ip.StartsWith("192.168.");
        }
    }
}