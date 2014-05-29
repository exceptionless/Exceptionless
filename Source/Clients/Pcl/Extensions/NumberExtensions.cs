using System;

namespace Exceptionless.Extensions {
    public static class NumberExtensions {
        public static string ToFileSizeDisplay(this int i, int decimals = 2) {
            return ToFileSizeDisplay((long)i, decimals);
        }

        public static string ToFileSizeDisplay(this long i, int decimals = 2) {
            if (i < 1024 * 1024 * 1024) // 1 GB
            {
                string value = Math.Round((decimal)i / 1024m / 1024m, decimals).ToString("N" + decimals);
                if (decimals > 0 && value.EndsWith(new string('0', decimals)))
                    value = value.Substring(0, value.Length - decimals - 1);

                return String.Concat(value, " MB");
            } else {
                string value = Math.Round((decimal)i / 1024m / 1024m / 1024m, decimals).ToString("N" + decimals);
                if (decimals > 0 && value.EndsWith(new string('0', decimals)))
                    value = value.Substring(0, value.Length - decimals - 1);

                return String.Concat(value, " GB");
            }
        }
    }
}
