using System;
using System.Linq;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;

namespace Exceptionless {
    public static class PersistentEventExtensions {
        public static void CopyDataToIndex(this PersistentEvent ev, params string[] keysToCopy) {
            keysToCopy = keysToCopy?.Length > 0 ? keysToCopy : ev.Data.Keys.ToArray();

            foreach (string key in keysToCopy.Where(k => !String.IsNullOrEmpty(k) && ev.Data.ContainsKey(k))) {
                string field = key.Trim().ToLower().Replace(' ', '-');
                if (field.StartsWith("@"))
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

        public static bool HasSessionEndTime(this PersistentEvent ev) {
            if (ev == null || !ev.IsSessionStart())
                return false;

            return ev.Data.ContainsKey(Event.KnownDataKeys.SessionEnd);
        }

        public static DateTime? GetSessionEndTime(this PersistentEvent ev) {
            if (ev == null || !ev.IsSessionStart())
                return null;

            object end;
            if (ev.Data.TryGetValue(Event.KnownDataKeys.SessionEnd, out end) && end is DateTime)
                return (DateTime)end;

            return null;
        }
        
        public static bool UpdateSessionStart(this PersistentEvent ev, DateTime lastActivityUtc, bool isSessionEnd = false, bool hasError = false) {
            if (ev == null || !ev.IsSessionStart())
                return false;

            decimal duration = ev.Value.GetValueOrDefault();
            if (duration < 0)
                duration = 0;
            
            decimal newDuration = (decimal)(lastActivityUtc - ev.Date.UtcDateTime).TotalSeconds;
            if (duration >= newDuration)
                lastActivityUtc = ev.Date.UtcDateTime.AddSeconds((double)duration);
            else
                duration = newDuration;

            ev.Value = duration;
            if (isSessionEnd) {
                ev.Data[Event.KnownDataKeys.SessionEnd] = lastActivityUtc;
                ev.CopyDataToIndex(Event.KnownDataKeys.SessionEnd);
            } else {
                ev.Data.Remove(Event.KnownDataKeys.SessionEnd);
                ev.Idx.Remove(Event.KnownDataKeys.SessionEnd + "-d");
            }

            return true;
        }

        public static PersistentEvent ToSessionStartEvent(this PersistentEvent source, DateTime? lastActivityUtc, bool? isSessionEnd, bool hasPremiumFeatures = true) {
            // TODO: Be selective about what data we copy.
            var startEvent = new PersistentEvent {
                SessionId = source.SessionId,
                Data = source.Data,
                Date = source.Date,
                Geo = source.Geo,
                OrganizationId = source.OrganizationId,
                ProjectId = source.ProjectId,
                Tags = source.Tags,
                Type = Event.KnownTypes.Session,
                Value = 0
            };

            if (lastActivityUtc.HasValue)
                startEvent.UpdateSessionStart(lastActivityUtc.Value, isSessionEnd.GetValueOrDefault());

            if (hasPremiumFeatures)
                startEvent.CopyDataToIndex();
            
            return startEvent;
        }

        public static PersistentEvent ToSessionEndEvent(this PersistentEvent source, string sessionId) {
            var endEvent = new PersistentEvent {
                SessionId = sessionId,
                Date = source.Date,
                OrganizationId = source.OrganizationId,
                ProjectId = source.ProjectId,
                Type = Event.KnownTypes.SessionEnd
            };
            
            endEvent.SetUserIdentity(source.GetUserIdentity());
            endEvent.AddRequestInfo(source.GetRequestInfo());

            return endEvent;
        }
    }
}