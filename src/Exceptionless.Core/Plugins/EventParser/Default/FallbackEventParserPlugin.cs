using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Pipeline;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Plugins.EventParser;

[Priority(Int32.MaxValue)]
public class FallbackEventParserPlugin : PluginBase, IEventParserPlugin
{
    private readonly TimeProvider _timeProvider;

    public FallbackEventParserPlugin(
        TimeProvider timeProvider, AppOptions options, ILoggerFactory loggerFactory) : base(options, loggerFactory)
    {
        _timeProvider = timeProvider;
    }

    public List<PersistentEvent>? ParseEvents(string input, int apiVersion, string? userAgent)
    {
        var events = input.SplitLines().Select(entry => new PersistentEvent
        {
            Date = _timeProvider.GetLocalNow(),
            Type = "log",
            Message = entry
        }).ToList();

        return events.Count > 0 ? events : null;
    }
}
