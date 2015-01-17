using System;

namespace Exceptionless.Core.Extensions {
    public static class NumericExtensions {
        public static string ToFileSizeDisplay(this int i) {
            return ToFileSizeDisplay((long)i, 2);
        }

        public static string ToFileSizeDisplay(this int i, int decimals) {
            return ToFileSizeDisplay((long)i, decimals);
        }

        public static string ToFileSizeDisplay(this long i) {
            return ToFileSizeDisplay(i, 2);
        }

        public static string ToFileSizeDisplay(this long i, int decimals) {
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

        public static TimeSpan Min(this TimeSpan source, TimeSpan other) {
            return source.Ticks > other.Ticks ? other : source;
        }

        public static TimeSpan Max(this TimeSpan source, TimeSpan other) {
            return source.Ticks < other.Ticks ? other : source;
        }

        public static string ToOrdinal(this int num) {
            switch (num % 100) {
            case 11:
            case 12:
            case 13:
                return num.ToString("#,###0") + "th";
            }

            switch (num % 10) {
            case 1:
                return num.ToString("#,###0") + "st";
            case 2:
                return num.ToString("#,###0") + "nd";
            case 3:
                return num.ToString("#,###0") + "rd";
            default:
                return num.ToString("#,###0") + "th";
            }
        }
    }
}
