using System;

namespace Exceptionless.Core.Geo {
    public class GeoResult {
        public double? Latitude { get; internal set; }
        public double? Longitude { get; internal set; }
        
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
            if (String.IsNullOrEmpty(input) || !input.Contains(","))
                return false;

            string[] parts = input.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2)
                return false;

            double latitude;
            if (!Double.TryParse(parts[0]?.Trim(), out latitude))
                return false;

            double longitude;
            if (!Double.TryParse(parts[1]?.Trim(), out longitude))
                return false;

            result = new GeoResult { Latitude = latitude, Longitude = longitude };
            return true;
        }

        public override string ToString() {
            return Latitude + "," + Longitude;
        }
    }
}