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
            Data[kvp.Key] = JsonElementToObject(kvp.Value);
        }
        ExtensionData = null;
    }

    /// <summary>
    /// Converts a JsonElement to its native .NET type equivalent.
    /// Matches ObjectToInferredTypesConverter behavior: objects → Dictionary, arrays → List.
    /// </summary>
    private static object? JsonElementToObject(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => ReadString(element),
            JsonValueKind.Number => ReadNumber(element),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Array => element.EnumerateArray()
                .Select(JsonElementToObject)
                .ToList(),
            JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(p => p.Name, p => JsonElementToObject(p.Value)),
            _ => element.GetRawText()
        };
    }

    /// <summary>
    /// Reads a number from JsonElement, matching ObjectToInferredTypesConverter behavior.
    /// Returns smallest fitting integer type (int → long → decimal).
    /// </summary>
    private static object ReadNumber(JsonElement element)
    {
        // Check raw text for decimal point to preserve decimal vs integer representation
        string rawText = element.GetRawText();
        if (rawText.Contains('.') || rawText.Contains('e') || rawText.Contains('E'))
        {
            // Has decimal point or exponent - return decimal (default mode)
            return element.GetDecimal();
        }

        // No decimal point - integer. Try Int32 first, then Int64, then Decimal
        if (element.TryGetInt32(out int i))
            return i;

        if (element.TryGetInt64(out long l))
            return l;

        return element.GetDecimal();
    }

    /// <summary>
    /// Reads a string from JsonElement, attempting DateTimeOffset parsing for ISO 8601 dates.
    /// </summary>
    private static object? ReadString(JsonElement element)
    {
        if (element.TryGetDateTimeOffset(out DateTimeOffset dateTimeOffset))
            return dateTimeOffset;

        if (element.TryGetDateTime(out DateTime dt))
            return dt;

        return element.GetString();
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
