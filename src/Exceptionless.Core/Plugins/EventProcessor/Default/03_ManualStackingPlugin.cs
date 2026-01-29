using System.Text.Json;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Pipeline;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Plugins.EventProcessor;

[Priority(3)]
public sealed class ManualStackingPlugin : EventProcessorPluginBase
{
    private readonly JsonSerializerOptions _jsonOptions;

    public ManualStackingPlugin(JsonSerializerOptions jsonOptions, AppOptions options, ILoggerFactory loggerFactory) : base(options, loggerFactory)
    {
        _jsonOptions = jsonOptions;
    }

    public override Task EventProcessingAsync(EventContext context)
    {
        var msi = context.Event.GetManualStackingInfo(_jsonOptions);
        if (msi?.SignatureData is not null)
        {
            foreach (var kvp in msi.SignatureData)
                context.StackSignatureData.AddItemIfNotEmpty(kvp.Key, kvp.Value);
        }

        return Task.CompletedTask;
    }
}
