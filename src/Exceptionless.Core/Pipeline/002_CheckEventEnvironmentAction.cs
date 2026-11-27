using Exceptionless.Core.Extensions;
using Exceptionless.Core.Plugins.EventProcessor;
using Exceptionless.Core.Services;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Pipeline;

/// <summary>
/// Discards events from environments the project does not list when the project asks for that. This runs before stacking so
/// rejected events never create stacks. Discarded events are counted as discarded usage by the caller, like stack discards.
/// </summary>
[Priority(2)]
public class CheckEventEnvironmentAction : EventPipelineActionBase
{
    private readonly DiscardedEnvironmentService _discardedEnvironments;

    public CheckEventEnvironmentAction(DiscardedEnvironmentService discardedEnvironments, AppOptions options, ILoggerFactory loggerFactory) : base(options, loggerFactory)
    {
        _discardedEnvironments = discardedEnvironments;
        ContinueOnError = true;
    }

    public override async Task ProcessBatchAsync(ICollection<EventContext> contexts)
    {
        var project = contexts.FirstOrDefault()?.Project;
        if (project is null || !project.DiscardUnlistedEnvironments)
            return;

        var discarded = new List<EventContext>();
        foreach (var ctx in contexts)
        {
            // Heartbeats and session ends never carry an environment. They only keep sessions alive and are never stored as events.
            if (ctx.Event.Environment is null && (ctx.Event.IsSessionHeartbeat() || ctx.Event.IsSessionEnd()))
                continue;

            if (!project.ShouldDiscardEnvironment(ctx.Event.Environment))
                continue;

            ctx.IsDiscarded = true;
            ctx.IsCancelled = true;
            discarded.Add(ctx);
        }

        if (discarded.Count == 0)
            return;

        _logger.LogInformation("Discarding {Count} events from environments that are not listed on the project", discarded.Count);

        try
        {
            await _discardedEnvironments.RecordAsync(project.Id, discarded.Select(c => c.Event.Environment));
        }
        catch (Exception ex)
        {
            // Visibility is best effort and must never fail ingestion.
            _logger.LogError(ex, "Error recording discarded environments: {Message}", ex.Message);
        }
    }

    public override Task ProcessAsync(EventContext ctx)
    {
        return Task.CompletedTask;
    }
}
