using Exceptionless.Core.Extensions;
using Foundatio.Repositories.Extensions;
using Foundatio.Utility;
using Microsoft.Extensions.Configuration;

namespace Exceptionless.Core.Configuration;

public class CacheOptions
{
    public string? ConnectionString { get; internal set; }
    public string? Provider { get; internal set; }
    public Dictionary<string, string> Data { get; internal set; } = null!;

    public string Scope { get; internal set; } = null!;
    public string ScopePrefix { get; internal set; } = null!;

    public static CacheOptions ReadFromConfiguration(IConfiguration config, AppOptions appOptions)
    {
        var options = new CacheOptions { Scope = appOptions.AppScope };
        options.ScopePrefix = !String.IsNullOrEmpty(options.Scope) ? $"{options.Scope}-" : String.Empty;

        string? cs = config.GetConnectionString("Cache");
        if (cs != null)
        {
            options.Data = cs.ParseConnectionString();
            options.Provider = options.Data.GetString(nameof(options.Provider));
        }
        else
        {
            var redisConnectionString = config.GetConnectionString("Redis");
            if (!String.IsNullOrEmpty(redisConnectionString))
            {
                options.Provider = "redis";
            }
        }

        string? providerConnectionString = !String.IsNullOrEmpty(options.Provider) ? config.GetConnectionString(options.Provider) : null;
        if (!String.IsNullOrEmpty(providerConnectionString))
        {
            var providerOptions = providerConnectionString.ParseConnectionString(defaultKey: "server");
            options.Data ??= [];
            options.Data.AddRange(providerOptions);
        }

        options.ConnectionString = options.Data.BuildConnectionString([nameof(options.Provider)]);

        return options;
    }
}
