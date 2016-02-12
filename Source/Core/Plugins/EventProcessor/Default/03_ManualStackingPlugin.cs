using System;
using System.Threading.Tasks;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;

namespace Exceptionless.Core.Plugins.EventProcessor {
    [Priority(3)]
    public class ManualStackingPlugin : EventProcessorPluginBase {
        public override Task EventProcessingAsync(EventContext context) {
            var data = context.Event.Data;
            if (data.ContainsKey(Event.KnownDataKeys.ManualStackingKey) && data[Event.KnownDataKeys.ManualStackingKey] != null)
                context.StackSignatureData.AddItemIfNotEmpty(nameof(Event.KnownDataKeys.ManualStackingKey), data[Event.KnownDataKeys.ManualStackingKey].ToString());

            return Task.CompletedTask;
        }
    }
}