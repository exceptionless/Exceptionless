using Exceptionless.Core.Plugins.EventProcessor;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Pipeline;

[Priority(40)]
public class CopySimpleDataToIdxAction : EventPipelineActionBase {
    public CopySimpleDataToIdxAction(AppOptions options, ILoggerFactory loggerFactory = null) : base(options, loggerFactory) {}

    public override Task ProcessAsync(EventContext ctx) {
        if (!ctx.Organization.HasPremiumFeatures)
            return Task.CompletedTask;

        // TODO: Do we need a pipeline action to trim keys and remove null values that may be sent by other native clients.
        ctx.Event.CopyDataToIndex(Array.Empty<string>());
        int fieldCount = ctx.Event.Idx.Count;
        AppDiagnostics.EventsFieldCount.Record(fieldCount);
        if (fieldCount > 20 && _logger.IsEnabled(LogLevel.Warning)) {
            var ev = ctx.Event;
            using (_logger.BeginScope(new ExceptionlessState().Organization(ctx.Organization.Id).Property("Event", new { ev.Date, ev.StackId, ev.Type, ev.Source, ev.Message, ev.Value, ev.Geo, ev.ReferenceId, ev.Tags, ev.Idx })))
                _logger.LogWarning("Event has {FieldCount} indexed fields.", fieldCount);
        }

        return Task.CompletedTask;
    }
}
