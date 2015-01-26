using System;
using System.IO;
using MaxMind.Db;
using MaxMind.GeoIP2;
using NLog.Fluent;

namespace Exceptionless.Core.Geo {
    public class MindMaxGeoIPResolver : IGeoIPResolver {
        private readonly Lazy<DatabaseReader> _reader = new Lazy<DatabaseReader>(GetDatabase);

        public Location ResolveIp(string ip) {
            if (String.IsNullOrWhiteSpace(ip) || (!ip.Contains(".") || !ip.Contains(":")))
                return null;

            if (_reader.Value == null)
                return null;

            try {
                var city = _reader.Value.City(ip);
                if (city == null || city.Location == null)
                    return null;

                return new Location {
                    Latitude = city.Location.Latitude,
                    Longitude = city.Location.Longitude
                };
            } catch (Exception ex) {
                Log.Error().Exception(ex).Message("Unable to resolve geo location for ip: " + ip).Write();
                return null;
            }
        }

        private static DatabaseReader GetDatabase() {
            if (String.IsNullOrEmpty(Settings.Current.GeoIpConnectionString) || !File.Exists(Settings.Current.GeoIpConnectionString)) {
                Log.Warn().Message("No GeoIP database was found.").Write();
                return null;
            }

            try {
                return new DatabaseReader(Settings.Current.GeoIpConnectionString, FileAccessMode.Memory);
            } catch (Exception ex) {
                Log.Error().Exception(ex).Message("Unable to open GeoIP database.").Write();
            }

            return null;
        }
    }
}