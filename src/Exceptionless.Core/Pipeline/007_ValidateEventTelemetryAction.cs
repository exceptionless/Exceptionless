using Exceptionless.Core.Plugins.EventProcessor;
using Exceptionless.Core.Validation;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Pipeline;

/// <summary>Rejects invalid observations individually before stack assignment and batch persistence.</summary>
[Priority(7)]
public sealed class ValidateEventTelemetryAction(AppOptions options, ILoggerFactory loggerFactory) : EventPipelineActionBase(options, loggerFactory)
{
    public override Task ProcessAsync(EventContext ctx)
    {
        if (ctx.Event is { Outcome: null, Result: null, ParentReferenceId: null, RootReferenceId: null, Measurements: null, Labels: null })
            return Task.CompletedTask;

        var errors = EventTelemetryValidation.GetErrors(ctx.Event);
        if (errors.Count > 0)
        {
            var error = new MiniValidatorException(errors);
            ctx.SetError(error.Message, error);
        }
        return Task.CompletedTask;
    }
}
