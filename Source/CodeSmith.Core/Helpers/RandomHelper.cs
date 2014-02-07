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

        public static DateTime GetDateTime(DateTime? minimum = null, DateTime? maximum = null) {
            if (!minimum.HasValue)
                minimum = DateTime.MinValue;
            if (!maximum.HasValue)
                maximum = DateTime.MaxValue;

            return new DateTime(GetLongRange(minimum.Value.Ticks, maximum.Value.Ticks));
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
