using System;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;

namespace Exceptionless {
    public static class PersistentEventExtensions {
        public static void CopyDataToIndex(this PersistentEvent ev, string[] keysToCopy) {
            keysToCopy = keysToCopy?.Length > 0 ? keysToCopy : ev.Data.Keys.ToArray();

            foreach (string key in keysToCopy.Where(k => !String.IsNullOrEmpty(k) && ev.Data.ContainsKey(k))) {
                string field = key.Trim().ToLowerInvariant();

                if (field.StartsWith("@ref:")) {
                    field = field.Substring(5);
                    if (!field.IsValidFieldName())
                        continue;

                    ev.Idx[field + "-r"] = (string)ev.Data[key];
                    continue;
                }

                if (field.StartsWith("@") || ev.Data[key] == null)
                    continue;

                if (!field.IsValidFieldName())
                    continue;

                var dataType = ev.Data[key].GetType();
                if (dataType == typeof(bool)) {
                    ev.Idx[field + "-b"] = ev.Data[key];
                } else if (dataType.IsNumeric()) {
                    ev.Idx[field + "-n"] = ev.Data[key];
                } else if (dataType == typeof(DateTime) || dataType == typeof(DateTimeOffset)) {
                    ev.Idx[field + "-d"] = ev.Data[key];
                } else if (dataType == typeof(string)) {
                    string input = (string)ev.Data[key];
                    if (String.IsNullOrEmpty(input) || input.Length >= 1000)
                        continue;

                    if (input.GetJsonType() != JsonType.None)
                        continue;

                    if (input[0] == '"')
                        input = input.TrimStart('"').TrimEnd('"');

                    if (Boolean.TryParse(input, out var value))
                        ev.Idx[field + "-b"] = value;
                    else if (DateTimeOffset.TryParse(input, out var dtoValue))
                        ev.Idx[field + "-d"] = dtoValue;
                    else if (Decimal.TryParse(input, out var decValue))
                        ev.Idx[field + "-n"] = decValue;
                    else if (Double.TryParse(input, out var dblValue))
                        ev.Idx[field + "-n"] = dblValue;
                    else
                        ev.Idx[field + "-s"] = input;
                }
            }
        }

        public static string GetEventReference(this PersistentEvent ev, string name) {
            if (ev == null || String.IsNullOrEmpty(name))
                return null;

            return ev.Data.GetString($"@ref:{name}");
        }

        /// <summary>
        /// Allows you to reference a parent event by it's <seealso cref="Event.ReferenceId" /> property. This allows you to have parent and child relationships.
        /// </summary>
        /// <param name="ev">The event</param>
        /// <param name="name">Reference name</param>
        /// <param name="id">The reference id that points to a specific event</param>
        public static void SetEventReference(this PersistentEvent ev, string name, string id) {
            if (String.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name));

            if (!IsValidIdentifier(id) || String.IsNullOrEmpty(id))
                throw new ArgumentException("Id must contain between 8 and 100 alphanumeric or '-' characters.", nameof(id));

            ev.Data[$"@ref:{name}"] = id;
        }

        public static string GetSessionId(this PersistentEvent ev) {
            if (ev == null)
                return null;

            return ev.IsSessionStart() ? ev.ReferenceId : ev.GetEventReference("session");
        }

        public static void SetSessionId(this PersistentEvent ev, string sessionId) {
            if (ev == null)
                return;

            if (!IsValidIdentifier(sessionId) || String.IsNullOrEmpty(sessionId))
                throw new ArgumentException("Session Id must contain between 8 and 100 alphanumeric or '-' characters.", nameof(sessionId));

            if (ev.IsSessionStart())
                ev.ReferenceId = sessionId;
            else
                ev.SetEventReference("session", sessionId);
        }

        public static bool HasSessionEndTime(this PersistentEvent ev) {
            if (ev == null || !ev.IsSessionStart())
                return false;

            return ev.Data.ContainsKey(Event.KnownDataKeys.SessionEnd);
        }

        public static DateTime? GetSessionEndTime(this PersistentEvent ev) {
            if (ev == null || !ev.IsSessionStart())
                return null;

            if (ev.Data.TryGetValue(Event.KnownDataKeys.SessionEnd, out var end) && end is DateTime)
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
                ev.CopyDataToIndex(new [] { Event.KnownDataKeys.SessionEnd });
            } else {
                ev.Data.Remove(Event.KnownDataKeys.SessionEnd);
                ev.Idx.Remove(Event.KnownDataKeys.SessionEnd + "-d");
            }

            return true;
        }

        public static PersistentEvent ToSessionStartEvent(this PersistentEvent source, DateTime? lastActivityUtc = null, bool? isSessionEnd = null, bool hasPremiumFeatures = true) {
            var startEvent = new PersistentEvent {
                Date = source.Date,
                Geo = source.Geo,
                OrganizationId = source.OrganizationId,
                ProjectId = source.ProjectId,
                Type = Event.KnownTypes.Session,
                Value = 0
            };

            startEvent.SetSessionId(source.GetSessionId());
            startEvent.SetUserIdentity(source.GetUserIdentity());
            startEvent.SetLocation(source.GetLocation());
            startEvent.SetVersion(source.GetVersion());

            var ei = source.GetEnvironmentInfo();
            if (ei != null) {
                startEvent.SetEnvironmentInfo(new EnvironmentInfo {
                    Architecture = ei.Architecture,
                    CommandLine = ei.CommandLine,
                    Data = ei.Data,
                    InstallId = ei.InstallId,
                    IpAddress = ei.IpAddress,
                    MachineName = ei.MachineName,
                    OSName = ei.OSName,
                    OSVersion = ei.OSVersion,
                    ProcessId = ei.ProcessId,
                    ProcessName = ei.ProcessName,
                    ProcessorCount = ei.ProcessorCount,
                    RuntimeVersion = ei.RuntimeVersion,
                    TotalPhysicalMemory = ei.TotalPhysicalMemory
                });
            }

            var ri = source.GetRequestInfo();
            if (ri != null) {
                startEvent.AddRequestInfo(new RequestInfo {
                    ClientIpAddress = ri.ClientIpAddress,
                    Data = ri.Data,
                    Host = ri.Host,
                    HttpMethod = ri.HttpMethod,
                    IsSecure = ri.IsSecure,
                    Port = ri.Port,
                    Path = ri.Path,
                    Referrer = ri.Referrer,
                    UserAgent = ri.UserAgent
                });
            }

            if (lastActivityUtc.HasValue)
                startEvent.UpdateSessionStart(lastActivityUtc.Value, isSessionEnd.GetValueOrDefault());

            if (hasPremiumFeatures)
                startEvent.CopyDataToIndex(Array.Empty<string>());

            return startEvent;
        }

        public static IEnumerable<string> GetIpAddresses(this PersistentEvent ev) {
            if (ev == null)
                yield break;

            if (!String.IsNullOrEmpty(ev.Geo) && (ev.Geo.Contains(".") || ev.Geo.Contains(":")))
                yield return ev.Geo.Trim();

            var ri = ev.GetRequestInfo();
            if (!String.IsNullOrEmpty(ri?.ClientIpAddress)) {
                foreach (string ip in ri.ClientIpAddress.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                    yield return ip.Trim();
            }

            var ei = ev.GetEnvironmentInfo();
            if (!String.IsNullOrEmpty(ei?.IpAddress)) {
                foreach (string ip in ei.IpAddress.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                    yield return ip.Trim();
            }
        }

        public static bool HasValidReferenceId(this PersistentEvent ev) {
            return IsValidIdentifier(ev.ReferenceId);
        }

        private static bool IsValidIdentifier(string value) {
            if (value == null)
                return true;

            if (value.Length < 8 || value.Length > 100)
                return false;

            return value.IsValidIdentifier();
        }
    }
}