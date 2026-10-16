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

        var providerConfiguration = ProviderConfigurationResolver.Resolve(config, ProviderRole.Queue);
        options.Data = providerConfiguration.Data;
        options.Provider = providerConfiguration.Provider;
        options.ConnectionString = providerConfiguration.ConnectionString;
        return options;
    }
}
