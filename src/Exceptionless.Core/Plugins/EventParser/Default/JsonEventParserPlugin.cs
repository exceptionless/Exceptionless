using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Serialization;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Plugins.EventParser;

[Priority(0)]
public class JsonEventParserPlugin : PluginBase, IEventParserPlugin
{
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly JsonSerializerOptions _normalizedJsonOptions;

    public JsonEventParserPlugin(AppOptions options, JsonSerializerOptions jsonOptions, ILoggerFactory loggerFactory) : base(options, loggerFactory)
    {
        // Create lenient parsing options — inbound events from older SDK clients may omit
        // non-nullable properties. We must not reject structurally valid events; the pipeline
        // handles missing/null values gracefully downstream.
        _normalizedJsonOptions = new JsonSerializerOptions(jsonOptions) { RespectNullableAnnotations = false };
        _jsonOptions = new JsonSerializerOptions(_normalizedJsonOptions)
        {
            // Preserve the original root value once, at ingestion. Repeating this during
            // storage or API deserialization would accumulate duplicate custom data.
            TypeInfoResolver = (jsonOptions.TypeInfoResolver ?? new DefaultJsonTypeInfoResolver())
                .WithAddedModifier(EventEnvironmentConverter.ConfigureIngestionProperty)
        };
    }

    public List<PersistentEvent>? ParseEvents(string input, int apiVersion, string? userAgent)
    {
        return ParseEvents(input, apiVersion, _jsonOptions);
    }

    internal List<PersistentEvent>? ParseNormalizedEvents(string input)
    {
        return ParseEvents(input, 2, _normalizedJsonOptions);
    }

    private List<PersistentEvent>? ParseEvents(string input, int apiVersion, JsonSerializerOptions jsonOptions)
    {
        if (apiVersion < 2)
            return null;

        var events = new List<PersistentEvent>();
        switch (input.GetJsonType())
        {
            case JsonType.Object:
            {
                try
                {
                    var ev = JsonSerializer.Deserialize<PersistentEvent>(input, jsonOptions);
                    if (ev is not null)
                        events.Add(ev);
                }
                catch (JsonException ex)
                {
                    // Deserialization failed — the payload is valid JSON but cannot be mapped
                    // to PersistentEvent (e.g. unexpected structure from an unknown SDK version).
                    _logger.LogDebug(ex, "Failed to deserialize event object from input");
                }
                break;
            }
            case JsonType.Array:
            {
                try
                {
                    var parsedEvents = JsonSerializer.Deserialize<PersistentEvent[]>(input, jsonOptions);
                    if (parsedEvents is { Length: > 0 })
                        events.AddRange(parsedEvents.Where(e => e is not null));
                }
                catch (JsonException ex)
                {
                    // Deserialization failed — the payload is valid JSON but cannot be mapped
                    // to PersistentEvent[] (e.g. unexpected structure from an unknown SDK version).
                    _logger.LogDebug(ex, "Failed to deserialize event array from input");
                }
                break;
            }
        }

        return events.Count > 0 ? events : null;
    }
}
