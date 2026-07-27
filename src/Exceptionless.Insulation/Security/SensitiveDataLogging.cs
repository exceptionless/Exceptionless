using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
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

    private delegate LogEventPropertyValue Transform(
        object value,
        ILogEventPropertyValueFactory propertyValueFactory);

    private static readonly FrozenDictionary<Type, Transform> _transformers =
        new Dictionary<Type, Transform>
        {
            [typeof(AppOptions)] = static (value, factory) => CreateSafeAppOptions((AppOptions)value, factory),
            [typeof(AuthOptions)] = static (value, factory) => CreateSafeAuthOptions((AuthOptions)value, factory),
            [typeof(CacheOptions)] = static (value, factory) => CreateSafeCacheOptions((CacheOptions)value, factory),
            [typeof(ElasticsearchOptions)] = static (value, factory) => CreateSafeElasticsearchOptions((ElasticsearchOptions)value, factory),
            [typeof(EmailOptions)] = static (value, factory) => CreateSafeEmailOptions((EmailOptions)value, factory),
            [typeof(IntercomOptions)] = static (value, factory) => CreateSafeIntercomOptions((IntercomOptions)value, factory),
            [typeof(MessageBusOptions)] = static (value, factory) => CreateSafeMessageBusOptions((MessageBusOptions)value, factory),
            [typeof(MetricOptions)] = static (value, factory) => CreateSafeMetricOptions((MetricOptions)value, factory),
            [typeof(QueueOptions)] = static (value, factory) => CreateSafeQueueOptions((QueueOptions)value, factory),
            [typeof(StorageOptions)] = static (value, factory) => CreateSafeStorageOptions((StorageOptions)value, factory),
            [typeof(StripeOptions)] = static (value, factory) => CreateSafeStripeOptions((StripeOptions)value, factory),
            [typeof(SlackOptions)] = static (value, factory) => CreateSafeSlackOptions((SlackOptions)value, factory),
            [typeof(OAuthServerOptions)] = static (value, factory) => CreateSafeOAuthServerOptions((OAuthServerOptions)value, factory),
            [typeof(SourceMapOptions)] = static (value, factory) => CreateSafeSourceMapOptions((SourceMapOptions)value, factory)
        }.ToFrozenDictionary();

    private static readonly SensitiveOptionsDestructuringPolicy _policy = new();

    public static LoggerConfiguration ApplySensitiveDataLogging(this LoggerConfiguration configuration)
    {
        return configuration.Destructure.With(_policy);
    }

    private static StructureValue CreateSafeAppOptions(AppOptions options, ILogEventPropertyValueFactory factory)
    {
        var result = new SafeStructureBuilder(nameof(AppOptions), factory, 41);
        result.Add(nameof(options.BaseURL), options.BaseURL);
        result.Add(nameof(options.InternalProjectId), options.InternalProjectId);
        result.Add(nameof(options.ExceptionlessApiKey), RedactedValue);
        result.Add(nameof(options.ExceptionlessServerUrl), options.ExceptionlessServerUrl);
        result.Add(nameof(options.AppMode), options.AppMode);
        result.Add(nameof(options.AppScope), options.AppScope);
        result.Add(nameof(options.RunJobsInProcess), options.RunJobsInProcess);
        result.Add(nameof(options.JobsIterationLimit), options.JobsIterationLimit);
        result.Add(nameof(options.BotThrottleLimit), options.BotThrottleLimit);
        result.Add(nameof(options.ApiThrottleLimit), options.ApiThrottleLimit);
        result.Add(nameof(options.EnableArchive), options.EnableArchive);
        result.Add(nameof(options.EnableSampleData), options.EnableSampleData);
        result.Add(nameof(options.EventSubmissionDisabled), options.EventSubmissionDisabled);
        result.Add(nameof(options.DisabledPipelineActions), options.DisabledPipelineActions?.ToArray());
        result.Add(nameof(options.DisabledPlugins), options.DisabledPlugins?.ToArray());
        result.Add(nameof(options.MaximumEventPostSize), options.MaximumEventPostSize);
        result.Add(nameof(options.MaximumRetentionDays), options.MaximumRetentionDays);
        result.Add(nameof(options.EnableRepositoryNotifications), options.EnableRepositoryNotifications);
        result.Add(nameof(options.EnableWebSockets), options.EnableWebSockets);
        result.Add(nameof(options.Version), options.Version);
        result.Add(nameof(options.InformationalVersion), options.InformationalVersion);
        result.Add(nameof(options.NotificationMessage), options.NotificationMessage);
        result.Add(nameof(options.GoogleGeocodingApiKey), RedactedValue);
        result.Add(nameof(options.MaxMindGeoIpKey), RedactedValue);
        result.Add(nameof(options.BulkBatchSize), options.BulkBatchSize);
        result.Add(
            nameof(options.CacheOptions),
            options.CacheOptions is null ? null : CreateSafeCacheOptions(options.CacheOptions, factory));
        result.Add(
            nameof(options.MessageBusOptions),
            options.MessageBusOptions is null ? null : CreateSafeMessageBusOptions(options.MessageBusOptions, factory));
        result.Add(
            nameof(options.QueueOptions),
            options.QueueOptions is null ? null : CreateSafeQueueOptions(options.QueueOptions, factory));
        result.Add(
            nameof(options.StorageOptions),
            options.StorageOptions is null ? null : CreateSafeStorageOptions(options.StorageOptions, factory));
        result.Add(
            nameof(options.EmailOptions),
            options.EmailOptions is null ? null : CreateSafeEmailOptions(options.EmailOptions, factory));
        result.Add(
            nameof(options.ElasticsearchOptions),
            options.ElasticsearchOptions is null ? null : CreateSafeElasticsearchOptions(options.ElasticsearchOptions, factory));
        result.Add(
            nameof(options.IntercomOptions),
            options.IntercomOptions is null ? null : CreateSafeIntercomOptions(options.IntercomOptions, factory));
        result.Add(
            nameof(options.SlackOptions),
            options.SlackOptions is null ? null : CreateSafeSlackOptions(options.SlackOptions, factory));
        result.Add(
            nameof(options.StripeOptions),
            options.StripeOptions is null ? null : CreateSafeStripeOptions(options.StripeOptions, factory));
        result.Add(
            nameof(options.AuthOptions),
            options.AuthOptions is null ? null : CreateSafeAuthOptions(options.AuthOptions, factory));
        result.Add(
            nameof(options.OAuthServerOptions),
            options.OAuthServerOptions is null ? null : CreateSafeOAuthServerOptions(options.OAuthServerOptions, factory));
        result.Add(
            nameof(options.SourceMapOptions),
            options.SourceMapOptions is null ? null : CreateSafeSourceMapOptions(options.SourceMapOptions, factory));
        return result.Build();
    }

    private static StructureValue CreateSafeAuthOptions(AuthOptions options, ILogEventPropertyValueFactory factory)
    {
        var result = new SafeStructureBuilder(nameof(AuthOptions), factory, 11);
        result.Add(nameof(options.EnableAccountCreation), options.EnableAccountCreation);
        result.Add(nameof(options.EnableActiveDirectoryAuth), options.EnableActiveDirectoryAuth);
        result.Add(nameof(options.MicrosoftId), options.MicrosoftId);
        result.Add(nameof(options.MicrosoftSecret), RedactedValue);
        result.Add(nameof(options.FacebookId), options.FacebookId);
        result.Add(nameof(options.FacebookSecret), RedactedValue);
        result.Add(nameof(options.GitHubId), options.GitHubId);
        result.Add(nameof(options.GitHubSecret), RedactedValue);
        result.Add(nameof(options.GoogleId), options.GoogleId);
        result.Add(nameof(options.GoogleSecret), RedactedValue);
        result.Add(nameof(options.LdapConnectionString), SanitizeStandaloneConnectionString(options.LdapConnectionString));
        return result.Build();
    }

    private static StructureValue CreateSafeCacheOptions(CacheOptions options, ILogEventPropertyValueFactory factory)
    {
        var result = new SafeStructureBuilder(nameof(CacheOptions), factory, 5);
        result.Add(nameof(options.ConnectionString), SanitizeConnectionString(options.ConnectionString, options.Provider));
        result.Add(nameof(options.Provider), options.Provider);
        result.Add(nameof(options.Data), SanitizeConnectionData(options.Data, options.Provider));
        result.Add(nameof(options.Scope), options.Scope);
        result.Add(nameof(options.ScopePrefix), options.ScopePrefix);
        return result.Build();
    }

    private static StructureValue CreateSafeElasticsearchOptions(ElasticsearchOptions options, ILogEventPropertyValueFactory factory)
    {
        var result = new SafeStructureBuilder(nameof(ElasticsearchOptions), factory, 15);
        result.Add(nameof(options.ServerUrl), SanitizeUri(options.ServerUrl));
        result.Add(nameof(options.NumberOfShards), options.NumberOfShards);
        result.Add(nameof(options.NumberOfReplicas), options.NumberOfReplicas);
        result.Add(nameof(options.FieldsLimit), options.FieldsLimit);
        result.Add(nameof(options.EnableMapperSizePlugin), options.EnableMapperSizePlugin);
        result.Add(nameof(options.Scope), options.Scope);
        result.Add(nameof(options.ScopePrefix), options.ScopePrefix);
        result.Add(nameof(options.EnableSnapshotJobs), options.EnableSnapshotJobs);
        result.Add(nameof(options.DisableIndexConfiguration), options.DisableIndexConfiguration);
        result.Add(nameof(options.Password), RedactedValue);
        result.Add(nameof(options.UserName), options.UserName);
        result.Add(nameof(options.ReindexCutOffDate), options.ReindexCutOffDate);
        result.Add(
            nameof(options.ElasticsearchToMigrate),
            options.ElasticsearchToMigrate is null
                ? null
                : CreateSafeElasticsearchOptions(options.ElasticsearchToMigrate, factory));
        return result.Build();
    }

    private static StructureValue CreateSafeEmailOptions(EmailOptions options, ILogEventPropertyValueFactory factory)
    {
        var result = new SafeStructureBuilder(nameof(EmailOptions), factory, 11);
        result.Add(nameof(options.EnableDailySummary), options.EnableDailySummary);
        result.Add(nameof(options.TestEmailAddress), options.TestEmailAddress);
        result.Add(nameof(options.ContactEmailAddress), options.ContactEmailAddress);
        result.Add(nameof(options.AllowedOutboundAddresses), options.AllowedOutboundAddresses?.ToArray());
        result.Add(nameof(options.SmtpFrom), options.SmtpFrom);
        result.Add(nameof(options.SmtpHost), options.SmtpHost);
        result.Add(nameof(options.SmtpPort), options.SmtpPort);
        result.Add(nameof(options.SmtpEncryption), options.SmtpEncryption);
        result.Add(nameof(options.SmtpUser), options.SmtpUser);
        result.Add(nameof(options.SmtpPassword), RedactedValue);
        return result.Build();
    }

    private static StructureValue CreateSafeIntercomOptions(IntercomOptions options, ILogEventPropertyValueFactory factory)
    {
        var result = new SafeStructureBuilder(nameof(IntercomOptions), factory, 3);
        result.Add(nameof(options.EnableIntercom), options.EnableIntercom);
        result.Add(nameof(options.IntercomId), options.IntercomId);
        result.Add(nameof(options.IntercomSecret), RedactedValue);
        return result.Build();
    }

    private static StructureValue CreateSafeMessageBusOptions(MessageBusOptions options, ILogEventPropertyValueFactory factory)
    {
        var result = new SafeStructureBuilder(nameof(MessageBusOptions), factory, 6);
        result.Add(nameof(options.ConnectionString), SanitizeConnectionString(options.ConnectionString, options.Provider));
        result.Add(nameof(options.Provider), options.Provider);
        result.Add(nameof(options.Data), SanitizeConnectionData(options.Data, options.Provider));
        result.Add(nameof(options.Scope), options.Scope);
        result.Add(nameof(options.ScopePrefix), options.ScopePrefix);
        result.Add(nameof(options.Topic), options.Topic);
        return result.Build();
    }

    private static StructureValue CreateSafeMetricOptions(MetricOptions options, ILogEventPropertyValueFactory factory)
    {
        var result = new SafeStructureBuilder(nameof(MetricOptions), factory, 3);
        result.Add(nameof(options.ConnectionString), SanitizeConnectionString(options.ConnectionString, options.Provider));
        result.Add(nameof(options.Provider), options.Provider);
        result.Add(nameof(options.Data), SanitizeConnectionData(options.Data, options.Provider));
        return result.Build();
    }

    private static StructureValue CreateSafeOAuthServerOptions(OAuthServerOptions options, ILogEventPropertyValueFactory factory)
    {
        var result = new SafeStructureBuilder(nameof(OAuthServerOptions), factory, 8);
        result.Add(nameof(options.AuthorizationCodeLifetime), options.AuthorizationCodeLifetime);
        result.Add(nameof(options.AccessTokenLifetime), options.AccessTokenLifetime);
        result.Add(nameof(options.RefreshTokenLifetime), options.RefreshTokenLifetime);
        result.Add(nameof(options.EnableClientIdMetadataDocuments), options.EnableClientIdMetadataDocuments);
        result.Add(nameof(options.DynamicClientRegistrationIpLimit), options.DynamicClientRegistrationIpLimit);
        result.Add(nameof(options.ClientMetadataDocumentCacheLifetime), options.ClientMetadataDocumentCacheLifetime);
        result.Add(nameof(options.ClientMetadataDocumentRequestTimeout), options.ClientMetadataDocumentRequestTimeout);
        result.Add(nameof(options.ClientMetadataDocumentMaxBytes), options.ClientMetadataDocumentMaxBytes);
        return result.Build();
    }

    private static StructureValue CreateSafeQueueOptions(QueueOptions options, ILogEventPropertyValueFactory factory)
    {
        var result = new SafeStructureBuilder(nameof(QueueOptions), factory, 7);
        result.Add(nameof(options.ConnectionString), SanitizeConnectionString(options.ConnectionString, options.Provider));
        result.Add(nameof(options.Provider), options.Provider);
        result.Add(nameof(options.Data), SanitizeConnectionData(options.Data, options.Provider));
        result.Add(nameof(options.Scope), options.Scope);
        result.Add(nameof(options.ScopePrefix), options.ScopePrefix);
        result.Add(nameof(options.MetricsPollingEnabled), options.MetricsPollingEnabled);
        result.Add(nameof(options.MetricsPollingInterval), options.MetricsPollingInterval);
        return result.Build();
    }

    private static StructureValue CreateSafeSlackOptions(SlackOptions options, ILogEventPropertyValueFactory factory)
    {
        var result = new SafeStructureBuilder(nameof(SlackOptions), factory, 3);
        result.Add(nameof(options.SlackId), options.SlackId);
        result.Add(nameof(options.SlackSecret), RedactedValue);
        result.Add(nameof(options.EnableSlack), options.EnableSlack);
        return result.Build();
    }

    private static StructureValue CreateSafeSourceMapOptions(SourceMapOptions options, ILogEventPropertyValueFactory factory)
    {
        var result = new SafeStructureBuilder(nameof(SourceMapOptions), factory, 41);
        result.Add(nameof(options.EnableAutoDownload), options.EnableAutoDownload);
        result.Add(nameof(options.RequestTimeoutMilliseconds), options.RequestTimeoutMilliseconds);
        result.Add(nameof(options.MaximumGeneratedFileSize), options.MaximumGeneratedFileSize);
        result.Add(nameof(options.MaximumSourceMapSize), options.MaximumSourceMapSize);
        result.Add(nameof(options.MaximumArtifactsPerProject), options.MaximumArtifactsPerProject);
        result.Add(nameof(options.MaximumStorageSizePerProject), options.MaximumStorageSizePerProject);
        result.Add(nameof(options.MaximumArtifactsPerFreeProject), options.MaximumArtifactsPerFreeProject);
        result.Add(nameof(options.MaximumStorageSizePerFreeProject), options.MaximumStorageSizePerFreeProject);
        result.Add(nameof(options.MaximumMappingSegments), options.MaximumMappingSegments);
        result.Add(nameof(options.MaximumRedirects), options.MaximumRedirects);
        result.Add(nameof(options.MaximumConcurrentDownloads), options.MaximumConcurrentDownloads);
        result.Add(nameof(options.MaximumConcurrentDownloadsGlobally), options.MaximumConcurrentDownloadsGlobally);
        result.Add(nameof(options.AutoDownloadRateLimitPeriodMinutes), options.AutoDownloadRateLimitPeriodMinutes);
        result.Add(nameof(options.MaximumAutoDiscoveriesPerFreeClientKey), options.MaximumAutoDiscoveriesPerFreeClientKey);
        result.Add(nameof(options.MaximumAutoDiscoveriesPerClientKey), options.MaximumAutoDiscoveriesPerClientKey);
        result.Add(nameof(options.MaximumAutoDiscoveriesPerFreeProject), options.MaximumAutoDiscoveriesPerFreeProject);
        result.Add(nameof(options.MaximumAutoDiscoveriesPerProject), options.MaximumAutoDiscoveriesPerProject);
        result.Add(nameof(options.MaximumAutoDiscoveriesPerFreeOrganization), options.MaximumAutoDiscoveriesPerFreeOrganization);
        result.Add(nameof(options.MaximumAutoDiscoveriesPerOrganization), options.MaximumAutoDiscoveriesPerOrganization);
        result.Add(nameof(options.MaximumAutoDownloadRequestsPerDestination), options.MaximumAutoDownloadRequestsPerDestination);
        result.Add(nameof(options.MaximumAutoDownloadConnectionsPerIpAddress), options.MaximumAutoDownloadConnectionsPerIpAddress);
        result.Add(nameof(options.MaximumAutoDownloadRequestsGlobally), options.MaximumAutoDownloadRequestsGlobally);
        result.Add(nameof(options.MaximumAutoRefreshRequestsPerDestination), options.MaximumAutoRefreshRequestsPerDestination);
        result.Add(nameof(options.MaximumAutoRefreshRequestsGlobally), options.MaximumAutoRefreshRequestsGlobally);
        result.Add(nameof(options.MaximumFramesPerError), options.MaximumFramesPerError);
        result.Add(nameof(options.MaximumProcessingTimeMilliseconds), options.MaximumProcessingTimeMilliseconds);
        result.Add(nameof(options.AutoDownloadRefreshIntervalMinutes), options.AutoDownloadRefreshIntervalMinutes);
        result.Add(nameof(options.ParsedSourceMapCacheLifetimeMinutes), options.ParsedSourceMapCacheLifetimeMinutes);
        result.Add(nameof(options.MaximumParsedSourceMapCacheSize), options.MaximumParsedSourceMapCacheSize);
        result.Add(nameof(options.UsageTrackingDebounceMinutes), options.UsageTrackingDebounceMinutes);
        result.Add(nameof(options.FreeArtifactRetentionDays), options.FreeArtifactRetentionDays);
        result.Add(nameof(options.ArtifactRetentionDays), options.ArtifactRetentionDays);
        result.Add(nameof(options.RequestTimeout), options.RequestTimeout);
        result.Add(nameof(options.MaximumProcessingTime), options.MaximumProcessingTime);
        result.Add(nameof(options.AutoDownloadRefreshInterval), options.AutoDownloadRefreshInterval);
        result.Add(nameof(options.ParsedSourceMapCacheLifetime), options.ParsedSourceMapCacheLifetime);
        result.Add(nameof(options.AutoDownloadRateLimitPeriod), options.AutoDownloadRateLimitPeriod);
        result.Add(nameof(options.UsageTrackingDebounce), options.UsageTrackingDebounce);
        result.Add(nameof(options.FreeArtifactRetention), options.FreeArtifactRetention);
        result.Add(nameof(options.ArtifactRetention), options.ArtifactRetention);
        return result.Build();
    }

    private static StructureValue CreateSafeStorageOptions(StorageOptions options, ILogEventPropertyValueFactory factory)
    {
        var result = new SafeStructureBuilder(nameof(StorageOptions), factory, 5);
        result.Add(nameof(options.ConnectionString), SanitizeConnectionString(options.ConnectionString, options.Provider));
        result.Add(nameof(options.Provider), options.Provider);
        result.Add(nameof(options.Data), SanitizeConnectionData(options.Data, options.Provider));
        result.Add(nameof(options.Scope), options.Scope);
        result.Add(nameof(options.ScopePrefix), options.ScopePrefix);
        return result.Build();
    }

    private static StructureValue CreateSafeStripeOptions(StripeOptions options, ILogEventPropertyValueFactory factory)
    {
        var result = new SafeStructureBuilder(nameof(StripeOptions), factory, 4);
        result.Add(nameof(options.EnableBilling), options.EnableBilling);
        result.Add(nameof(options.StripeApiKey), RedactedValue);
        result.Add(nameof(options.StripePublishableApiKey), options.StripePublishableApiKey);
        result.Add(nameof(options.StripeWebHookSigningSecret), RedactedValue);
        return result.Build();
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
            if (!_transformers.TryGetValue(value.GetType(), out var transform))
            {
                result = null;
                return false;
            }

            result = transform(value, propertyValueFactory);
            return true;
        }
    }

    private sealed class SafeStructureBuilder
    {
        private readonly ILogEventPropertyValueFactory _factory;
        private readonly List<LogEventProperty> _properties;
        private readonly string _typeTag;

        public SafeStructureBuilder(
            string typeTag,
            ILogEventPropertyValueFactory factory,
            int propertyCapacity)
        {
            _typeTag = typeTag;
            _factory = factory;
            _properties = new List<LogEventProperty>(propertyCapacity);
        }

        public void Add(string name, object? value)
        {
            _properties.Add(new LogEventProperty(
                name,
                value as LogEventPropertyValue ?? _factory.CreatePropertyValue(value, destructureObjects: true)));
        }

        public StructureValue Build() => new(_properties, _typeTag);
    }
}
