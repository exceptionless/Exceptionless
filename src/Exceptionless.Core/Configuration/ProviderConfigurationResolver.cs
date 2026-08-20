using Exceptionless.Core.Extensions;
using Foundatio.Utility;
using Microsoft.Extensions.Configuration;

namespace Exceptionless.Core.Configuration;

internal enum ProviderRole
{
    Cache,
    MessageBus,
    Queue,
    Storage
}

internal sealed record ProviderConfiguration(
    string Provider,
    string? ConnectionString,
    Dictionary<string, string?> Data);

internal static class ProviderConfigurationResolver
{
    private const string LocalProvider = "local";
    private const string ProviderKey = "provider";
    private const string RabbitMqProvider = "rabbitmq";
    private const string RedisProvider = "redis";
    private const string ServerKey = "server";

    private enum ProviderConnectionStringFormat
    {
        KeyValue,
        Redis,
        AmqpUri
    }

    private sealed record ProviderCandidate(
        string Provider,
        string ConnectionStringName,
        ProviderConnectionStringFormat Format = ProviderConnectionStringFormat.KeyValue,
        bool AllowsEmptyConfiguration = false);

    private static readonly IReadOnlyDictionary<ProviderRole, ProviderCandidate[]> _providerCandidates =
        new Dictionary<ProviderRole, ProviderCandidate[]>
        {
            [ProviderRole.Cache] =
            [
                new(RedisProvider, "Redis", ProviderConnectionStringFormat.Redis)
            ],
            [ProviderRole.MessageBus] =
            [
                new(RabbitMqProvider, "RabbitMQ", ProviderConnectionStringFormat.AmqpUri),
                new(RedisProvider, "Redis", ProviderConnectionStringFormat.Redis)
            ],
            [ProviderRole.Queue] =
            [
                new("azurestorage", "AzureQueues"),
                new("sqs", "SQS"),
                new(RedisProvider, "Redis", ProviderConnectionStringFormat.Redis)
            ],
            [ProviderRole.Storage] =
            [
                new("azurestorage", "AzureStorage"),
                new("s3", "S3"),
                new("aliyun", "Aliyun"),
                new("folder", "Folder", AllowsEmptyConfiguration: true)
            ]
        };

    public static ProviderConfiguration Resolve(IConfiguration configuration, ProviderRole role)
    {
        string roleName = role.ToString();
        string? selector = configuration.GetConnectionString(roleName);
        if (String.IsNullOrWhiteSpace(selector))
            return ResolveInferred(configuration, role);

        selector = selector.Trim();
        if (String.Equals(selector, LocalProvider, StringComparison.OrdinalIgnoreCase))
            return CreateLocalConfiguration();

        Dictionary<string, string?> roleData = new(StringComparer.OrdinalIgnoreCase);
        string? inlineConnectionString = null;
        try
        {
            roleData.AddRange(selector.ParseConnectionString());
        }
        catch (ArgumentException)
        {
            if (!TryParseInlineConnectionString(selector, roleData, out inlineConnectionString))
                throw CreateInvalidConfigurationException(roleName);
        }

        string? provider = roleData.GetString(ProviderKey);
        if (String.IsNullOrWhiteSpace(provider))
            throw CreateInvalidConfigurationException(roleName);

        provider = provider.Trim().ToLowerInvariant();
        roleData[ProviderKey] = provider;
        if (String.Equals(provider, RedisProvider, StringComparison.Ordinal)
            && inlineConnectionString is null
            && TryGetInlineRedisConnectionString(selector, roleData, out inlineConnectionString))
        {
            roleData.Clear();
            roleData[ProviderKey] = provider;
        }

        if (String.Equals(provider, LocalProvider, StringComparison.Ordinal))
        {
            if (roleData.Count > 1 || inlineConnectionString is not null)
                throw CreateInvalidConfigurationException(roleName);

            return CreateLocalConfiguration();
        }

        ProviderCandidate candidate = GetCandidate(role, provider);
        if (inlineConnectionString is not null)
        {
            if (candidate.Format is ProviderConnectionStringFormat.KeyValue)
                throw CreateInvalidConfigurationException(roleName);

            return CreateRawConfiguration(roleName, candidate, roleData, inlineConnectionString);
        }

        string? providerConnectionString = GetProviderConnectionString(configuration, candidate);
        Dictionary<string, string?> explicitData = roleData
            .Where(pair => !String.Equals(pair.Key, ProviderKey, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);

        if (candidate.Format is not ProviderConnectionStringFormat.KeyValue)
            return ResolveRawConfiguration(roleName, candidate, roleData, explicitData, providerConnectionString);

        if (explicitData.Count == 0 && !String.IsNullOrWhiteSpace(providerConnectionString))
            return CreateConfiguration(candidate, providerConnectionString);

        var data = ParseProviderData(candidate, providerConnectionString);
        data.AddRange(explicitData);
        ValidateProviderIdentity(candidate, data);
        data[ProviderKey] = provider;

        string? connectionString = data.BuildConnectionString([ProviderKey]);
        if (String.IsNullOrWhiteSpace(connectionString))
        {
            if (candidate.AllowsEmptyConfiguration)
                return new ProviderConfiguration(provider, null, data);

            throw CreateInvalidConfigurationException(roleName);
        }

        return new ProviderConfiguration(provider, connectionString, data);
    }

    private static ProviderConfiguration ResolveInferred(IConfiguration configuration, ProviderRole role)
    {
        foreach (ProviderCandidate candidate in _providerCandidates[role])
        {
            string? connectionString = configuration.GetConnectionString(candidate.ConnectionStringName);
            if (String.IsNullOrWhiteSpace(connectionString))
                continue;

            return candidate.Format is not ProviderConnectionStringFormat.KeyValue
                ? CreateRawConfiguration(role.ToString(), candidate, new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase), connectionString)
                : CreateConfiguration(candidate, connectionString!);
        }

        return CreateLocalConfiguration();
    }

    private static ProviderCandidate GetCandidate(ProviderRole role, string provider)
    {
        ProviderCandidate? candidate = _providerCandidates[role]
            .FirstOrDefault(candidate => String.Equals(candidate.Provider, provider, StringComparison.OrdinalIgnoreCase));
        if (candidate is null)
            throw new InvalidOperationException($"Provider '{provider}' is not supported for ConnectionStrings:{role}.");

        return candidate;
    }

    private static string? GetProviderConnectionString(IConfiguration configuration, ProviderCandidate candidate)
    {
        string? connectionString = configuration.GetConnectionString(candidate.ConnectionStringName);
        if (!String.IsNullOrWhiteSpace(connectionString))
            return connectionString;

        if (!String.Equals(candidate.ConnectionStringName, candidate.Provider, StringComparison.OrdinalIgnoreCase))
            connectionString = configuration.GetConnectionString(candidate.Provider);

        return connectionString;
    }

    private static ProviderConfiguration CreateConfiguration(ProviderCandidate candidate, string connectionString)
    {
        connectionString = TrimMatchingQuotes(connectionString.Trim());
        var data = ParseProviderData(candidate, connectionString);
        ValidateProviderIdentity(candidate, data);
        data[ProviderKey] = candidate.Provider;
        return new ProviderConfiguration(candidate.Provider, data.BuildConnectionString([ProviderKey]), data);
    }

    private static ProviderConfiguration ResolveRawConfiguration(
        string roleName,
        ProviderCandidate candidate,
        Dictionary<string, string?> roleData,
        Dictionary<string, string?> explicitData,
        string? providerConnectionString)
    {
        if (explicitData.Count == 1
            && explicitData.TryGetValue(ServerKey, out string? server)
            && !String.IsNullOrWhiteSpace(server))
        {
            return CreateRawConfiguration(roleName, candidate, roleData, server);
        }

        if (explicitData.Count > 0 || String.IsNullOrWhiteSpace(providerConnectionString))
            throw CreateInvalidConfigurationException(roleName);

        return CreateRawConfiguration(roleName, candidate, roleData, providerConnectionString);
    }

    private static ProviderConfiguration CreateRawConfiguration(
        string roleName,
        ProviderCandidate candidate,
        Dictionary<string, string?> data,
        string connectionString)
    {
        connectionString = TrimMatchingQuotes(connectionString.Trim());
        if (candidate.Format is ProviderConnectionStringFormat.AmqpUri && !IsSupportedAbsoluteUri(connectionString))
            throw CreateInvalidConfigurationException(roleName);
        if (candidate.Format is ProviderConnectionStringFormat.Redis && ContainsProviderMetadata(connectionString))
            throw CreateInvalidConfigurationException(roleName);

        ValidateProviderIdentity(candidate, data);
        data[ProviderKey] = candidate.Provider;
        data[ServerKey] = connectionString;
        return new ProviderConfiguration(candidate.Provider, connectionString, data);
    }

    private static void ValidateProviderIdentity(ProviderCandidate candidate, IDictionary<string, string?> data)
    {
        string? configuredProvider = data.GetString(ProviderKey);
        if (!String.IsNullOrWhiteSpace(configuredProvider)
            && !String.Equals(configuredProvider, candidate.Provider, StringComparison.OrdinalIgnoreCase))
        {
            throw CreateInvalidConfigurationException(candidate.ConnectionStringName);
        }
    }

    private static Dictionary<string, string?> ParseProviderData(ProviderCandidate candidate, string? connectionString)
    {
        if (String.IsNullOrWhiteSpace(connectionString))
            return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        try
        {
            return connectionString.ParseConnectionString();
        }
        catch (ArgumentException)
        {
            throw CreateInvalidConfigurationException(candidate.ConnectionStringName);
        }
    }

    private static bool ContainsProviderMetadata(string connectionString)
    {
        try
        {
            return connectionString.ParseConnectionString().ContainsKey(ProviderKey);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool TryGetInlineRedisConnectionString(
        string selector,
        IDictionary<string, string?> roleData,
        out string? connectionString)
    {
        connectionString = null;
        if (roleData.ContainsKey(ServerKey))
            return false;

        int separatorIndex = selector.IndexOf(';');
        if (separatorIndex < 0)
            return false;

        string value = TrimMatchingQuotes(selector[(separatorIndex + 1)..].Trim());
        if (String.IsNullOrWhiteSpace(value)
            || (value.Contains('=') && !value.Contains(',')))
            return false;

        connectionString = value;
        return true;
    }

    private static bool TryParseInlineConnectionString(
        string selector,
        Dictionary<string, string?> data,
        out string? connectionString)
    {
        connectionString = null;
        int separatorIndex = selector.IndexOf(';');
        if (separatorIndex < 0)
            return false;

        Dictionary<string, string?> providerData;
        try
        {
            providerData = selector[..separatorIndex].ParseConnectionString();
        }
        catch (ArgumentException)
        {
            return false;
        }

        if (providerData.Count != 1 || String.IsNullOrWhiteSpace(providerData.GetString(ProviderKey)))
            return false;

        string configuredConnectionString = TrimMatchingQuotes(selector[(separatorIndex + 1)..].Trim());
        if (String.IsNullOrWhiteSpace(configuredConnectionString))
            return false;

        data.AddRange(providerData);
        connectionString = configuredConnectionString;
        return true;
    }

    private static ProviderConfiguration CreateLocalConfiguration()
    {
        return new ProviderConfiguration(
            LocalProvider,
            null,
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                [ProviderKey] = LocalProvider
            });
    }

    private static bool IsSupportedAbsoluteUri(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
            && (String.Equals(uri.Scheme, "amqp", StringComparison.OrdinalIgnoreCase)
                || String.Equals(uri.Scheme, "amqps", StringComparison.OrdinalIgnoreCase));
    }

    private static string TrimMatchingQuotes(string value)
    {
        if (value.Length >= 2 && ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
            return value[1..^1];

        return value;
    }

    private static InvalidOperationException CreateInvalidConfigurationException(string connectionStringName)
    {
        return new InvalidOperationException(
            $"ConnectionStrings:{connectionStringName} must specify a supported provider and a valid connection string, or use 'local'.");
    }
}
