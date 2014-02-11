#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using Exceptionless.Configuration;

namespace Exceptionless.Extensions {
    public static class ClientConfigurationExtensions {
        public static string GetString(this ClientConfiguration configuration, string name) {
            return GetString(configuration, name, String.Empty);
        }

        public static string GetString(this ClientConfiguration configuration, string name, string @default) {
            string value;

            if (configuration.TryGetValue(name, out value))
                return value;

            return @default;
        }

        public static bool TryGetValue(this ClientConfiguration configuration, string name, out string value) {
            value = null;
            if (!configuration.ContainsKey(name))
                return false;

            value = configuration[name];
            return true;
        }

        public static bool GetBoolean(this ClientConfiguration configuration, string name) {
            return GetBoolean(configuration, name, false);
        }

        public static bool GetBoolean(this ClientConfiguration configuration, string name, bool @default) {
            bool value;
            string temp;

            bool result = configuration.TryGetValue(name, out temp);
            if (!result)
                return @default;

            result = bool.TryParse(temp, out value);
            return result ? value : @default;
        }

        public static int GetInt32(this ClientConfiguration configuration, string name) {
            return GetInt32(configuration, name, 0);
        }

        public static int GetInt32(this ClientConfiguration configuration, string name, int @default) {
            int value;
            string temp;

            bool result = configuration.TryGetValue(name, out temp);
            if (!result)
                return @default;

            result = int.TryParse(temp, out value);
            return result ? value : @default;
        }

        public static long GetInt64(this ClientConfiguration configuration, string name) {
            return GetInt64(configuration, name, 0L);
        }

        public static long GetInt64(this ClientConfiguration configuration, string name, long @default) {
            long value;
            string temp;

            bool result = configuration.TryGetValue(name, out temp);
            if (!result)
                return @default;

            result = long.TryParse(temp, out value);
            return result ? value : @default;
        }

        public static double GetDouble(this ClientConfiguration configuration, string name, double @default = 0d) {
            double value;
            string temp;

            bool result = configuration.TryGetValue(name, out temp);
            if (!result)
                return @default;

            result = double.TryParse(temp, out value);
            return result ? value : @default;
        }

        public static DateTime GetDateTime(this ClientConfiguration configuration, string name) {
            return GetDateTime(configuration, name, DateTime.MinValue);
        }

        public static DateTime GetDateTime(this ClientConfiguration configuration, string name, DateTime @default) {
            DateTime value;
            string temp;

            bool result = configuration.TryGetValue(name, out temp);
            if (!result)
                return @default;

            result = DateTime.TryParse(temp, out value);
            return result ? value : @default;
        }

        public static DateTimeOffset GetDateTimeOffset(this ClientConfiguration configuration, string name) {
            return GetDateTimeOffset(configuration, name, DateTimeOffset.MinValue);
        }

        public static DateTimeOffset GetDateTimeOffset(this ClientConfiguration configuration, string name, DateTimeOffset @default) {
            DateTimeOffset value;
            string temp;

            bool result = configuration.TryGetValue(name, out temp);
            if (!result)
                return @default;

            result = DateTimeOffset.TryParse(temp, out value);
            return result ? value : @default;
        }

        public static Guid GetGuid(this ClientConfiguration configuration, string name) {
            return GetGuid(configuration, name, Guid.Empty);
        }

        public static Guid GetGuid(this ClientConfiguration configuration, string name, Guid @default) {
            string temp;

            bool result = configuration.TryGetValue(name, out temp);
            return result ? new Guid(temp) : @default;
        }
    }
}