using System.Text.Json;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Pipeline;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Plugins.EventParser;

[Priority(0)]
public class JsonEventParserPlugin : PluginBase, IEventParserPlugin
{
    private readonly JsonSerializerOptions _jsonOptions;

    public JsonEventParserPlugin(AppOptions options, JsonSerializerOptions jsonOptions, ILoggerFactory loggerFactory) : base(options, loggerFactory)
    {
        // Create lenient parsing options — inbound events from older SDK clients may omit
        // non-nullable properties. We must not reject structurally valid events; the pipeline
        // handles missing/null values gracefully downstream.
        _jsonOptions = new JsonSerializerOptions(jsonOptions) { RespectNullableAnnotations = false };
    }

    public List<PersistentEvent>? ParseEvents(string input, int apiVersion, string? userAgent)
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
                    var ev = JsonSerializer.Deserialize<PersistentEvent>(input, _jsonOptions);
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
                    var parsedEvents = JsonSerializer.Deserialize<PersistentEvent[]>(input, _jsonOptions);
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
