using Exceptionless.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Plugins.EventParser;

public class EventParserPluginManager : PluginManagerBase<IEventParserPlugin>
{
    private readonly TimeProvider _timeProvider;

    public EventParserPluginManager(IServiceProvider serviceProvider, AppOptions options,
        TimeProvider timeProvider, ILoggerFactory loggerFactory) : base(serviceProvider, options, loggerFactory)
    {
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Runs through the formatting plugins to calculate an html summary for the stack based on the event data.
    /// </summary>
    public List<PersistentEvent> ParseEvents(string input, int apiVersion, string? userAgent)
    {
        string metricPrefix = "events.parse.";
        foreach (var plugin in Plugins.Values.ToList())
        {
            string metricName = String.Concat(metricPrefix, plugin.Name.ToLower());

            try
            {
                List<PersistentEvent>? events = null;
                AppDiagnostics.Time(() => events = plugin.ParseEvents(input, apiVersion, userAgent), metricName);
                if (events is null)
                    continue;

                SetRequiredProperties(events);

                return events;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling ParseEvents in plugin {PluginName}: {Message}", plugin.Name, ex.Message);
            }
        }

        return new List<PersistentEvent>();
    }

    public List<PersistentEvent> ParseNormalizedEvents(string input)
    {
        var parser = _serviceProvider.GetRequiredService<JsonEventParserPlugin>();
        List<PersistentEvent>? events = null;
        AppDiagnostics.Time(() => events = parser.ParseNormalizedEvents(input), String.Concat("events.parse.", parser.Name.ToLower()));
        events ??= [];
        SetRequiredProperties(events);
        return events;
    }

    private void SetRequiredProperties(List<PersistentEvent> events)
    {
        foreach (var ev in events)
        {
            if (ev.Date == DateTimeOffset.MinValue)
                ev.Date = _timeProvider.GetLocalNow();

            if (String.IsNullOrWhiteSpace(ev.Type))
                ev.Type = ev.HasError() || ev.HasSimpleError() ? Event.KnownTypes.Error : Event.KnownTypes.Log;
        }
    }
}
