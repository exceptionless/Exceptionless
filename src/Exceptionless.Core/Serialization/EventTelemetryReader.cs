using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;
using Exceptionless.Core.Models;

namespace Exceptionless.Core.Serialization;

/// <summary>Preserves older clients' unstructured root properties when they do not use the new native shapes.</summary>
internal static partial class EventTelemetryReader
{
    private static readonly JsonSerializerOptions _options = new() { PropertyNameCaseInsensitive = true };
    private static readonly HashSet<string> _fields = new(StringComparer.OrdinalIgnoreCase)
    {
        "outcome", "result", "parent_reference_id", "root_reference_id", "measurements", "labels"
    };

    // Only the inbound parser uses this modifier. Native models retain their typed API and storage contracts.
    public static void CaptureTelemetryProperties(JsonTypeInfo typeInfo)
    {
        if (!typeof(Event).IsAssignableFrom(typeInfo.Type))
            return;

        for (int i = typeInfo.Properties.Count - 1; i >= 0; i--)
            if (_fields.Contains(typeInfo.Properties[i].Name))
                typeInfo.Properties.RemoveAt(i);
    }

    public static bool TryRead(Event ev, string name, JsonElement value)
    {
        try
        {
            switch (name.Replace("_", "").ToLowerInvariant())
            {
                case "outcome" when value.ValueKind == JsonValueKind.String && value.GetString() is Event.KnownOutcomes.Success or Event.KnownOutcomes.Failure or Event.KnownOutcomes.Unknown:
                    ev.Outcome = value.GetString();
                    return true;
                case "result" when value.ValueKind == JsonValueKind.String:
                    ev.Result = value.GetString();
                    return true;
                case "parentreferenceid" when value.ValueKind == JsonValueKind.String && IsReference(value.GetString()):
                    ev.ParentReferenceId = value.GetString();
                    return true;
                case "rootreferenceid" when value.ValueKind == JsonValueKind.String && IsReference(value.GetString()):
                    ev.RootReferenceId = value.GetString();
                    return true;
                case "measurements" when value.ValueKind == JsonValueKind.Array && value.EnumerateArray().All(IsMeasurement):
                    ev.Measurements = value.Deserialize<List<EventMeasurement>>(_options);
                    return true;
                case "labels" when value.ValueKind == JsonValueKind.Object && value.EnumerateObject().All(property => property.Value.ValueKind == JsonValueKind.String):
                    ev.Labels = value.Deserialize<Dictionary<string, string>>(_options);
                    return true;
            }
        }
        catch (JsonException)
        {
            // This property was previously extension data; preserve its original value.
        }

        return false;
    }

    public static bool IsReference(string? value) => value is not null && ReferencePattern().IsMatch(value);

    private static bool IsMeasurement(JsonElement element) => element.ValueKind == JsonValueKind.Null ||
        (element.ValueKind == JsonValueKind.Object && element.EnumerateObject().Any(property =>
            property.Name.Equals("name", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Equals("value", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Equals("unit", StringComparison.OrdinalIgnoreCase)));

    [GeneratedRegex("^[a-zA-Z0-9-]{8,100}$")]
    private static partial Regex ReferencePattern();
}
