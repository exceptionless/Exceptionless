using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Exceptionless.Core;
using Exceptionless.Core.Configuration;
using Foundatio.Utility;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using StackExchange.Redis;

namespace Exceptionless.Insulation.Security;

/// <summary>
/// Replaces configuration option objects with safe, structured copies before they reach log sinks.
/// </summary>
public static class SensitiveDataLogging
{
    private const string RedactedValue = "[REDACTED]";

    private static readonly FrozenSet<string> _secretConnectionStringKeys = new[]
    {
        "password",
        "pwd",
        "secret",
        "secretkey",
        "accesskeysecret",
        "accountkey",
        "sharedaccesskey",
        "sharedaccesssignature",
        "signature",
        "sig",
        "token",
        "accesstoken",
        "access_token",
        "apikey",
        "api_key",
        "clientsecret",
        "client_secret",
        "connectiontoken",
        "sas",
        "x-amz-signature"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private delegate object? PropertySanitizer(object options, object? value);
    private delegate object? PropertySanitizer<in TOptions>(TOptions options, object? value);

    private sealed record SensitivePropertyRule(
        Type OptionsType,
        string PropertyName,
        PropertySanitizer Sanitize);

    // Properties not listed here are logged normally.
    private static readonly FrozenDictionary<Type, FrozenDictionary<string, PropertySanitizer>> _sensitiveProperties =
        new SensitivePropertyRule[]
        {
            Redact<AppOptions>(nameof(AppOptions.ExceptionlessApiKey)),
            Redact<AppOptions>(nameof(AppOptions.GoogleGeocodingApiKey)),
            Redact<AppOptions>(nameof(AppOptions.MaxMindGeoIpKey)),
            Redact<AuthOptions>(nameof(AuthOptions.MicrosoftSecret)),
            Redact<AuthOptions>(nameof(AuthOptions.FacebookSecret)),
            Redact<AuthOptions>(nameof(AuthOptions.GitHubSecret)),
            Redact<AuthOptions>(nameof(AuthOptions.GoogleSecret)),
            Sanitize<AuthOptions>(
                nameof(AuthOptions.LdapConnectionString),
                static (_, value) => SanitizeStandaloneConnectionString((string?)value)),
            Sanitize<CacheOptions>(
                nameof(CacheOptions.ConnectionString),
                static (options, value) => SanitizeConnectionString((string?)value, options.Provider)),
            Sanitize<CacheOptions>(
                nameof(CacheOptions.Data),
                static (options, value) => SanitizeConnectionData((IDictionary<string, string?>?)value, options.Provider)),
            Sanitize<ElasticsearchOptions>(
                nameof(ElasticsearchOptions.ServerUrl),
                static (_, value) => SanitizeUri((string?)value)),
            Redact<ElasticsearchOptions>(nameof(ElasticsearchOptions.Password)),
            Redact<EmailOptions>(nameof(EmailOptions.SmtpPassword)),
            Redact<IntercomOptions>(nameof(IntercomOptions.IntercomSecret)),
            Sanitize<MessageBusOptions>(
                nameof(MessageBusOptions.ConnectionString),
                static (options, value) => SanitizeConnectionString((string?)value, options.Provider)),
            Sanitize<MessageBusOptions>(
                nameof(MessageBusOptions.Data),
                static (options, value) => SanitizeConnectionData((IDictionary<string, string?>?)value, options.Provider)),
            Sanitize<MetricOptions>(
                nameof(MetricOptions.ConnectionString),
                static (options, value) => SanitizeConnectionString((string?)value, options.Provider)),
            Sanitize<MetricOptions>(
                nameof(MetricOptions.Data),
                static (options, value) => SanitizeConnectionData((IDictionary<string, string?>?)value, options.Provider)),
            Sanitize<QueueOptions>(
                nameof(QueueOptions.ConnectionString),
                static (options, value) => SanitizeConnectionString((string?)value, options.Provider)),
            Sanitize<QueueOptions>(
                nameof(QueueOptions.Data),
                static (options, value) => SanitizeConnectionData((IDictionary<string, string?>?)value, options.Provider)),
            Redact<SlackOptions>(nameof(SlackOptions.SlackSecret)),
            Sanitize<StorageOptions>(
                nameof(StorageOptions.ConnectionString),
                static (options, value) => SanitizeConnectionString((string?)value, options.Provider)),
            Sanitize<StorageOptions>(
                nameof(StorageOptions.Data),
                static (options, value) => SanitizeConnectionData((IDictionary<string, string?>?)value, options.Provider)),
            Redact<StripeOptions>(nameof(StripeOptions.StripeApiKey)),
            Redact<StripeOptions>(nameof(StripeOptions.StripeWebHookSigningSecret))
        }
        .GroupBy(rule => rule.OptionsType)
        .ToFrozenDictionary(
            group => group.Key,
            group => group.ToFrozenDictionary(
                rule => rule.PropertyName,
                rule => rule.Sanitize,
                StringComparer.Ordinal));

    private static readonly FrozenDictionary<Type, PropertyInfo[]> _propertiesByType =
        _sensitiveProperties.Keys.ToFrozenDictionary(
            type => type,
            type => type
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(property => property.GetMethod?.IsPrivate is false && property.GetIndexParameters().Length == 0)
                .ToArray());

    private static readonly SensitiveOptionsDestructuringPolicy _policy = new();

    public static LoggerConfiguration ApplySensitiveDataLogging(this LoggerConfiguration configuration)
    {
        return configuration.Destructure.With(_policy);
    }

    private static SensitivePropertyRule Redact<TOptions>(string propertyName)
    {
        return Sanitize<TOptions>(propertyName, static (_, _) => RedactedValue);
    }

    private static SensitivePropertyRule Sanitize<TOptions>(
        string propertyName,
        PropertySanitizer<TOptions> sanitize)
    {
        return new SensitivePropertyRule(
            typeof(TOptions),
            propertyName,
            (options, value) => sanitize((TOptions)options, value));
    }

    private static Dictionary<string, string?>? SanitizeConnectionData(
        IDictionary<string, string?>? data,
        string? provider)
    {
        if (data is null)
            return null;

        var safeData = new Dictionary<string, string?>(data.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var pair in data)
            safeData[pair.Key] = SanitizeConnectionValue(pair.Key, pair.Value, provider);

        return safeData;
    }

    private static string? SanitizeConnectionString(string? connectionString, string? provider)
    {
        if (String.IsNullOrEmpty(connectionString))
            return connectionString;

        if (String.Equals(provider, "redis", StringComparison.OrdinalIgnoreCase)
            && TrySanitizeRedisConnectionString(connectionString, out string? safeRedisConnectionString))
        {
            return safeRedisConnectionString;
        }

        try
        {
            var data = connectionString.ParseConnectionString(defaultKey: "server");
            return SanitizeConnectionData(data, provider)?.BuildConnectionString();
        }
        catch (ArgumentException)
        {
            string? safeUri = SanitizeUri(connectionString);
            if (!String.Equals(safeUri, connectionString, StringComparison.Ordinal))
                return safeUri;

            return RedactCommaSeparatedSecrets(connectionString);
        }
    }

    private static string? SanitizeStandaloneConnectionString(string? connectionString)
    {
        if (String.IsNullOrEmpty(connectionString))
            return connectionString;

        string? safeUri = SanitizeUri(connectionString);
        if (!String.Equals(safeUri, connectionString, StringComparison.Ordinal))
            return safeUri;

        try
        {
            var data = connectionString.ParseConnectionString();
            return SanitizeConnectionData(data, provider: null)?.BuildConnectionString();
        }
        catch (ArgumentException)
        {
            return RedactCommaSeparatedSecrets(connectionString);
        }
    }

    private static string? SanitizeConnectionValue(string key, string? value, string? provider)
    {
        if (_secretConnectionStringKeys.Contains(key))
            return RedactedValue;

        if (String.IsNullOrEmpty(value))
            return value;

        if (String.Equals(provider, "redis", StringComparison.OrdinalIgnoreCase)
            && String.Equals(key, "server", StringComparison.OrdinalIgnoreCase)
            && TrySanitizeRedisConnectionString(value, out string? safeRedisConnectionString))
        {
            return safeRedisConnectionString;
        }

        return SanitizeUri(value);
    }

    private static bool TrySanitizeRedisConnectionString(string connectionString, out string? safeConnectionString)
    {
        try
        {
            var redisOptions = ConfigurationOptions.Parse(connectionString);
            safeConnectionString = redisOptions
                .ToString(includePassword: false)
                .Replace("password=*****", $"password={RedactedValue}", StringComparison.OrdinalIgnoreCase);
            return true;
        }
        catch (ArgumentException)
        {
            safeConnectionString = null;
            return false;
        }
    }

    private static string? SanitizeUri(string? value)
    {
        if (String.IsNullOrEmpty(value)
            || !value.Contains("://", StringComparison.Ordinal)
            || !Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return value;
        }

        var builder = new UriBuilder(uri);
        bool changed = false;

        if (!String.IsNullOrEmpty(builder.Password))
        {
            builder.Password = RedactedValue;
            changed = true;
        }

        if (!String.IsNullOrEmpty(builder.Query))
        {
            string[] segments = builder.Query.TrimStart('?').Split('&');
            for (int index = 0; index < segments.Length; index++)
            {
                int separatorIndex = segments[index].IndexOf('=');
                string encodedKey = separatorIndex >= 0 ? segments[index][..separatorIndex] : segments[index];
                string key = Uri.UnescapeDataString(encodedKey.Replace("+", " ", StringComparison.Ordinal));

                if (!_secretConnectionStringKeys.Contains(key))
                    continue;

                segments[index] = $"{encodedKey}={Uri.EscapeDataString(RedactedValue)}";
                changed = true;
            }

            if (changed)
                builder.Query = String.Join('&', segments);
        }

        return changed ? builder.Uri.AbsoluteUri : value;
    }

    private static string RedactCommaSeparatedSecrets(string value)
    {
        string[] segments = value.Split(',');
        bool changed = false;

        for (int index = 0; index < segments.Length; index++)
        {
            int separatorIndex = segments[index].IndexOf('=');
            if (separatorIndex < 0)
                continue;

            string key = segments[index][..separatorIndex].Trim();
            if (!_secretConnectionStringKeys.Contains(key))
                continue;

            segments[index] = $"{segments[index][..(separatorIndex + 1)]}{RedactedValue}";
            changed = true;
        }

        return changed ? String.Join(',', segments) : value;
    }

    private sealed class SensitiveOptionsDestructuringPolicy : IDestructuringPolicy
    {
        public bool TryDestructure(
            object value,
            ILogEventPropertyValueFactory propertyValueFactory,
            [NotNullWhen(true)]
            out LogEventPropertyValue? result)
        {
            Type optionsType = value.GetType();
            if (!_sensitiveProperties.TryGetValue(optionsType, out var sensitiveProperties))
            {
                result = null;
                return false;
            }

            PropertyInfo[] properties = _propertiesByType[optionsType];
            var safeProperties = new List<LogEventProperty>(properties.Length);
            foreach (PropertyInfo property in properties)
            {
                object? propertyValue = property.GetValue(value);
                if (sensitiveProperties.TryGetValue(property.Name, out var sanitize))
                    propertyValue = sanitize(value, propertyValue);

                safeProperties.Add(new LogEventProperty(
                    property.Name,
                    propertyValueFactory.CreatePropertyValue(propertyValue, destructureObjects: true)));
            }

            result = new StructureValue(safeProperties, optionsType.Name);
            return true;
        }
    }
}
