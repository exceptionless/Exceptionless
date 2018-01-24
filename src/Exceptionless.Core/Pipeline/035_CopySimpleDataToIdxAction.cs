using System;
using System.Threading.Tasks;
using Exceptionless.Core.AppStats;
using Exceptionless.Core.Plugins.EventProcessor;
using Foundatio.Metrics;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Pipeline {
    [Priority(40)]
    public class CopySimpleDataToIdxAction : EventPipelineActionBase {
        private readonly IMetricsClient _metricsClient;

        public CopySimpleDataToIdxAction(IMetricsClient metricsClient, ILoggerFactory loggerFactory = null) : base(loggerFactory) {
            _metricsClient = metricsClient;
        }

        public override Task ProcessAsync(EventContext ctx) {
            if (!ctx.Organization.HasPremiumFeatures)
                return Task.CompletedTask;

            // TODO: Do we need a pipeline action to trim keys and remove null values that may be sent by other native clients.
            ctx.Event.CopyDataToIndex(Array.Empty<string>());
            int fieldCount = ctx.Event.Idx.Count;
            _metricsClient.Gauge(MetricNames.EventsFieldCount, fieldCount);
            if (fieldCount > 20 && _logger.IsEnabled(LogLevel.Warning)) {
                var ev = ctx.Event;
                using (_logger.BeginScope(new ExceptionlessState().Organization(ctx.Organization.Id).Property("Event", new { ev.Date, ev.StackId, ev.Type, ev.Source, ev.Message, ev.Value, ev.Geo, ev.ReferenceId, ev.Tags, ev.Idx })))
                    _logger.LogWarning("Event has {FieldCount} indexed fields.", fieldCount);
            }

            return Task.CompletedTask;
        }
    }
}