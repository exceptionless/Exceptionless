using System;
using System.Diagnostics;
using System.Globalization;
using Exceptionless.Core.Models.Data;

namespace Exceptionless.Core.Geo {
    [DebuggerDisplay("{Latitude},{Longitude}, {Locality}, {Level2}, {Level1}, {Country}")]
    public class GeoResult {
        public double? Latitude { get; set; }

        public double? Longitude { get; set; }
        
        public string Country { get; set; }

        /// <summary>
        /// State / Province
        /// </summary>
        public string Level1 { get; set; }

        /// <summary>
        /// County
        /// </summary>
        public string Level2 { get; set; }

        /// <summary>
        /// City
        /// </summary>
        public string Locality { get; set; }

        public bool IsValid() {
            if (!Latitude.HasValue || Latitude < -90.0 || Latitude > 90.0)
                return false;

            if (!Longitude.HasValue || Longitude < -180.0 || Longitude > 180.0)
                return false;

            return true;
        }

        public static bool TryParse(string input, out GeoResult result) {
            result = null;
            if (String.IsNullOrEmpty(input))
                return false;

            string[] parts = input.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2)
                return false;

            if (!Double.TryParse(parts[0]?.Trim(), out double latitude))
                return false;

            if (!Double.TryParse(parts[1]?.Trim(), out double longitude))
                return false;

            result = new GeoResult { Latitude = latitude, Longitude = longitude };
            return true;
        }

        public override string ToString() {
            if (!Latitude.HasValue || !Longitude.HasValue)
                return null;

            return Latitude.GetValueOrDefault().ToString("#0.0#######", CultureInfo.InvariantCulture) + "," + Longitude.GetValueOrDefault().ToString("#0.0#######", CultureInfo.InvariantCulture);
        }
    }

    public static class GeoResultExtensions {
        public static Location ToLocation(this GeoResult result) {
            if (result == null)
                return null;

            if (String.IsNullOrEmpty(result.Country) && String.IsNullOrEmpty(result.Level1) && String.IsNullOrEmpty(result.Level2) && String.IsNullOrEmpty(result.Locality))
                return null;

            return new Location {
                Country = result.Country?.Trim(),
                Level1 = result.Level1?.Trim(),
                Level2 = result.Level2?.Trim(),
                Locality = result.Locality?.Trim()
            };
        }
    }
}