using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Plugins.WebHook;

public class WebHookDataPluginManager : PluginManagerBase<IWebHookDataPlugin>
{
    public WebHookDataPluginManager(IServiceProvider serviceProvider, AppOptions options, ILoggerFactory loggerFactory) : base(serviceProvider, options, loggerFactory) { }

    /// <summary>
    /// Runs all of the event plugins create method.
    /// </summary>
    public async Task<object?> CreateFromEventAsync(WebHookDataContext context)
    {
        string metricPrefix = String.Concat(_metricPrefix, "ev-create", ".");
        foreach (var plugin in Plugins.Values)
        {
            string metricName = String.Concat(metricPrefix, plugin.Name.ToLower());
            try
            {
                object? data = null;
                await AppDiagnostics.TimeAsync(async () => data = await plugin.CreateFromEventAsync(context), metricName);
                if (context.IsCancelled)
                    break;

                if (data is null)
                    continue;

                return data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling create from event {EventId} in plugin {PluginName}: {Message}", context.Event?.Id, plugin.Name, ex.Message);
            }
        }

        return null;
    }

    /// <summary>
    /// Runs all of the event plugins create method.
    /// </summary>
    public async Task<object?> CreateFromStackAsync(WebHookDataContext context)
    {
        string metricPrefix = String.Concat(_metricPrefix, "st-create", ".");
        foreach (var plugin in Plugins.Values)
        {
            string metricName = String.Concat(metricPrefix, plugin.Name.ToLower());
            try
            {
                object? data = null;
                await AppDiagnostics.TimeAsync(async () => data = await plugin.CreateFromStackAsync(context), metricName);
                if (context.IsCancelled)
                    break;

                if (data is null)
                    continue;

                return data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling create from stack {StackId} in plugin {PluginName}: {Message}", context.Stack.Id, plugin.Name, ex.Message);
            }
        }

        return null;
    }
}
