using Microsoft.Extensions.Configuration;

namespace Exceptionless.Core.Configuration;

public class MessageBusOptions
{
    public string? ConnectionString { get; internal set; }
    public string? Provider { get; internal set; }
    public Dictionary<string, string?> Data { get; internal set; } = null!;

    public string Scope { get; internal set; } = null!;
    public string ScopePrefix { get; internal set; } = null!;
    public string Topic { get; internal set; } = null!;

    public static MessageBusOptions ReadFromConfiguration(IConfiguration config, AppOptions appOptions)
    {
        var options = new MessageBusOptions { Scope = appOptions.AppScope };
        options.ScopePrefix = !String.IsNullOrEmpty(options.Scope) ? $"{options.Scope}-" : String.Empty;
        options.Topic = config.GetValue<string>(nameof(options.Topic), $"{options.ScopePrefix}messages");

        string? cs = config.GetConnectionString("MessageBus");

        if (cs != null)
        {
            var providerConfiguration = ProviderConfigurationResolver.Resolve(config, "MessageBus", providerConnectionStringDefaultKey: "server");
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
