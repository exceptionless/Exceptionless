using System;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Models;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Plugins.EventProcessor {
    [Priority(10)]
    public sealed class NotFoundPlugin : EventProcessorPluginBase {
        public NotFoundPlugin(ILoggerFactory loggerFactory = null) : base(loggerFactory) {}

        public override Task EventProcessingAsync(EventContext context) {
            if (context.Event.Type != Event.KnownTypes.NotFound)
                return Task.CompletedTask;

            context.Event.Data.Remove(Event.KnownDataKeys.EnvironmentInfo);
            context.Event.Data.Remove(Event.KnownDataKeys.TraceLog);

            var req = context.Event.GetRequestInfo();
            if (req == null)
                return Task.CompletedTask;

            if (String.IsNullOrWhiteSpace(context.Event.Source)) {
                context.Event.Message = null;
                context.Event.Source = req.GetFullPath(includeHttpMethod: true, includeHost: false, includeQueryString: false);
            }

            context.Event.Data.Remove(Event.KnownDataKeys.Error);
            context.Event.Data.Remove(Event.KnownDataKeys.SimpleError);

            return Task.CompletedTask;
        }
    }
}