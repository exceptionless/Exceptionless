using System;
using System.Collections.Generic;
using Exceptionless.Core.Models.Collections;

namespace Exceptionless.Core.Models {
    public class SettingsDictionary : ObservableDictionary<string, string> {
        public SettingsDictionary() : base(StringComparer.OrdinalIgnoreCase) {}

        public SettingsDictionary(IEnumerable<KeyValuePair<string, string>> values) : base(StringComparer.OrdinalIgnoreCase) {
            foreach (var kvp in values)
                Add(kvp.Key, kvp.Value);
        }

        public string GetString(string name) {
            return GetString(name, String.Empty);
        }

        public string GetString(string name, string @default) {

            if (TryGetValue(name, out var value))
                return value;

            return @default;
        }

        public bool GetBoolean(string name) {
            return GetBoolean(name, false);
        }

        public bool GetBoolean(string name, bool @default) {

            bool result = TryGetValue(name, out var temp);
            if (!result)
                return @default;

            result = Boolean.TryParse(temp, out var value);
            return result ? value : @default;
        }

        public int GetInt32(string name) {
            return GetInt32(name, 0);
        }

        public int GetInt32(string name, int @default) {

            bool result = TryGetValue(name, out var temp);
            if (!result)
                return @default;

            result = Int32.TryParse(temp, out var value);
            return result ? value : @default;
        }

        public long GetInt64(string name) {
            return GetInt64(name, 0L);
        }

        public long GetInt64(string name, long @default) {

            bool result = TryGetValue(name, out var temp);
            if (!result)
                return @default;

            result = Int64.TryParse(temp, out var value);
            return result ? value : @default;
        }

        public double GetDouble(string name, double @default = 0d) {

            bool result = TryGetValue(name, out var temp);
            if (!result)
                return @default;

            result = Double.TryParse(temp, out var value);
            return result ? value : @default;
        }

        public DateTime GetDateTime(string name) {
            return GetDateTime(name, DateTime.MinValue);
        }

        public DateTime GetDateTime(string name, DateTime @default) {

            bool result = TryGetValue(name, out var temp);
            if (!result)
                return @default;

            result = DateTime.TryParse(temp, out var value);
            return result ? value : @default;
        }

        public DateTimeOffset GetDateTimeOffset(string name) {
            return GetDateTimeOffset(name, DateTimeOffset.MinValue);
        }

        public DateTimeOffset GetDateTimeOffset(string name, DateTimeOffset @default) {

            bool result = TryGetValue(name, out var temp);
            if (!result)
                return @default;

            result = DateTimeOffset.TryParse(temp, out var value);
            return result ? value : @default;
        }

        public Guid GetGuid(string name) {
            return GetGuid(name, Guid.Empty);
        }

        public Guid GetGuid(string name, Guid @default) {

            bool result = TryGetValue(name, out var temp);
            return result ? new Guid(temp) : @default;
        }

        public IEnumerable<string> GetStringCollection(string name) {
            return GetStringCollection(name, null);
        }

        public IEnumerable<string> GetStringCollection(string name, string @default) {
            string value = GetString(name, @default);

            if (String.IsNullOrEmpty(value))
                return new List<string>();

            var values = value.Split(new[] { ",", ";", Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < values.Length; i++)
                values[i] = values[i].Trim();

            var list = new List<string>(values);
            return list;
        }

        public void Apply(IEnumerable<KeyValuePair<string, string>> values) {
            if (values == null)
                return;

            foreach (var v in values) {
                if (!ContainsKey(v.Key) || v.Value != this[v.Key])
                    this[v.Key] = v.Value;
            }
        }

        public static class KnownKeys {
            public const string DataExclusions = "@@DataExclusions";
            public const string UserAgentBotPatterns = "@@UserAgentBotPatterns";
        }
    }
}