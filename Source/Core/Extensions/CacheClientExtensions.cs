using System;
using Foundatio.Caching;

namespace Exceptionless.Extensions {
    public static class CacheClientExtensions {
        public static T TryGet<T>(this ICacheClient client, string key) {
            return TryGet(client, key, default(T));
        }

        public static T TryGet<T>(this ICacheClient client, string key, T defaultValue) {
            T value;
            if (client.TryGet(key, out value))
                return value;

            return defaultValue;
        }

        public static bool TrySet<T>(this ICacheClient client, string key, T value) {
            try {
                return client.Set(key, value);
            } catch (Exception) {
                return false;
            }
        }

        public static bool TrySet<T>(this ICacheClient client, string key, T value, DateTime expiresAt) {
            try {
                return client.Set(key, value, expiresAt);
            } catch (Exception) {
                return false;
            }
        }

        public static bool TrySet<T>(this ICacheClient client, string key, T value, TimeSpan expiresIn) {
            try {
                return client.Set(key, value, expiresIn);
            } catch (Exception) {
                return false;
            }
        }

        public static long IncrementIf(this ICacheClient client, string key, uint value, TimeSpan timeToLive, bool shouldIncrement, long? startingValue = null) {
            if (!startingValue.HasValue)
                startingValue = 0;

            var count = client.Get<long?>(key);
            if (!shouldIncrement)
                return count.HasValue ? count.Value : startingValue.Value;

            if (count.HasValue)
                return client.Increment(key, value);

            long newValue = startingValue.Value + value;
            client.Set(key, newValue, timeToLive);
            return newValue;
        }

        public static long Increment(this ICacheClient client, string key, uint value, TimeSpan timeToLive, long? startingValue = null) {
            if (!startingValue.HasValue)
                startingValue = 0;

            var count = client.Get<long?>(key);
            if (count.HasValue)
                return client.Increment(key, value);

            long newValue = startingValue.Value + value;
            client.Set(key, newValue, timeToLive);
            return newValue;
        }
    }
}