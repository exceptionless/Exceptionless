using System;
using System.Linq;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;

namespace Exceptionless {
    public static class PersistentEventExtensions {
        public static void CopyDataToIndex(this PersistentEvent ev, params string[] keysToCopy) {
            keysToCopy = keysToCopy?.Where(k => !String.IsNullOrEmpty(k)).ToArray();
            keysToCopy = keysToCopy?.Length > 0 ? keysToCopy : ev.Data.Keys.Where(k => !String.IsNullOrEmpty(k) && !k.StartsWith("@")).ToArray();

            foreach (string key in keysToCopy) {
                string field = key.Trim().ToLower().Replace(' ', '-');
                if (field.StartsWith("@") || ev.Data[key] == null)
                    continue;

                Type dataType = ev.Data[key].GetType();
                if (dataType == typeof(bool)) {
                    ev.Idx[field + "-b"] = ev.Data[key];
                } else if (dataType.IsNumeric()) {
                    ev.Idx[field + "-n"] = ev.Data[key];
                } else if (dataType == typeof(DateTime) || dataType == typeof(DateTimeOffset)) {
                    ev.Idx[field + "-d"] = ev.Data[key];
                } else if (dataType == typeof(string)) {
                    var input = (string)ev.Data[key];
                    if (String.IsNullOrEmpty(input) || input.Length >= 1000)
                        continue;

                    if (input.GetJsonType() != JsonType.None)
                        continue;

                    if (input[0] == '"')
                        input = input.TrimStart('"').TrimEnd('"');

                    bool value;
                    DateTimeOffset dtoValue;
                    Decimal decValue;
                    Double dblValue;
                    if (Boolean.TryParse(input, out value))
                        ev.Idx[field + "-b"] = value;
                    else if (DateTimeOffset.TryParse(input, out dtoValue))
                        ev.Idx[field + "-d"] = dtoValue;
                    else if (Decimal.TryParse(input, out decValue))
                        ev.Idx[field + "-n"] = decValue;
                    else if (Double.TryParse(input, out dblValue))
                        ev.Idx[field + "-n"] = dblValue;
                    else
                        ev.Idx[field + "-s"] = input;
                }
            }
        }

        public static bool UpdateSessionStart(this PersistentEvent ev, DateTime lastActivityUtc, bool isSessionEnd = false) {
            if (ev == null || !ev.IsSessionStart())
                return false;

            var duration = (decimal)(lastActivityUtc - ev.Date.UtcDateTime).TotalSeconds;
            if ((duration < 0 || ev.Value.GetValueOrDefault() >= duration) && ev.Value.HasValue && !isSessionEnd)
                return true;

            ev.Value = duration;
            if (isSessionEnd) {
                ev.Data[Event.KnownDataKeys.SessionEnd] = lastActivityUtc;
                ev.CopyDataToIndex(Event.KnownDataKeys.SessionEnd);
            }

            return true;
        }
    }
}