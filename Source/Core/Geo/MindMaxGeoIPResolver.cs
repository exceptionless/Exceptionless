using System;
using System.IO;
using MaxMind.Db;
using MaxMind.GeoIP2;
using NLog.Fluent;

namespace Exceptionless.Core.Geo {
    public class MindMaxGeoIPResolver : IGeoIPResolver {
        private readonly Lazy<DatabaseReader> _reader = new Lazy<DatabaseReader>(GetDatabase);

        public Location ResolveIp(string ip) {
            if (String.IsNullOrWhiteSpace(ip) || (!ip.Contains(".") && !ip.Contains(":")))
                return null;

            ip = ip.Trim();

            if (IsPrivateNetwork(ip))
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

        private bool IsPrivateNetwork(string ip) {
            if (String.Equals(ip, "::1") || String.Equals(ip, "127.0.0.1"))
                return true;

            // 10.0.0.0 – 10.255.255.255 (Class A)
            if (ip.StartsWith("10."))
                return true;

            // 172.16.00 – 172.31.255.255 (Class B)
            if (ip.StartsWith("172.")) {
                for (var range = 16; range < 32; range++) {
                    if (ip.StartsWith("172." + range + "."))
                        return true;
                }
            }

            // 192.168.0.0 – 192.168.255.255 (Class C)
            return ip.StartsWith("192.168.");
        }

        private static DatabaseReader GetDatabase() {
            if (String.IsNullOrEmpty(Settings.Current.GeoIPDatabasePath) || !File.Exists(Settings.Current.GeoIPDatabasePath)) {
                Log.Warn().Message("No GeoIP database was found.").Write();
                return null;
            }

            try {
                return new DatabaseReader(Settings.Current.GeoIPDatabasePath, FileAccessMode.Memory);
            } catch (Exception ex) {
                Log.Error().Exception(ex).Message("Unable to open GeoIP database.").Write();
            }

            return null;
        }
    }
}