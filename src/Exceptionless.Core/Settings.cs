using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Exceptionless.Core.Extensions;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Stripe;

namespace Exceptionless.Core {
    public class Settings {
        public string BaseURL { get; private set; }

        /// <summary>
        /// Internal project id keeps us from recursively logging to our self
        /// </summary>
        public string InternalProjectId { get; private set; }

        /// <summary>
        /// Configures the exceptionless client api key, which logs all internal errors and log messages.
        /// </summary>
        public string ExceptionlessApiKey { get; private set; }

        /// <summary>
        /// Configures the exceptionless client server url, which logs all internal errors and log messages.
        /// </summary>
        public string ExceptionlessServerUrl { get; private set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public AppMode AppMode { get; private set; }

        public string AppScope { get; private set; }

        public bool HasAppScope => !String.IsNullOrEmpty(AppScope);

        public string AppScopePrefix => HasAppScope ? AppScope + "-" : String.Empty;

        public string QueueScope { get; set; }

        public string QueueScopePrefix => !String.IsNullOrEmpty(QueueScope) ? QueueScope + "-" : AppScopePrefix;

        public bool RunJobsInProcess { get; private set; }

        public int JobsIterationLimit { get; set; }

        public int BotThrottleLimit { get; private set; }

        public int ApiThrottleLimit { get; private set; }

        public bool EnableArchive { get; private set; }

        public bool EventSubmissionDisabled { get; private set; }

        internal List<string> DisabledPipelineActions { get; private set; }
        internal List<string> DisabledPlugins { get; private set; }

        /// <summary>
        /// In bytes
        /// </summary>
        public long MaximumEventPostSize { get; private set; }

        public int MaximumRetentionDays { get; private set; }

        public string MetricsServerName { get; private set; }

        public int MetricsServerPort { get; private set; }

        public bool EnableMetricsReporting { get; private set; }

        public string RedisConnectionString { get; private set; }

        public bool EnableSnapshotJobs { get; set; }

        public bool DisableIndexConfiguration { get; set; }

        public bool EnableBootstrapStartupActions { get; private set; }

        public string ElasticSearchConnectionString { get; private set; }

        public int ElasticSearchNumberOfShards { get; private set; }

        public int ElasticSearchNumberOfReplicas { get; private set; }

        public int ElasticSearchFieldsLimit { get; private set; }

        public bool EnableElasticsearchMapperSizePlugin { get; private set; }

        public string LdapConnectionString { get; private set; }

        public bool EnableActiveDirectoryAuth { get; internal set; }

        public bool EnableRepositoryNotifications { get; private set; }

        public bool EnableWebSockets { get; private set; }

        public string Version { get; private set; }

        public string InformationalVersion { get; private set; }

        public bool EnableIntercom => !String.IsNullOrEmpty(IntercomAppSecret);

        public string IntercomAppSecret { get; private set; }

        public string MicrosoftAppId { get; private set; }

        public string MicrosoftAppSecret { get; private set; }

        public string FacebookAppId { get; private set; }

        public string FacebookAppSecret { get; private set; }

        public string GitHubAppId { get; private set; }

        public string GitHubAppSecret { get; private set; }

        public string GoogleAppId { get; private set; }

        public string GoogleAppSecret { get; private set; }

        public string GoogleGeocodingApiKey { get; private set; }

        public string SlackAppId { get; private set; }

        public string SlackAppSecret { get; private set; }

        public bool EnableSlack => !String.IsNullOrEmpty(SlackAppId);

        public bool EnableBilling => !String.IsNullOrEmpty(StripeApiKey);

        public string StripeApiKey { get; private set; }
        public string StripeWebHookSigningSecret { get; set; }

        public string StorageFolder { get; private set; }

        public string AzureStorageConnectionString { get; private set; }

        public string AzureStorageQueueConnectionString { get; private set; }

        public string AliyunStorageConnectionString { get; private set; }

        public string MinioStorageConnectionString { get; private set; }

        public int BulkBatchSize { get; private set; }

        public bool EnableAccountCreation { get; internal set; }

        public bool EnableDailySummary { get; private set; }

        /// <summary>
        /// All emails that do not match the AllowedOutboundAddresses will be sent to this address in QA mode
        /// </summary>
        public string TestEmailAddress { get; private set; }

        /// <summary>
        /// Email addresses that match this comma delimited list of domains and email addresses will be allowed to be sent out in QA mode
        /// </summary>
        public List<string> AllowedOutboundAddresses { get; private set; }

        public string SmtpFrom { get; private set; }

        public string SmtpHost { get; private set; }

        public int SmtpPort { get; private set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public SmtpEncryption SmtpEncryption { get; private set; }

        public string SmtpUser { get; private set; }

        public string SmtpPassword { get; private set; }

        public static Settings Current { get; private set; }

        public static void Initialize(IConfiguration configRoot, string environment) {
            var settings = new Settings();

            settings.BaseURL = configRoot.GetValue<string>(nameof(BaseURL))?.TrimEnd('/');
            settings.InternalProjectId = configRoot.GetValue(nameof(InternalProjectId), "54b56e480ef9605a88a13153");
            settings.ExceptionlessApiKey = configRoot.GetValue<string>(nameof(ExceptionlessApiKey));
            settings.ExceptionlessServerUrl = configRoot.GetValue<string>(nameof(ExceptionlessServerUrl));

            settings.AppMode = configRoot.GetValue(nameof(AppMode), AppMode.Production);
            if (environment != null && environment.Equals("Development", StringComparison.OrdinalIgnoreCase))
                settings.AppMode = AppMode.Development;
            else if (environment != null && environment.Equals("Staging", StringComparison.OrdinalIgnoreCase))
                settings.AppMode = AppMode.Staging;

            settings.QueueScope = configRoot.GetValue(nameof(QueueScope), String.Empty);
            settings.AppScope = configRoot.GetValue(nameof(AppScope), String.Empty);
            settings.RunJobsInProcess = configRoot.GetValue(nameof(RunJobsInProcess), settings.AppMode == AppMode.Development);
            settings.JobsIterationLimit = configRoot.GetValue(nameof(JobsIterationLimit), -1);
            settings.BotThrottleLimit = configRoot.GetValue(nameof(BotThrottleLimit), 25).NormalizeValue();

            settings.ApiThrottleLimit = configRoot.GetValue(nameof(ApiThrottleLimit), settings.AppMode == AppMode.Development ? Int32.MaxValue : 3500).NormalizeValue();
            settings.EnableArchive = configRoot.GetValue(nameof(EnableArchive), true);
            settings.EventSubmissionDisabled = configRoot.GetValue(nameof(EventSubmissionDisabled), false);
            settings.DisabledPipelineActions = configRoot.GetValueList(nameof(DisabledPipelineActions), String.Empty);
            settings.DisabledPlugins = configRoot.GetValueList(nameof(DisabledPlugins), String.Empty);
            settings.MaximumEventPostSize = configRoot.GetValue(nameof(MaximumEventPostSize), 200000).NormalizeValue();
            settings.MaximumRetentionDays = configRoot.GetValue(nameof(MaximumRetentionDays), 180).NormalizeValue();
            settings.MetricsServerName = configRoot.GetValue<string>(nameof(MetricsServerName));
            settings.MetricsServerPort = configRoot.GetValue(nameof(MetricsServerPort), 8125);
            settings.EnableMetricsReporting = configRoot.GetValue(nameof(EnableMetricsReporting), !String.IsNullOrEmpty(settings.MetricsServerName));
            settings.IntercomAppSecret = configRoot.GetValue<string>(nameof(IntercomAppSecret));
            settings.GoogleAppId = configRoot.GetValue<string>(nameof(GoogleAppId));
            settings.GoogleAppSecret = configRoot.GetValue<string>(nameof(GoogleAppSecret));
            settings.GoogleGeocodingApiKey = configRoot.GetValue<string>(nameof(GoogleGeocodingApiKey));
            settings.SlackAppId = configRoot.GetValue<string>(nameof(SlackAppId));
            settings.SlackAppSecret = configRoot.GetValue<string>(nameof(SlackAppSecret));
            settings.MicrosoftAppId = configRoot.GetValue<string>(nameof(MicrosoftAppId));
            settings.MicrosoftAppSecret = configRoot.GetValue<string>(nameof(MicrosoftAppSecret));
            settings.FacebookAppId = configRoot.GetValue<string>(nameof(FacebookAppId));
            settings.FacebookAppSecret = configRoot.GetValue<string>(nameof(FacebookAppSecret));
            settings.GitHubAppId = configRoot.GetValue<string>(nameof(GitHubAppId));
            settings.GitHubAppSecret = configRoot.GetValue<string>(nameof(GitHubAppSecret));
            settings.StripeApiKey = configRoot.GetValue<string>(nameof(StripeApiKey));
            settings.StripeWebHookSigningSecret = configRoot.GetValue<string>(nameof(StripeWebHookSigningSecret));
            if (settings.EnableBilling)
                StripeConfiguration.SetApiKey(settings.StripeApiKey);

            settings.StorageFolder = configRoot.GetValue<string>(nameof(StorageFolder), "|DataDirectory|\\storage");
            settings.BulkBatchSize = configRoot.GetValue(nameof(BulkBatchSize), 1000);

            settings.EnableRepositoryNotifications = configRoot.GetValue(nameof(EnableRepositoryNotifications), true);
            settings.EnableWebSockets = configRoot.GetValue(nameof(EnableWebSockets), true);
            settings.EnableBootstrapStartupActions = configRoot.GetValue(nameof(EnableBootstrapStartupActions), true);
            settings.EnableAccountCreation = configRoot.GetValue(nameof(EnableAccountCreation), true);
            settings.EnableDailySummary = configRoot.GetValue(nameof(EnableDailySummary), settings.AppMode == AppMode.Production);
            settings.AllowedOutboundAddresses = configRoot.GetValueList(nameof(AllowedOutboundAddresses), "exceptionless.io").Select(v => v.ToLowerInvariant()).ToList();
            settings.TestEmailAddress = configRoot.GetValue(nameof(TestEmailAddress), "noreply@exceptionless.io");
            settings.SmtpFrom = configRoot.GetValue(nameof(SmtpFrom), "Exceptionless <noreply@exceptionless.io>");
            settings.SmtpHost = configRoot.GetValue(nameof(SmtpHost), "localhost");
            settings.SmtpPort = configRoot.GetValue(nameof(SmtpPort), String.Equals(settings.SmtpHost, "localhost") ? 25 : 587);
            settings.SmtpEncryption = configRoot.GetValue(nameof(SmtpEncryption), settings.GetDefaultSmtpEncryption(settings.SmtpPort));
            settings.SmtpUser = configRoot.GetValue<string>(nameof(SmtpUser));
            settings.SmtpPassword = configRoot.GetValue<string>(nameof(SmtpPassword));

            if (String.IsNullOrWhiteSpace(settings.SmtpUser) != String.IsNullOrWhiteSpace(settings.SmtpPassword))
                throw new ArgumentException("Must specify both the SmtpUser and the SmtpPassword, or neither.");

            settings.AzureStorageConnectionString = configRoot.GetConnectionString(nameof(AzureStorageConnectionString));
            settings.AzureStorageQueueConnectionString = configRoot.GetConnectionString(nameof(AzureStorageQueueConnectionString));
            settings.AliyunStorageConnectionString = configRoot.GetConnectionString(nameof(AliyunStorageConnectionString));
            settings.MinioStorageConnectionString = configRoot.GetConnectionString(nameof(MinioStorageConnectionString));

            settings.DisableIndexConfiguration = configRoot.GetValue(nameof(DisableIndexConfiguration), false);
            settings.EnableSnapshotJobs = configRoot.GetValue(nameof(EnableSnapshotJobs), String.IsNullOrEmpty(settings.AppScopePrefix) && settings.AppMode == AppMode.Production);
            settings.ElasticSearchConnectionString = configRoot.GetConnectionString(nameof(ElasticSearchConnectionString)) ?? "http://localhost:9200";
            settings.ElasticSearchNumberOfShards = configRoot.GetValue(nameof(ElasticSearchNumberOfShards), 1);
            settings.ElasticSearchNumberOfReplicas = configRoot.GetValue(nameof(ElasticSearchNumberOfReplicas), settings.AppMode == AppMode.Production ? 1 : 0);
            settings.ElasticSearchFieldsLimit = configRoot.GetValue(nameof(ElasticSearchFieldsLimit), 1500);
            settings.EnableElasticsearchMapperSizePlugin = configRoot.GetValue(nameof(EnableElasticsearchMapperSizePlugin), settings.AppMode != AppMode.Development);

            settings.RedisConnectionString = configRoot.GetConnectionString(nameof(RedisConnectionString));

            settings.LdapConnectionString = configRoot.GetConnectionString(nameof(LdapConnectionString));
            settings.EnableActiveDirectoryAuth = configRoot.GetValue(nameof(EnableActiveDirectoryAuth), !String.IsNullOrEmpty(settings.LdapConnectionString));

            try {
                var versionInfo = FileVersionInfo.GetVersionInfo(typeof(Settings).Assembly.Location);
                settings.Version = versionInfo.FileVersion;
                settings.InformationalVersion = versionInfo.ProductVersion;
            } catch { }

            Current = settings;
        }

        private SmtpEncryption GetDefaultSmtpEncryption(int port) {
            switch (port) {
                case 465:
                    return SmtpEncryption.SSL;
                case 587:
                case 2525:
                    return SmtpEncryption.StartTLS;
                default:
                    return SmtpEncryption.None;
            }
        }
    }

    public enum AppMode {
        Development,
        Staging,
        Production
    }

    public enum SmtpEncryption {
        None,
        StartTLS,
        SSL
    }
}