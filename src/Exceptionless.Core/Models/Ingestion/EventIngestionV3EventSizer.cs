using System.Text;
using System.Text.Json;

namespace Exceptionless.Core.Models.Ingestion;

public static class EventIngestionV3EventSizer
{
    public static long GetEstimatedSize(EventIngestionV3Event source)
    {
        long size = 128;
        size += GetSize(source.Id) + GetSize(source.Type) + GetSize(source.Source) + GetSize(source.Message);
        size += GetSize(source.ReferenceId) + GetSize(source.ExceptionType) + GetSize(source.StackTrace);
        size += GetSize(source.Version) + GetSize(source.Level);
        size += GetSize(source.Tags);
        size += GetSize(source.Data);
        if (source.Client is not null)
            size += 32 + GetSize(source.Client.Name) + GetSize(source.Client.Version);
        if (source.Stacking is not null)
            size += 32 + GetSize(source.Stacking.Title) + GetSize(source.Stacking.SignatureData);

        if (source.User is not null)
            size += 32 + GetSize(source.User.Identity) + GetSize(source.User.Name) + GetSize(source.User.Data);
        if (source.Request is not null)
        {
            size += 96 + GetSize(source.Request.UserAgent) + GetSize(source.Request.HttpMethod) + GetSize(source.Request.Host);
            size += GetSize(source.Request.Path) + GetSize(source.Request.Referrer) + GetSize(source.Request.ClientIpAddress);
            size += GetSize(source.Request.Headers) + GetSize(source.Request.Cookies) + GetSize(source.Request.QueryString);
            size += GetSize(source.Request.PostData) + GetSize(source.Request.Data);
        }
        if (source.Environment is not null)
        {
            size += 96 + GetSize(source.Environment.Architecture) + GetSize(source.Environment.OSName);
            size += GetSize(source.Environment.OSVersion) + GetSize(source.Environment.MachineName);
            size += GetSize(source.Environment.RuntimeVersion) + GetSize(source.Environment.ProcessName);
            size += GetSize(source.Environment.ProcessId) + GetSize(source.Environment.ThreadName);
            size += GetSize(source.Environment.ThreadId) + GetSize(source.Environment.Data);
        }

        return size;
    }

    private static int GetSize(string? value) => value is null ? 0 : Encoding.UTF8.GetByteCount(value);

    private static long GetSize(IEnumerable<string>? values)
    {
        if (values is null)
            return 0;
        long size = 0;
        foreach (string value in values)
            size += GetSize(value);
        return size;
    }

    private static long GetSize(IReadOnlyDictionary<string, string>? values)
    {
        if (values is null)
            return 0;
        long size = 0;
        foreach (var pair in values)
            size += GetSize(pair.Key) + GetSize(pair.Value);
        return size;
    }

    private static long GetSize(IReadOnlyDictionary<string, string[]>? values)
    {
        if (values is null)
            return 0;
        long size = 0;
        foreach (var pair in values)
            size += GetSize(pair.Key) + GetSize(pair.Value);
        return size;
    }

    private static long GetSize(JsonElement? element)
    {
        if (element is null)
            return 0;

        return GetSize(element.Value);
    }

    private static long GetSize(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            long size = 0;
            foreach (JsonProperty property in element.EnumerateObject())
                size += GetSize(property.Name) + GetSize(property.Value) + 4;
            return size;
        }
        if (element.ValueKind == JsonValueKind.Array)
        {
            long size = 0;
            foreach (JsonElement item in element.EnumerateArray())
                size += GetSize(item) + 1;
            return size;
        }

        return element.ValueKind switch
        {
            JsonValueKind.String => GetSize(element.GetString()),
            JsonValueKind.Number => 32,
            JsonValueKind.True or JsonValueKind.False => 5,
            JsonValueKind.Null or JsonValueKind.Undefined => 4,
            _ => 0
        };
    }
}
