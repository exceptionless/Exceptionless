using System;

namespace Exceptionless.Extensions {
    internal static class DateTimeExtensions {
        public static int ToEpoch(this DateTime fromDate) {
            var utc = (fromDate.ToUniversalTime().Ticks - 621355968000000000) / 10000000;
            return Convert.ToInt32(utc);
        }

        public static int ToEpochOffset(this DateTime date, int timestamp) {
            return timestamp - date.ToEpoch();
        }

        public static int ToEpoch(this DateTime date, int offset) {
            return offset + date.ToEpoch();
        }

        public static DateTime ToDateTime(this int secondsSinceEpoch) {
            return new DateTime(621355968000000000 + (secondsSinceEpoch * 10000000));
        }
    }
}
