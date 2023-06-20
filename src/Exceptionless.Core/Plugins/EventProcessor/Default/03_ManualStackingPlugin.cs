﻿using Exceptionless.Core.Extensions;
using Exceptionless.Core.Pipeline;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Plugins.EventProcessor;

[Priority(3)]
public sealed class ManualStackingPlugin : EventProcessorPluginBase
{
    public ManualStackingPlugin(AppOptions options, ILoggerFactory loggerFactory = null) : base(options, loggerFactory) { }

    public override Task EventProcessingAsync(EventContext context)
    {
        var msi = context.Event.GetManualStackingInfo();
        if (msi?.SignatureData != null)
        {
            foreach (var kvp in msi.SignatureData)
                context.StackSignatureData.AddItemIfNotEmpty(kvp.Key, kvp.Value);
        }

        return Task.CompletedTask;
    }
}
