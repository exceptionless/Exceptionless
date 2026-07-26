using System.Diagnostics.CodeAnalysis;
using Exceptionless.Core.Extensions;
using Foundatio.Utility;
using Microsoft.Extensions.Configuration;

namespace Exceptionless.Core.Configuration;

public class MessageBusOptions
{
    private const string RabbitMqProvider = "rabbitmq";
    private const string ServerKey = "server";

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
            if (TryGetRabbitMqConnectionString(config, cs, out string? connectionString))
            {
                options.Provider = RabbitMqProvider;
                options.ConnectionString = connectionString;
                options.Data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    [nameof(options.Provider)] = options.Provider
                };
                options.Data[ServerKey] = connectionString;

                return options;
            }

            options.Data = cs.ParseConnectionString();
            options.Provider = options.Data.GetString(nameof(options.Provider));
            string? providerConnectionString = !String.IsNullOrEmpty(options.Provider) ? config.GetConnectionString(options.Provider) : null;

            var providerOptions = providerConnectionString.ParseConnectionString(defaultKey: ServerKey);
            options.Data.AddRange(providerOptions);

            options.ConnectionString = options.Data.BuildConnectionString(new HashSet<string> { nameof(options.Provider) });
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

    private static bool TryGetRabbitMqConnectionString(IConfiguration config, string selector, [NotNullWhen(true)] out string? connectionString)
    {
        connectionString = null;

        int separatorIndex = selector.IndexOf(';');
        string providerSelector = separatorIndex >= 0 ? selector[..separatorIndex] : selector;
        var providerData = providerSelector.ParseConnectionString();

        if (!String.Equals(providerData.GetString(nameof(Provider)), RabbitMqProvider, StringComparison.OrdinalIgnoreCase))
            return false;

        string? configuredConnectionString = separatorIndex >= 0 ? selector[(separatorIndex + 1)..] : null;
        if (String.IsNullOrWhiteSpace(configuredConnectionString))
            configuredConnectionString = config.GetConnectionString(RabbitMqProvider);

        if (String.IsNullOrWhiteSpace(configuredConnectionString))
            return false;

        connectionString = TrimMatchingQuotes(configuredConnectionString.Trim());
        return true;
    }

    private static string TrimMatchingQuotes(string value)
    {
        if (value.Length >= 2 && ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
            return value[1..^1];

        return value;
    }
}
