using System.Text.Json;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Pipeline;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Plugins.EventProcessor;

[Priority(10)]
public sealed class NotFoundPlugin : EventProcessorPluginBase
{
    private readonly JsonSerializerOptions _jsonOptions;

    public NotFoundPlugin(JsonSerializerOptions jsonOptions, AppOptions options, ILoggerFactory loggerFactory) : base(options, loggerFactory)
    {
        _jsonOptions = jsonOptions;
    }

    public override Task EventProcessingAsync(EventContext context)
    {
        if (context.Event.Type != Event.KnownTypes.NotFound || context.Event.Data is null)
            return Task.CompletedTask;

        context.Event.Data.Remove(Event.KnownDataKeys.EnvironmentInfo);
        context.Event.Data.Remove(Event.KnownDataKeys.TraceLog);

        var req = context.Event.GetRequestInfo(_jsonOptions);
        if (req is null)
            return Task.CompletedTask;

        if (String.IsNullOrWhiteSpace(context.Event.Source))
        {
            context.Event.Message = null;
            context.Event.Source = req.GetFullPath(includeHttpMethod: true, includeHost: false, includeQueryString: false);
        }

        context.Event.Data.Remove(Event.KnownDataKeys.Error);
        context.Event.Data.Remove(Event.KnownDataKeys.SimpleError);

        return Task.CompletedTask;
    }
}
