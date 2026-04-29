using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Pipeline;
using Foundatio.Serializer;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Plugins.EventProcessor;

[Priority(10)]
public sealed class NotFoundPlugin : EventProcessorPluginBase
{
    private readonly ITextSerializer _serializer;

    public NotFoundPlugin(ITextSerializer serializer, AppOptions options, ILoggerFactory loggerFactory) : base(options, loggerFactory)
    {
        _serializer = serializer;
    }

    public override Task EventProcessingAsync(EventContext context)
    {
        if (context.Event.Type != Event.KnownTypes.NotFound || context.Event.Data is null)
            return Task.CompletedTask;

        context.Event.Data.Remove(Event.KnownDataKeys.EnvironmentInfo);
        context.Event.Data.Remove(Event.KnownDataKeys.TraceLog);

        var req = context.Event.GetRequestInfo(_serializer);
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
