#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using ServiceStack.CacheAccess;

namespace Exceptionless.Extensions {
    public static class CacheClientExtensions {
        public static T TryGet<T>(this ICacheClient client, string key) {
            return TryGet<T>(client, key, default(T));
        }

        public static T TryGet<T>(this ICacheClient client, string key, T defaultValue) {
            try {
                return client.Get<T>(key);
            } catch (Exception) {
                return defaultValue;
            }
        }

        public static long Increment(this ICacheClient client, string key, uint value, TimeSpan timeToLive, uint? startingValue = null) {
            if (!startingValue.HasValue)
                startingValue = 0;

            var count = client.Get<long?>(key);
            if (count.HasValue)
                return client.Increment(key, value);

            client.Set(key, startingValue + value, timeToLive);
            return value;
        }
    }
}