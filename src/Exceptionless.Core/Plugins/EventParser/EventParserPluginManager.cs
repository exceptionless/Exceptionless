using Exceptionless.Core.Models;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Plugins.EventParser;

public class EventParserPluginManager : PluginManagerBase<IEventParserPlugin>
{
    public EventParserPluginManager(IServiceProvider serviceProvider, AppOptions options, ILoggerFactory loggerFactory) : base(serviceProvider, options, loggerFactory) { }

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

                // Set required event properties
                events.ForEach(e =>
                {
                    if (e.Date == DateTimeOffset.MinValue)
                        e.Date = _timeProvider.GetLocalNow();

                    if (String.IsNullOrWhiteSpace(e.Type))
                        e.Type = e.HasError() || e.HasSimpleError() ? Event.KnownTypes.Error : Event.KnownTypes.Log;
                });

                return events;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling ParseEvents in plugin {PluginName}: {Message}", plugin.Name, ex.Message);
            }
        }

        return new List<PersistentEvent>();
    }
}
