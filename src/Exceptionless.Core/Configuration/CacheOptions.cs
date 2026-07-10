using Microsoft.Extensions.Configuration;

namespace Exceptionless.Core.Configuration;

public class CacheOptions
{
    public string? ConnectionString { get; internal set; }
    public string? Provider { get; internal set; }
    public Dictionary<string, string?> Data { get; internal set; } = null!;

    public string Scope { get; internal set; } = null!;
    public string ScopePrefix { get; internal set; } = null!;

    public static CacheOptions ReadFromConfiguration(IConfiguration config, AppOptions appOptions)
    {
        var options = new CacheOptions { Scope = appOptions.AppScope };
        options.ScopePrefix = !String.IsNullOrEmpty(options.Scope) ? $"{options.Scope}-" : String.Empty;

        string? cs = config.GetConnectionString("Cache");
        if (cs != null)
        {
            var providerConfiguration = ProviderConfigurationResolver.Resolve(config, "Cache", providerConnectionStringDefaultKey: "server");
            options.Data = providerConfiguration.Data;
            options.Provider = providerConfiguration.Provider;
            options.ConnectionString = providerConfiguration.ConnectionString;
        }
        else
        {
            string? redisConnectionString = config.GetConnectionString("Redis");
            if (!String.IsNullOrEmpty(redisConnectionString))
            {
                options.Provider = "redis";
                options.ConnectionString = redisConnectionString;
            }
        }

        return options;
    }
}
