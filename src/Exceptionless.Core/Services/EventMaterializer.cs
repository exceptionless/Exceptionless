using System.Text.Json;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Models.Ingestion;
using Exceptionless.Core.Serialization;

namespace Exceptionless.Core.Services;

public interface IEventMaterializer
{
    PersistentEvent Materialize(EventIngestionV3Event source, StackFingerprint fingerprint, Organization organization, Project project);
}

public sealed class EventMaterializer(StackTraceParser stackTraceParser, TimeProvider timeProvider) : IEventMaterializer
{
    public PersistentEvent Materialize(EventIngestionV3Event source, StackFingerprint fingerprint, Organization organization, Project project)
    {
        var ev = new PersistentEvent
        {
            Type = source.Type,
            Source = source.Source,
            Date = source.Date ?? timeProvider.GetUtcNow(),
            CreatedUtc = timeProvider.GetUtcNow().UtcDateTime,
            Message = source.Message,
            ReferenceId = source.ReferenceId,
            Value = source.Value,
            Tags = source.Tags is null ? [] : new TagSet(source.Tags),
            Data = ConvertData(source.Data),
            OrganizationId = organization.Id,
            ProjectId = project.Id
        };

        if (String.Equals(source.Type, Event.KnownTypes.Error, StringComparison.OrdinalIgnoreCase))
        {
            ev.SetError(String.IsNullOrEmpty(source.StackTrace)
                ? new Error { Type = source.ExceptionType, Message = source.Message, StackTrace = [] }
                : stackTraceParser.ParseError(source.StackTrace, source.ExceptionType, source.Message));
        }

        if (String.Equals(source.Type, Event.KnownTypes.Error, StringComparison.OrdinalIgnoreCase) || source.Stacking is not null)
        {
            if (String.IsNullOrWhiteSpace(fingerprint.Title))
                ev.SetManualStackingInfo(fingerprint.SignatureData.ToDictionary(pair => pair.Key, pair => pair.Value));
            else
                ev.SetManualStackingInfo(fingerprint.Title, fingerprint.SignatureData.ToDictionary(pair => pair.Key, pair => pair.Value));
        }

        if (!String.IsNullOrWhiteSpace(source.Version))
            ev.SetVersion(source.Version);
        if (!String.IsNullOrWhiteSpace(source.Level))
            ev.SetLevel(source.Level.Trim());
        if (source.Client is not null)
        {
            ev.SetSubmissionClient(new SubmissionClient
            {
                UserAgent = source.Client.Name.Trim(),
                Version = source.Client.Version.Trim()
            });
        }

        if (source.User is not null)
        {
            ev.SetUserIdentity(new UserInfo(source.User.Identity ?? String.Empty, source.User.Name)
            {
                Data = ConvertData(source.User.Data)
            });
        }

        if (source.Request is not null)
            ev.Data![Event.KnownDataKeys.RequestInfo] = Map(source.Request);

        if (source.Environment is not null)
            ev.SetEnvironmentInfo(Map(source.Environment));

        if (timeProvider.GetUtcNow().UtcDateTime < ev.Date.UtcDateTime)
            ev.Date = timeProvider.GetUtcNow();

        ev.Tags?.RemoveExcessTags();
        ev.Message = String.IsNullOrWhiteSpace(ev.Message) ? null : ev.Message.Truncate(2000);
        ev.Source = String.IsNullOrWhiteSpace(ev.Source) ? null : ev.Source.Truncate(2000);
        if (!ev.HasValidReferenceId())
        {
            ev.Data["InvalidReferenceId"] = ev.ReferenceId;
            ev.ReferenceId = "invalid-reference-id";
        }

        bool includePrivateInformation = project.Configuration.Settings.GetBoolean(SettingsDictionary.KnownKeys.IncludePrivateInformation, true);
        if (!includePrivateInformation)
        {
            ev.RemoveUserIdentity();
            if (ev.Data.TryGetValue(Event.KnownDataKeys.RequestInfo, out object? requestValue) && requestValue is RequestInfo request)
            {
                request.ClientIpAddress = null;
                request.Cookies?.Clear();
                request.PostData = null;
                request.QueryString?.Clear();
            }

            if (ev.Data.TryGetValue(Event.KnownDataKeys.EnvironmentInfo, out object? environmentValue) && environmentValue is EnvironmentInfo environment)
                environment.MachineName = null;
        }

        if (organization.HasPremiumFeatures)
            ev.CopyDataToIndex([]);

        return ev;
    }

    private static RequestInfo Map(EventIngestionV3Request source) => new()
    {
        UserAgent = source.UserAgent,
        HttpMethod = source.HttpMethod,
        IsSecure = source.IsSecure,
        Host = source.Host,
        Port = source.Port,
        Path = source.Path,
        Referrer = source.Referrer,
        ClientIpAddress = source.ClientIpAddress,
        Headers = source.Headers,
        Cookies = source.Cookies,
        QueryString = source.QueryString,
        PostData = source.PostData.HasValue ? JsonElementConverter.Convert(source.PostData.Value) : null,
        Data = ConvertData(source.Data)
    };

    private static EnvironmentInfo Map(EventIngestionV3Environment source) => new()
    {
        Architecture = source.Architecture,
        OSName = source.OSName,
        OSVersion = source.OSVersion,
        MachineName = source.MachineName,
        RuntimeVersion = source.RuntimeVersion,
        ProcessName = source.ProcessName,
        ProcessId = source.ProcessId,
        ThreadName = source.ThreadName,
        ThreadId = source.ThreadId,
        ProcessorCount = source.ProcessorCount,
        TotalPhysicalMemory = source.TotalPhysicalMemory,
        AvailablePhysicalMemory = source.AvailablePhysicalMemory,
        ProcessMemorySize = source.ProcessMemorySize,
        Data = ConvertData(source.Data)
    };

    private static DataDictionary ConvertData(JsonElement? element)
    {
        if (element is not { ValueKind: JsonValueKind.Object } value)
            return [];

        var data = new DataDictionary();
        foreach (JsonProperty property in value.EnumerateObject())
            data[property.Name] = JsonElementConverter.Convert(property.Value);
        return data;
    }
}
