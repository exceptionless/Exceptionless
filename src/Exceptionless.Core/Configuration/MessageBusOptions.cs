using Exceptionless.Core.Extensions;
using Foundatio.Utility;
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
            if (TryGetRawRabbitMqConnectionString(cs, out var connectionString))
            {
                options.Provider = "rabbitmq";
                options.ConnectionString = connectionString;
                options.Data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    [nameof(options.Provider)] = options.Provider
                };
            }
            else
            {
                options.Data = cs.ParseConnectionString();
                options.Provider = options.Data.GetString(nameof(options.Provider));
            }

            if (String.IsNullOrEmpty(options.ConnectionString))
            {
                string? providerConnectionString = !String.IsNullOrEmpty(options.Provider) ? config.GetConnectionString(options.Provider) : null;
                if (String.Equals(options.Provider, "rabbitmq", StringComparison.OrdinalIgnoreCase) && !String.IsNullOrWhiteSpace(providerConnectionString))
                {
                    options.ConnectionString = TrimMatchingQuotes(providerConnectionString.Trim());
                }
                else
                {
                    var providerOptions = providerConnectionString.ParseConnectionString(defaultKey: "server");
                    options.Data ??= new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                    options.Data.AddRange(providerOptions);

                    options.ConnectionString = options.Data.BuildConnectionString(new HashSet<string> { nameof(options.Provider) });
                }
            }
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

    private static bool TryGetRawRabbitMqConnectionString(string connectionString, out string? rawConnectionString)
    {
        rawConnectionString = null;

        const string providerPrefix = "provider=";
        if (!connectionString.StartsWith(providerPrefix, StringComparison.OrdinalIgnoreCase))
            return false;

        int separatorIndex = connectionString.IndexOf(';');
        if (separatorIndex <= providerPrefix.Length)
            return false;

        string provider = connectionString.Substring(providerPrefix.Length, separatorIndex - providerPrefix.Length).Trim();
        if (!String.Equals(provider, "rabbitmq", StringComparison.OrdinalIgnoreCase))
            return false;

        rawConnectionString = TrimMatchingQuotes(connectionString[(separatorIndex + 1)..].Trim());
        return !String.IsNullOrEmpty(rawConnectionString);
    }

    private static string TrimMatchingQuotes(string value)
    {
        if (value.Length >= 2 && ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
            return value[1..^1];

        return value;
    }
}
