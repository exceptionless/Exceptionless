using Exceptionless.Core.Extensions;
using Foundatio.Utility;
using Microsoft.Extensions.Configuration;

namespace Exceptionless.Core.Configuration;

public class QueueOptions
{
    public string? ConnectionString { get; internal set; }
    public string? Provider { get; internal set; }
    public Dictionary<string, string?> Data { get; internal set; } = new(StringComparer.OrdinalIgnoreCase);

    public string Scope { get; internal set; } = null!;
    public string ScopePrefix { get; internal set; } = null!;
    public bool MetricsPollingEnabled { get; set; } = true;
    public TimeSpan MetricsPollingInterval { get; set; } = TimeSpan.FromSeconds(5);

    public static QueueOptions ReadFromConfiguration(IConfiguration config, AppOptions appOptions)
    {
        var options = new QueueOptions
        {
            Scope = appOptions.AppScope,
            ScopePrefix = !String.IsNullOrEmpty(appOptions.AppScope) ? $"{appOptions.AppScope}-" : String.Empty,
            MetricsPollingInterval = appOptions.AppMode == AppMode.Development ? TimeSpan.FromSeconds(15) : TimeSpan.FromSeconds(5)
        };

        string? cs = config.GetConnectionString("Queue");
        if (!String.IsNullOrWhiteSpace(cs))
        {
            var providerConfiguration = ProviderConfigurationResolver.Resolve(config, "Queue");
            options.Data = providerConfiguration.Data;
            options.Provider = providerConfiguration.Provider;
            options.ConnectionString = providerConfiguration.ConnectionString;
            return options;
        }
        else
        {
            string? azureStorageConnectionString = config.GetConnectionString("AzureQueues");
            if (!String.IsNullOrEmpty(azureStorageConnectionString))
            {
                options.Provider = "azurestorage";
                options.ConnectionString = azureStorageConnectionString;
                options.Data = azureStorageConnectionString.ParseConnectionString();
                return options;
            }

            string? redisConnectionString = config.GetConnectionString("Redis");
            if (!String.IsNullOrEmpty(redisConnectionString))
            {
                options.Provider = "redis";
                options.ConnectionString = redisConnectionString;
                options.Data = redisConnectionString.ParseConnectionString();
                return options;
            }
        }

        return options;
    }
}
