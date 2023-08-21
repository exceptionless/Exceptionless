using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Pipeline;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Exceptionless.Core.Plugins.EventParser;

[Priority(0)]
public class JsonEventParserPlugin : PluginBase, IEventParserPlugin
{
    private readonly JsonSerializerSettings _settings;

    public JsonEventParserPlugin(AppOptions options, JsonSerializerSettings settings, ILoggerFactory loggerFactory) : base(options, loggerFactory)
    {
        _settings = settings;
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
                    if (input.TryFromJson(out PersistentEvent? ev, _settings) && ev is not null)
                        events.Add(ev);
                    break;
                }
            case JsonType.Array:
                {
                    if (input.TryFromJson(out PersistentEvent[]? parsedEvents, _settings) && parsedEvents is { Length: > 0 })
                        events.AddRange(parsedEvents);

                    break;
                }
        }

        return events.Count > 0 ? events : null;
    }
}
