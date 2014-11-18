using System;
using System.Threading;
using CodeSmith.Core.Security;

namespace CodeSmith.Core.Helpers {
    public static class RandomHelper {
        private static int _seed;

        private static readonly ThreadLocal<Random> _threadLocal = new ThreadLocal<Random>
            (() => new Random(Interlocked.Increment(ref _seed)));

        static RandomHelper() {
            _seed = Environment.TickCount;
        }

        public static Random Instance { get { return _threadLocal.Value; } }

        public static int GetRange(int min, int max) {
            return Instance.Next(min, max);
        }

        public static long GetLongRange(long min, long max) {
            var buf = new byte[8];
            Instance.NextBytes(buf);
            long longRand = BitConverter.ToInt64(buf, 0);

            return (Math.Abs(longRand % (max - min)) + min);
        }

        public static DateTime GetDateTime(DateTime? start = null, DateTime? end = null) {
            if (start.HasValue && end.HasValue && start.Value >= end.Value)
                throw new Exception("Start date must be less than end date.");

            DateTime min = start ?? DateTime.MinValue;
            DateTime max = end ?? DateTime.MaxValue;

            TimeSpan timeSpan = max - min;
            var newSpan = new TimeSpan(GetLongRange(0, timeSpan.Ticks));

            return min + newSpan;
        }

        public static DateTimeOffset GetDateTimeOffset(DateTimeOffset? start = null, DateTimeOffset? end = null) {
            if (start.HasValue && end.HasValue && start.Value >= end.Value)
                throw new Exception("Start date must be less than end date.");

            DateTimeOffset min = start ?? DateTimeOffset.MinValue;
            DateTimeOffset max = end ?? DateTimeOffset.MaxValue;

            TimeSpan timeSpan = max - min;
            var newSpan = new TimeSpan(GetLongRange(0, timeSpan.Ticks));

            return min + newSpan;
        }

        public static bool GetBool() {
            return Instance.NextDouble() > 0.5;
        }

        public static string GetIp4Address() {
            return String.Concat(GetRange(0, 255), ".", GetRange(0, 255), ".", GetRange(0, 255), ".", GetRange(0, 255));
        }

        public static string GetPronouncableString(int length) {
            return PasswordGenerator.GeneratePassword(length);
        }
    }
}
