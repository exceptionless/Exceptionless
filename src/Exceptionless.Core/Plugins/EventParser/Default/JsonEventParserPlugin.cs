using System.Text.Json;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Pipeline;
using Foundatio.Serializer;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Plugins.EventParser;

[Priority(0)]
public class JsonEventParserPlugin : PluginBase, IEventParserPlugin
{
    private readonly ITextSerializer _serializer;

    public JsonEventParserPlugin(AppOptions options, ITextSerializer serializer, ILoggerFactory loggerFactory) : base(options, loggerFactory)
    {
        _serializer = serializer;
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
                    var ev = _serializer.Deserialize<PersistentEvent>(input);
                    if (ev is not null)
                        events.Add(ev);
                }
                catch (JsonException)
                {
                    // Invalid JSON - ignore
                }
                break;
            }
            case JsonType.Array:
            {
                try
                {
                    var parsedEvents = _serializer.Deserialize<PersistentEvent[]>(input);
                    if (parsedEvents is { Length: > 0 })
                        events.AddRange(parsedEvents);
                }
                catch (JsonException)
                {
                    // Invalid JSON - ignore
                }
                break;
            }
        }

        return events.Count > 0 ? events : null;
    }
}
