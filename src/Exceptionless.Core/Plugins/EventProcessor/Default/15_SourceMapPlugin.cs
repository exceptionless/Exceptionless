using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Services.SourceMaps;
using Foundatio.Serializer;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Plugins.EventProcessor.Default;

[Priority(15)]
public sealed class SourceMapPlugin : EventProcessorPluginBase
{
    private readonly SourceMapService _sourceMapService;
    private readonly ITextSerializer _serializer;

    public SourceMapPlugin(SourceMapService sourceMapService, ITextSerializer serializer, AppOptions options, ILoggerFactory loggerFactory)
        : base(options, loggerFactory)
    {
        _sourceMapService = sourceMapService;
        _serializer = serializer;
        ContinueOnError = true;
    }

    public override async Task EventProcessingAsync(EventContext context)
    {
        if (!context.Event.IsError())
            return;

        var error = context.Event.GetError(_serializer, _logger);
        if (error is null)
            return;

        if (await _sourceMapService.SymbolicateAsync(context.Project.Id, error))
            context.Event.SetError(error);
    }
}
