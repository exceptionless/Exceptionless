using Exceptionless.Core.Plugins.EventProcessor;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Pipeline;

[Priority(5)]
public class RunEventProcessingPluginsAction : EventPipelineActionBase
{
    private readonly EventPluginManager _pluginManager;

    public RunEventProcessingPluginsAction(EventPluginManager pluginManager, AppOptions options, ILoggerFactory loggerFactory) : base(options, loggerFactory)
    {
        _pluginManager = pluginManager;
        ContinueOnError = true;
    }

    public override Task ProcessBatchAsync(ICollection<EventContext> contexts)
    {
        return _pluginManager.EventBatchProcessingAsync(contexts);
    }
}
