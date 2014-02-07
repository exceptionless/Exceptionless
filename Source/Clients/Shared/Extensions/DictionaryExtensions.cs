#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.Collections.Generic;

namespace Exceptionless.Extensions {
    internal static class DictionaryExtensions {
        public static string GetString(this IDictionary<string, string> dictionary, string name) {
            return GetString(dictionary, name, String.Empty);
        }

        public static string GetString(this IDictionary<string, string> dictionary, string name, string @default) {
            string value;

            if (dictionary.TryGetValue(name, out value))
                return value;

            return @default;
        }

        public static bool GetBoolean(this IDictionary<string, string> dictionary, string name) {
            return GetBoolean(dictionary, name, false);
        }

        public static bool GetBoolean(this IDictionary<string, string> dictionary, string name, bool @default) {
            bool value;
            string temp;

            bool result = dictionary.TryGetValue(name, out temp);
            if (!result)
                return @default;

            result = bool.TryParse(temp, out value);
            return result ? value : @default;
        }

        public static int GetInt32(this IDictionary<string, string> dictionary, string name) {
            return GetInt32(dictionary, name, 0);
        }

        public static int GetInt32(this IDictionary<string, string> dictionary, string name, int @default) {
            int value;
            string temp;

            bool result = dictionary.TryGetValue(name, out temp);
            if (!result)
                return @default;

            result = int.TryParse(temp, out value);
            return result ? value : @default;
        }

        public static long GetInt64(this IDictionary<string, string> dictionary, string name) {
            return GetInt64(dictionary, name, 0L);
        }

        public static long GetInt64(this IDictionary<string, string> dictionary, string name, long @default) {
            long value;
            string temp;

            bool result = dictionary.TryGetValue(name, out temp);
            if (!result)
                return @default;

            result = long.TryParse(temp, out value);
            return result ? value : @default;
        }

        public static double GetDouble(this IDictionary<string, string> dictionary, string name) {
            return GetDouble(dictionary, name, 0d);
        }

        public static double GetDouble(this IDictionary<string, string> dictionary, string name, double @default) {
            double value;
            string temp;

            bool result = dictionary.TryGetValue(name, out temp);
            if (!result)
                return @default;

            result = double.TryParse(temp, out value);
            return result ? value : @default;
        }

        public static DateTime GetDateTime(this IDictionary<string, string> dictionary, string name) {
            return GetDateTime(dictionary, name, DateTime.MinValue);
        }

        public static DateTime GetDateTime(this IDictionary<string, string> dictionary, string name, DateTime @default) {
            DateTime value;
            string temp;

            bool result = dictionary.TryGetValue(name, out temp);
            if (!result)
                return @default;

            result = DateTime.TryParse(temp, out value);
            return result ? value : @default;
        }

        public static DateTimeOffset GetDateTimeOffset(this IDictionary<string, string> dictionary, string name) {
            return GetDateTimeOffset(dictionary, name, DateTimeOffset.MinValue);
        }

        public static DateTimeOffset GetDateTimeOffset(this IDictionary<string, string> dictionary, string name, DateTimeOffset @default) {
            DateTimeOffset value;
            string temp;

            bool result = dictionary.TryGetValue(name, out temp);
            if (!result)
                return @default;

            result = DateTimeOffset.TryParse(temp, out value);
            return result ? value : @default;
        }

        public static Guid GetGuid(this IDictionary<string, string> dictionary, string name) {
            return GetGuid(dictionary, name, Guid.Empty);
        }

        public static Guid GetGuid(this IDictionary<string, string> dictionary, string name, Guid @default) {
            string temp;

            bool result = dictionary.TryGetValue(name, out temp);
            return result ? new Guid(temp) : @default;
        }
    }
}