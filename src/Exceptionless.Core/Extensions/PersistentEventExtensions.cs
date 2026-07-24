using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Foundatio.Serializer;
using Microsoft.Extensions.Logging;

namespace Exceptionless;

public static class PersistentEventExtensions
{
    private static readonly char[] _commaSeparator = [','];

    public static string? GetEventReference(this PersistentEvent ev, string name)
    {
        if (String.IsNullOrEmpty(name) || ev.Data is null)
            return null;

        return ev.Data.GetString($"@ref:{name}");
    }

    /// <summary>
    /// Allows you to reference a parent event by it's <seealso cref="Event.ReferenceId" /> property. This allows you to have parent and child relationships.
    /// </summary>
    /// <param name="ev">The event</param>
    /// <param name="name">Reference name</param>
    /// <param name="id">The reference id that points to a specific event</param>
    public static void SetEventReference(this PersistentEvent ev, string name, string id)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        if (!IsValidIdentifier(id) || String.IsNullOrEmpty(id))
            throw new ArgumentException("Id must contain between 8 and 100 alphanumeric or '-' characters.", nameof(id));

        ev.Data ??= new DataDictionary();
        ev.Data[$"@ref:{name}"] = id;
    }

    public static string? GetSessionId(this PersistentEvent ev)
    {
        return ev.IsSessionStart() ? ev.ReferenceId : ev.GetEventReference("session");
    }

    public static void SetSessionId(this PersistentEvent ev, string sessionId)
    {
        if (!IsValidIdentifier(sessionId) || String.IsNullOrEmpty(sessionId))
            throw new ArgumentException("Session Id must contain between 8 and 100 alphanumeric or '-' characters.", nameof(sessionId));

        if (ev.IsSessionStart())
            ev.ReferenceId = sessionId;
        else
            ev.SetEventReference("session", sessionId);
    }

    public static bool HasSessionEndTime(this PersistentEvent ev)
    {
        if (!ev.IsSessionStart())
            return false;

        return ev.Data is not null && ev.Data.ContainsKey(Event.KnownDataKeys.SessionEnd);
    }

    public static DateTime? GetSessionEndTime(this PersistentEvent ev)
    {
        if (!ev.IsSessionStart())
            return null;

        if (ev.Data is not null && ev.Data.TryGetValue(Event.KnownDataKeys.SessionEnd, out object? sessionEnd))
        {
            if (sessionEnd is DateTimeOffset dto)
                return dto.UtcDateTime;

            if (sessionEnd is DateTime dt)
                return dt;
        }

        return null;
    }

    public static bool UpdateSessionStart(this PersistentEvent ev, DateTime lastActivityUtc, bool isSessionEnd = false, bool hasError = false)
    {
        if (!ev.IsSessionStart())
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

        ev.Data ??= new DataDictionary();
        if (isSessionEnd)
        {
            ev.Data[Event.KnownDataKeys.SessionEnd] = lastActivityUtc;
        }
        else
        {
            ev.Data.Remove(Event.KnownDataKeys.SessionEnd);
        }

        if (hasError)
            ev.Data[Event.KnownDataKeys.SessionHasError] = true;

        return true;
    }

    public static PersistentEvent ToSessionStartEvent(this PersistentEvent source, ITextSerializer serializer, ILogger logger, DateTime? lastActivityUtc = null, bool? isSessionEnd = null, bool includePrivateInformation = true, bool hasError = false)
    {
        var startEvent = new PersistentEvent
        {
            Date = source.Date,
            Geo = source.Geo,
            OrganizationId = source.OrganizationId,
            ProjectId = source.ProjectId,
            Type = Event.KnownTypes.Session,
            Value = 0
        };

        string? sessionId = source.GetSessionId();
        if (sessionId is not null)
            startEvent.SetSessionId(sessionId);
        if (includePrivateInformation)
            startEvent.SetUserIdentity(source.GetUserIdentity(serializer, logger));
        startEvent.SetLocation(source.GetLocation(serializer, logger));
        startEvent.SetVersion(source.GetVersion());

        var ei = source.GetEnvironmentInfo(serializer, logger);
        if (ei is not null)
        {
            startEvent.SetEnvironmentInfo(new EnvironmentInfo
            {
                Architecture = ei.Architecture,
                CommandLine = ei.CommandLine,
                Data = ei.Data,
                InstallId = ei.InstallId,
                IpAddress = includePrivateInformation ? ei.IpAddress : null,
                MachineName = includePrivateInformation ? ei.MachineName : null,
                OSName = ei.OSName,
                OSVersion = ei.OSVersion,
                ProcessId = ei.ProcessId,
                ProcessName = ei.ProcessName,
                ProcessorCount = ei.ProcessorCount,
                RuntimeVersion = ei.RuntimeVersion,
                TotalPhysicalMemory = ei.TotalPhysicalMemory
            });
        }

        var ri = source.GetRequestInfo(serializer, logger);
        if (ri is not null)
        {
            startEvent.AddRequestInfo(new RequestInfo
            {
                ClientIpAddress = includePrivateInformation ? ri.ClientIpAddress : null,
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
            startEvent.UpdateSessionStart(lastActivityUtc.Value, isSessionEnd.GetValueOrDefault(), hasError);

        return startEvent;
    }

    public static IEnumerable<string> GetIpAddresses(this PersistentEvent ev, ITextSerializer serializer, ILogger logger)
    {
        if (!String.IsNullOrEmpty(ev.Geo) && (ev.Geo.Contains('.') || ev.Geo.Contains(':')))
            yield return ev.Geo.Trim();

        var ri = ev.GetRequestInfo(serializer, logger);
        if (!String.IsNullOrEmpty(ri?.ClientIpAddress))
        {
            foreach (string ip in ri.ClientIpAddress.Split(_commaSeparator, StringSplitOptions.RemoveEmptyEntries))
                yield return ip.Trim();
        }

        var ei = ev.GetEnvironmentInfo(serializer, logger);
        if (!String.IsNullOrEmpty(ei?.IpAddress))
        {
            foreach (string ip in ei.IpAddress.Split(_commaSeparator, StringSplitOptions.RemoveEmptyEntries))
                yield return ip.Trim();
        }
    }

    public static bool HasValidReferenceId(this PersistentEvent ev)
    {
        return IsValidIdentifier(ev.ReferenceId);
    }

    private static bool IsValidIdentifier(string? value)
    {
        if (value is null)
            return true;

        if (value.Length is < 8 or > 100)
            return false;

        return value.IsValidIdentifier();
    }
}
