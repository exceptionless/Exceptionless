using Exceptionless.Core.Extensions;
using Exceptionless.Core.Pipeline;
using Foundatio.Serializer;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Plugins.EventProcessor;

[Priority(3)]
public sealed class ManualStackingPlugin : EventProcessorPluginBase
{
    private readonly ITextSerializer _serializer;

    public ManualStackingPlugin(ITextSerializer serializer, AppOptions options, ILoggerFactory loggerFactory) : base(options, loggerFactory)
    {
        _serializer = serializer;
    }

    public override Task EventProcessingAsync(EventContext context)
    {
        var msi = context.Event.GetManualStackingInfo(_serializer);
        if (msi?.SignatureData is not null)
        {
            foreach (var kvp in msi.SignatureData)
                context.StackSignatureData.AddItemIfNotEmpty(kvp.Key, kvp.Value);
        }

        return Task.CompletedTask;
    }
}
