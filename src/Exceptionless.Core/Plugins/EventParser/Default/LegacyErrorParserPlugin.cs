using System.Text.Json;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Plugins.EventUpgrader;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Plugins.EventParser;

[Priority(10)]
public class LegacyErrorParserPlugin : PluginBase, IEventParserPlugin
{
    private readonly EventUpgraderPluginManager _manager;
    private readonly JsonSerializerOptions _jsonOptions;

    public LegacyErrorParserPlugin(EventUpgraderPluginManager manager, JsonSerializerOptions jsonOptions, AppOptions appOptions, ILoggerFactory loggerFactory) : base(appOptions, loggerFactory)
    {
        _manager = manager;
        _jsonOptions = jsonOptions;
    }

    public List<PersistentEvent>? ParseEvents(string input, int apiVersion, string? userAgent)
    {
        if (apiVersion != 1)
            return null;

        try
        {
            var ctx = new EventUpgraderContext(input);
            _manager.Upgrade(ctx);

            return ctx.Documents.ToList<PersistentEvent>(_jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing event: {Message}", ex.Message);
            return null;
        }
    }
}
