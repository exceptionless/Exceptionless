using Microsoft.Extensions.Configuration;

namespace Exceptionless.Core.Configuration;

public class CacheOptions
{
    public string? ConnectionString { get; internal set; }
    public string? Provider { get; internal set; }
    public Dictionary<string, string?> Data { get; internal set; } = new(StringComparer.OrdinalIgnoreCase);

    public string Scope { get; internal set; } = null!;
    public string ScopePrefix { get; internal set; } = null!;

    public static CacheOptions ReadFromConfiguration(IConfiguration config, AppOptions appOptions)
    {
        var options = new CacheOptions { Scope = appOptions.AppScope };
        options.ScopePrefix = !String.IsNullOrEmpty(options.Scope) ? $"{options.Scope}-" : String.Empty;

        var providerConfiguration = ProviderConfigurationResolver.Resolve(config, ProviderRole.Cache);
        options.Data = providerConfiguration.Data;
        options.Provider = providerConfiguration.Provider;
        options.ConnectionString = providerConfiguration.ConnectionString;

        return options;
    }
}
