using System;

namespace Exceptionless.Core.Geo {
    public class Location {
        public double? Latitude { get; internal set; }
        public double? Longitude { get; internal set; }

        public bool IsValid() {
            if (!Latitude.HasValue || Latitude < -90.0 || Latitude > 90.0)
                return false;

            if (!Longitude.HasValue || Longitude < -180.0 || Longitude > 180.0)
                return false;

            return true;
        }

        public static bool TryParse(string input, out Location location) {
            location = null;
            if (String.IsNullOrWhiteSpace(input) || !input.Contains(","))
                return false;

            string[] parts = input.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2)
                return false;

            double latitude;
            if (!Double.TryParse(parts[0], out latitude))
                return false;

            double longitude;
            if (!Double.TryParse(parts[1], out longitude))
                return false;

            location = new Location { Latitude = latitude, Longitude = longitude };
            return true;
        }

        public override string ToString() {
            return Latitude + "," + Longitude;
        }
    }
}