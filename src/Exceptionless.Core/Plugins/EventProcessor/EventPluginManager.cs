using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Plugins.EventProcessor;

public class EventPluginManager : PluginManagerBase<IEventProcessorPlugin>
{
    public EventPluginManager(IServiceProvider serviceProvider, AppOptions options, ILoggerFactory loggerFactory) : base(serviceProvider, options, loggerFactory) { }

    /// <summary>
    /// Runs all the event plugins startup method.
    /// </summary>
    public async Task StartupAsync()
    {
        foreach (var plugin in Plugins.Values.ToList())
        {
            try
            {
                string metricName = String.Concat(_metricPrefix, plugin.Name.ToLower());
                await AppDiagnostics.TimeAsync(() => plugin.StartupAsync(), metricName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling startup in plugin {PluginName}: {Message}", plugin.Name, ex.Message);
            }
        }
    }

    /// <summary>
    /// Runs all the event plugins event processing method.
    /// </summary>
    public async Task EventBatchProcessingAsync(ICollection<EventContext> contexts)
    {
        foreach (var plugin in Plugins.Values)
        {
            var contextsToProcess = contexts.Where(c => c is { IsCancelled: false, HasError: false }).ToList();
            if (contextsToProcess.Count == 0)
                break;

            string metricName = String.Concat(_metricPrefix, plugin.Name.ToLower());
            try
            {
                await AppDiagnostics.TimeAsync(() => plugin.EventBatchProcessingAsync(contextsToProcess), metricName);

                if (contextsToProcess.All(c => c.IsCancelled || c.HasError))
                    break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling event processing in plugin {PluginName}: {Message}", plugin.Name, ex.Message);
            }
        }
    }

    /// <summary>
    /// Runs all the event plugins event processed method.
    /// </summary>
    public async Task EventBatchProcessedAsync(ICollection<EventContext> contexts)
    {
        foreach (var plugin in Plugins.Values)
        {
            var contextsToProcess = contexts.Where(c => c is { IsCancelled: false, HasError: false }).ToList();
            if (contextsToProcess.Count == 0)
                break;

            string metricName = String.Concat(_metricPrefix, plugin.Name.ToLower());
            try
            {
                await AppDiagnostics.TimeAsync(() => plugin.EventBatchProcessedAsync(contextsToProcess), metricName);

                if (contextsToProcess.All(c => c.IsCancelled || c.HasError))
                    break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling event processed in plugin {PluginName}: {Message}", plugin.Name, ex.Message);
            }
        }
    }
}
