using Microsoft.Extensions.Configuration;

namespace Exceptionless.Core.Configuration;

public class StorageOptions
{
    public string? ConnectionString { get; internal set; }
    public string? Provider { get; internal set; }
    public Dictionary<string, string?> Data { get; internal set; } = new(StringComparer.OrdinalIgnoreCase);

    public string Scope { get; internal set; } = null!;
    public string ScopePrefix { get; internal set; } = null!;

    public static StorageOptions ReadFromConfiguration(IConfiguration config, AppOptions appOptions)
    {
        var options = new StorageOptions
        {
            Scope = appOptions.AppScope,
            ScopePrefix = !String.IsNullOrEmpty(appOptions.AppScope) ? $"{appOptions.AppScope}-" : String.Empty
        };

        var providerConfiguration = ProviderConfigurationResolver.Resolve(config, ProviderRole.Storage);
        options.Data = providerConfiguration.Data;
        options.Provider = providerConfiguration.Provider;
        options.ConnectionString = providerConfiguration.ConnectionString;
        return options;
    }
}
