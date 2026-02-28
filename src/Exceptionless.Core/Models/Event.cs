using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Exceptionless.Core.Extensions;

namespace Exceptionless.Core.Models;

[DebuggerDisplay("Type: {Type}, Date: {Date}, Message: {Message}, Value: {Value}, Count: {Count}")]
public class Event : IData, IJsonOnDeserialized
{
    /// <summary>
    /// The event type (ie. error, log message, feature usage). Check <see cref="KnownTypes">Event.KnownTypes</see> for standard event types.
    /// </summary>
    [StringLength(100)]
    public string? Type { get; set; }

    /// <summary>
    /// The event source (ie. machine name, log name, feature name).
    /// </summary>
    [StringLength(2000)]
    public string? Source { get; set; }

    /// <summary>
    /// The date that the event occurred on.
    /// </summary>
    public DateTimeOffset Date { get; set; }

    /// <summary>
    /// A list of tags used to categorize this event.
    /// </summary>
    public TagSet? Tags { get; set; } = [];

    /// <summary>
    /// The event message.
    /// </summary>
    [StringLength(2000)]
    public string? Message { get; set; }

    /// <summary>
    /// The geo coordinates where the event happened.
    /// </summary>
    public string? Geo { get; set; }

    /// <summary>
    /// The value of the event if any.
    /// </summary>
    public decimal? Value { get; set; }

    /// <summary>
    /// The number of duplicated events.
    /// </summary>
    public int? Count { get; set; }

    /// <summary>
    /// Optional data entries that contain additional information about this event.
    /// </summary>
    public DataDictionary? Data { get; set; } = new();

    /// <summary>
    /// Captures unknown JSON properties during deserialization.
    /// These are merged into <see cref="Data"/> after deserialization.
    /// Known data keys like "@error", "@request", "@environment" may appear at root level.
    /// </summary>
    [JsonExtensionData]
    [JsonInclude]
    internal Dictionary<string, JsonElement>? ExtensionData { get; set; }

    /// <summary>
    /// An optional identifier to be used for referencing this event instance at a later time.
    /// </summary>
    public string? ReferenceId { get; set; }

    /// <summary>
    /// Called after JSON deserialization to merge extension data into the Data dictionary.
    /// This handles the case where known data keys like "@error", "@request", "@environment"
    /// appear at the JSON root level instead of nested under "data".
    /// </summary>
    void IJsonOnDeserialized.OnDeserialized()
    {
        if (ExtensionData is null || ExtensionData.Count == 0)
            return;

        Data ??= new DataDictionary();
        foreach (var kvp in ExtensionData)
        {
            Data[kvp.Key] = ConvertJsonElement(kvp.Value);
        }
        ExtensionData = null;
    }

    /// <summary>
    /// Converts a <see cref="JsonElement"/> to a .NET type so downstream code
    /// (e.g., <c>value as string</c>) works correctly.
    /// </summary>
    private static object? ConvertJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out long l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            // For objects/arrays, keep as JsonElement — GetValue<T> handles these
            _ => element
        };
    }

    protected bool Equals(Event other)
    {
        return String.Equals(Type, other.Type) && String.Equals(Source, other.Source) && Tags.CollectionEquals(other.Tags) && String.Equals(Message, other.Message) && String.Equals(Geo, other.Geo) && Value == other.Value && Equals(Data, other.Data);
    }

    public override bool Equals(object? obj)
    {
        if (obj is null)
            return false;
        if (ReferenceEquals(this, obj))
            return true;
        if (obj.GetType() != GetType())
            return false;
        return Equals((Event)obj);
    }

    private static readonly List<string> _exclusions = [KnownDataKeys.TraceLog];
    public override int GetHashCode()
    {
        unchecked
        {
            int hashCode = Type?.GetHashCode() ?? 0;
            hashCode = (hashCode * 397) ^ (Source?.GetHashCode() ?? 0);
            hashCode = (hashCode * 397) ^ (Tags?.GetCollectionHashCode() ?? 0);
            hashCode = (hashCode * 397) ^ (Message?.GetHashCode() ?? 0);
            hashCode = (hashCode * 397) ^ (Geo?.GetHashCode() ?? 0);
            hashCode = (hashCode * 397) ^ Value.GetHashCode();
            hashCode = (hashCode * 397) ^ (Data?.GetCollectionHashCode(_exclusions) ?? 0);
            return hashCode;
        }
    }

    public static class KnownTypes
    {
        public const string Error = "error";
        public const string FeatureUsage = "usage";
        public const string Log = "log";
        public const string NotFound = "404";
        public const string Session = "session";
        public const string SessionEnd = "sessionend";
        public const string SessionHeartbeat = "heartbeat";
    }

    public static class KnownTags
    {
        public const string Critical = "Critical";
        public const string Internal = "Internal";
    }

    public static class KnownDataKeys
    {
        public const string Error = "@error";
        public const string SimpleError = "@simple_error";
        public const string RequestInfo = "@request";
        public const string TraceLog = "@trace";
        public const string EnvironmentInfo = "@environment";
        public const string UserInfo = "@user";
        public const string UserDescription = "@user_description";
        public const string Version = "@version";
        public const string Level = "@level";
        public const string Location = "@location";
        public const string SubmissionMethod = "@submission_method";
        public const string SubmissionClient = "@submission_client";
        public const string SessionEnd = "sessionend";
        public const string SessionHasError = "haserror";
        public const string ManualStackingInfo = "@stack";
    }
}
