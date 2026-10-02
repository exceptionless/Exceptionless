using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Serialization;
using MiniValidation;

namespace Exceptionless.Core.Models;

[DebuggerDisplay("Type: {Type}, Date: {Date}, Message: {Message}, Value: {Value}, Count: {Count}")]
public class Event : IData, IJsonOnDeserialized
{
    /// <summary>
    /// The event type (ie. error, log message, feature usage). Check <see cref="KnownTypes">Event.KnownTypes</see> for standard event types.
    /// Nullable in transit; the pipeline infers a default before save. Validated as required on repository save.
    /// </summary>
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string? Type { get; set; }

    /// <summary>
    /// The event source (ie. machine name, log name, feature name).
    /// </summary>
    [StringLength(2000, MinimumLength = 1)]
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
    [StringLength(2000, MinimumLength = 1)]
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

    /// <summary>The result of this operation, independent of the event type.</summary>
    [StringLength(100, MinimumLength = 1)]
    public string? Outcome { get; set; }

    /// <summary>Reference of the immediate parent event in the same project.</summary>
    public string? ParentReferenceId { get; set; }

    /// <summary>Reference of the root event in the same project. Supplied by the producer.</summary>
    public string? RootReferenceId { get; set; }

    /// <summary>Up to 32 uniquely named numeric observations. Missing values are not zero.</summary>
    [MaxLength(32)]
    public List<EventMeasurement>? Measurements { get; set; }

    /// <summary>Up to 32 categorical labels, indexed as exact strings without type inference.</summary>
    [MaxLength(32), SkipRecursion]
    public Dictionary<string, string>? Dimensions { get; set; }

    /// <summary>
    /// Optional data entries that contain additional information about this event.
    /// </summary>
    [SkipRecursion]
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
        EventDataNormalizer.Normalize(Data);

        if (ExtensionData is { Count: > 0 })
        {
            // STJ with SnakeCaseLower policy only matches case-insensitively against the
            // policy-transformed name "reference_id". PascalCase "ReferenceId" and camelCase
            // "referenceId" are entirely different strings (not just different casing) so they
            // go to ExtensionData. Required for older .NET SDK versions (< 5.x) that submit
            // events with PascalCase/camelCase property names.
            if (ReferenceId is null &&
                (ExtensionData.Remove("ReferenceId", out var refIdElement) ||
                 ExtensionData.Remove("referenceId", out refIdElement)))
            {
                ReferenceId = refIdElement.GetString();
            }

            Data ??= [];
            foreach (var kvp in ExtensionData)
            {
                if (EventTelemetryReader.TryRead(this, kvp.Key, kvp.Value))
                    continue;

                object? value = JsonElementConverter.Convert(kvp.Value);
                EventDataNormalizer.Set(Data, kvp.Key, value);
            }

            ExtensionData = null;
        }
    }

    protected bool Equals(Event other)
    {
        return String.Equals(Type, other.Type) && String.Equals(Source, other.Source) && Tags.CollectionEquals(other.Tags) && String.Equals(Message, other.Message) && String.Equals(Geo, other.Geo) && Value == other.Value && Equals(Data, other.Data)
            && Outcome == other.Outcome && ParentReferenceId == other.ParentReferenceId && RootReferenceId == other.RootReferenceId
            && (Measurements ?? []).OrderBy(m => m?.Name, StringComparer.Ordinal).SequenceEqual((other.Measurements ?? []).OrderBy(m => m?.Name, StringComparer.Ordinal))
            && (Dimensions ?? []).OrderBy(d => d.Key, StringComparer.Ordinal).SequenceEqual((other.Dimensions ?? []).OrderBy(d => d.Key, StringComparer.Ordinal));
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
            hashCode = (hashCode * 397) ^ HashCode.Combine(Outcome, ParentReferenceId, RootReferenceId);
            foreach (var measurement in (Measurements ?? []).OrderBy(m => m?.Name, StringComparer.Ordinal))
                hashCode = (hashCode * 397) ^ (measurement?.GetHashCode() ?? 0);
            foreach (var dimension in (Dimensions ?? []).OrderBy(d => d.Key, StringComparer.Ordinal))
                hashCode = (hashCode * 397) ^ dimension.GetHashCode();
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
