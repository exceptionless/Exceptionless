using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Exceptionless.Core.Extensions;
using Foundatio.Logging;

namespace Exceptionless.Core {
    public class Settings : SettingsBase<Settings> {
        public bool EnableSSL { get; private set; }

        public string BaseURL { get; private set; }

        /// <summary>
        /// Internal project id keeps us from recursively logging to ourself
        /// </summary>
        public string InternalProjectId { get; private set; }

        public WebsiteMode WebsiteMode { get; private set; }

        public string AppScope { get; private set; }

        public bool HasAppScope => !String.IsNullOrEmpty(AppScope);

        public string AppScopePrefix => HasAppScope ? AppScope + "-" : String.Empty;

        public string TestEmailAddress { get; private set; }

        public List<string> AllowedOutboundAddresses { get; private set; }

        public bool RunJobsInProcess { get; private set; }

        public int BotThrottleLimit { get; private set; }

        public int ApiThrottleLimit { get; private set; }

        public bool EventSubmissionDisabled { get; private set; }

        /// <summary>
        /// In bytes
        /// </summary>
        public long MaximumEventPostSize { get; private set; }

        public int MaximumRetentionDays { get; private set; }

        public bool EnableDailySummary { get; private set; }

        public string MetricsServerName { get; private set; }

        public int MetricsServerPort { get; private set; }

        public bool EnableMetricsReporting { get; private set; }

        public string RedisConnectionString { get; private set; }

        public bool EnableRedis { get; private set; }

        public bool DisableSnapshotJobs { get; set; }

        public bool DisableIndexConfiguration { get; set; }

        public string ElasticSearchConnectionString { get; private set; }

        public int ElasticSearchNumberOfShards { get; private set; }

        public int ElasticSearchNumberOfReplicas { get; private set; }

        public bool EnableElasticsearchTracing { get; private set; }

        public string LdapConnectionString { get; private set; }

        public bool EnableActiveDirectoryAuth { get; internal set; }

        public bool EnableSignalR { get; private set; }

        public string Version { get; private set; }

        public bool EnableIntercom => !String.IsNullOrEmpty(IntercomAppSecret);

        public string IntercomAppSecret { get; private set; }

        public bool EnableAccountCreation { get; internal set; }

        public string MicrosoftAppId { get; private set; }

        public string MicrosoftAppSecret { get; private set; }

        public string FacebookAppId { get; private set; }

        public string FacebookAppSecret { get; private set; }

        public string GitHubAppId { get; private set; }

        public string GitHubAppSecret { get; private set; }

        public string GoogleAppId { get; private set; }

        public string GoogleAppSecret { get; private set; }

        public string GoogleGeocodingApiKey { get; private set; }

        public bool EnableBilling => !String.IsNullOrEmpty(StripeApiKey);

        public string StripeApiKey { get; private set; }

        public string StorageFolder { get; private set; }

        public string AzureStorageConnectionString { get; private set; }

        public bool EnableAzureStorage { get; private set; }

        public int BulkBatchSize { get; private set; }

        internal string SmtpHost { get; private set; }

        internal int SmtpPort { get; private set; }

        internal bool SmtpEnableSsl { get; private set; }

        internal string SmtpUser { get; private set; }

        internal string SmtpPassword { get; private set; }

        public override void Initialize() {
            EnvironmentVariablePrefix = "Exceptionless_";

            EnableSSL = GetBool(nameof(EnableSSL));

            string value = GetString(nameof(BaseURL));
            if (!String.IsNullOrEmpty(value)) {
                if (value.EndsWith("/"))
                    value = value.Substring(0, value.Length - 1);

                if (EnableSSL && value.StartsWith("http:"))
                    value = value.ReplaceFirst("http:", "https:");
                else if (!EnableSSL && value.StartsWith("https:"))
                    value = value.ReplaceFirst("https:", "http:");

                BaseURL = value;
            }

            InternalProjectId = GetString(nameof(InternalProjectId), "54b56e480ef9605a88a13153");
            WebsiteMode = GetEnum<WebsiteMode>(nameof(WebsiteMode), WebsiteMode.Dev);
            AppScope = GetString(nameof(AppScope), String.Empty);
            TestEmailAddress = GetString(nameof(TestEmailAddress));
            AllowedOutboundAddresses = GetStringList(nameof(AllowedOutboundAddresses), "exceptionless.io").Select(v => v.ToLowerInvariant()).ToList();
            RunJobsInProcess = GetBool(nameof(RunJobsInProcess), true);
            BotThrottleLimit = GetInt(nameof(BotThrottleLimit), 25);
            ApiThrottleLimit = GetInt(nameof(ApiThrottleLimit), Int32.MaxValue);
            EventSubmissionDisabled = GetBool(nameof(EventSubmissionDisabled));
            MaximumEventPostSize = GetInt64(nameof(MaximumEventPostSize), 1000000);
            MaximumRetentionDays = GetInt(nameof(MaximumRetentionDays), 180);
            EnableDailySummary = GetBool(nameof(EnableDailySummary));
            MetricsServerName = GetString(nameof(MetricsServerName)) ?? "127.0.0.1";
            MetricsServerPort = GetInt(nameof(MetricsServerPort), 8125);
            EnableMetricsReporting = GetBool(nameof(EnableMetricsReporting));
            IntercomAppSecret = GetString(nameof(IntercomAppSecret));
            EnableAccountCreation = GetBool(nameof(EnableAccountCreation), true);
            GoogleAppId = GetString(nameof(GoogleAppId));
            GoogleAppSecret = GetString(nameof(GoogleAppSecret));
            GoogleGeocodingApiKey = GetString(nameof(GoogleGeocodingApiKey));
            MicrosoftAppId = GetString(nameof(MicrosoftAppId));
            MicrosoftAppSecret = GetString(nameof(MicrosoftAppSecret));
            FacebookAppId = GetString(nameof(FacebookAppId));
            FacebookAppSecret = GetString(nameof(FacebookAppSecret));
            GitHubAppId = GetString(nameof(GitHubAppId));
            GitHubAppSecret = GetString(nameof(GitHubAppSecret));
            StripeApiKey = GetString(nameof(StripeApiKey));
            StorageFolder = GetString(nameof(StorageFolder));
            BulkBatchSize = GetInt(nameof(BulkBatchSize), 1000);

            SmtpHost = GetString(nameof(SmtpHost));
            SmtpPort = GetInt(nameof(SmtpPort), 587);
            SmtpEnableSsl = GetBool(nameof(SmtpEnableSsl), true);
            SmtpUser = GetString(nameof(SmtpUser));
            SmtpPassword = GetString(nameof(SmtpPassword));

            AzureStorageConnectionString = GetConnectionString(nameof(AzureStorageConnectionString));
            EnableAzureStorage = GetBool(nameof(EnableAzureStorage), !String.IsNullOrEmpty(AzureStorageConnectionString));

            DisableIndexConfiguration = GetBool(nameof(DisableIndexConfiguration));
            DisableSnapshotJobs = GetBool(nameof(DisableSnapshotJobs), !String.IsNullOrEmpty(AppScopePrefix));
            ElasticSearchConnectionString = GetConnectionString(nameof(ElasticSearchConnectionString));
            ElasticSearchNumberOfShards = GetInt(nameof(ElasticSearchNumberOfShards), 1);
            ElasticSearchNumberOfReplicas = GetInt(nameof(ElasticSearchNumberOfReplicas), 0);
            EnableElasticsearchTracing = GetBool(nameof(EnableElasticsearchTracing));

            RedisConnectionString = GetConnectionString(nameof(RedisConnectionString));
            EnableRedis = GetBool(nameof(EnableRedis), !String.IsNullOrEmpty(RedisConnectionString));

            LdapConnectionString = GetConnectionString(nameof(LdapConnectionString));
            EnableActiveDirectoryAuth = GetBool(nameof(EnableActiveDirectoryAuth), !String.IsNullOrEmpty(LdapConnectionString));

            EnableSignalR = GetBool(nameof(EnableSignalR), true);

            Version = FileVersionInfo.GetVersionInfo(typeof(Settings).Assembly.Location).ProductVersion;
        }

        public const string JobBootstrappedServiceProvider = "Exceptionless.Insulation.Jobs.JobBootstrappedServiceProvider,Exceptionless.Insulation";

        public LoggerFactory GetLoggerFactory() {
            return new LoggerFactory();
        }
    }

    public enum WebsiteMode {
        Production,
        QA,
        Dev
    }
}
