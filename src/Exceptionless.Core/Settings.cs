using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Exceptionless.Core.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Exceptionless.Core {
    public class Settings {
        public bool EnableSSL { get; private set; }

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
        public WebsiteMode WebsiteMode { get; private set; }

        public string AppScope { get; private set; }

        public bool HasAppScope => !String.IsNullOrEmpty(AppScope);

        public string AppScopePrefix => HasAppScope ? AppScope + "-" : String.Empty;

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

        public bool EnableRedis { get; private set; }

        public bool DisableSnapshotJobs { get; set; }

        public bool DisableIndexConfiguration { get; set; }

        public bool DisableBootstrapStartupActions { get; private set; }

        public string ElasticSearchConnectionString { get; private set; }

        public int ElasticSearchNumberOfShards { get; private set; }

        public int ElasticSearchNumberOfReplicas { get; private set; }

        public bool EnableElasticsearchMapperSizePlugin { get; private set; }

        public string LdapConnectionString { get; private set; }

        public bool EnableActiveDirectoryAuth { get; internal set; }

        public bool EnableWebSockets { get; private set; }

        public string Version { get; private set; }

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

        public string StorageFolder { get; private set; }

        public string AzureStorageConnectionString { get; private set; }

        public bool EnableAzureStorage { get; private set; }

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

        public static void Initialize(IConfiguration configRoot) {
            var settings = new Settings();
            var config = configRoot.GetSection("AppSettings");

            settings.EnableSSL = config.GetValue(nameof(EnableSSL), false);

            string value = config.GetValue<string>(nameof(BaseURL));
            if (!String.IsNullOrEmpty(value)) {
                if (value.EndsWith("/"))
                    value = value.Substring(0, value.Length - 1);

                if (settings.EnableSSL && value.StartsWith("http:"))
                    value = value.ReplaceFirst("http:", "https:");
                else if (!settings.EnableSSL && value.StartsWith("https:"))
                    value = value.ReplaceFirst("https:", "http:");

                settings.BaseURL = value;
            }

            settings.InternalProjectId = config.GetValue(nameof(InternalProjectId), "54b56e480ef9605a88a13153");
            settings.ExceptionlessApiKey = config.GetValue<string>(nameof(ExceptionlessApiKey));
            settings.ExceptionlessServerUrl = config.GetValue<string>(nameof(ExceptionlessServerUrl));
            settings.WebsiteMode = config.GetValue(nameof(WebsiteMode), WebsiteMode.Dev);
            settings.AppScope = config.GetValue(nameof(AppScope), String.Empty);

            settings.RunJobsInProcess = config.GetValue(nameof(RunJobsInProcess), true);
            settings.JobsIterationLimit = config.GetValue(nameof(JobsIterationLimit), -1);
            settings.BotThrottleLimit = config.GetValue(nameof(BotThrottleLimit), 25);
            settings.ApiThrottleLimit = config.GetValue(nameof(ApiThrottleLimit), Int32.MaxValue);
            settings.EnableArchive = config.GetValue(nameof(EnableArchive), true);
            settings.EventSubmissionDisabled = config.GetValue(nameof(EventSubmissionDisabled), false);
            settings.DisabledPipelineActions = config.GetValueList(nameof(DisabledPipelineActions), String.Empty);
            settings.DisabledPlugins = config.GetValueList(nameof(DisabledPlugins), String.Empty);
            settings.MaximumEventPostSize = config.GetValue(nameof(MaximumEventPostSize), 1000000);
            settings.MaximumRetentionDays = config.GetValue(nameof(MaximumRetentionDays), 180);
            settings.MetricsServerName = config.GetValue<string>(nameof(MetricsServerName)) ?? "127.0.0.1";
            settings.MetricsServerPort = config.GetValue(nameof(MetricsServerPort), 8125);
            settings.EnableMetricsReporting = config.GetValue(nameof(EnableMetricsReporting), true);
            settings.IntercomAppSecret = config.GetValue<string>(nameof(IntercomAppSecret));
            settings.GoogleAppId = config.GetValue<string>(nameof(GoogleAppId));
            settings.GoogleAppSecret = config.GetValue<string>(nameof(GoogleAppSecret));
            settings.GoogleGeocodingApiKey = config.GetValue<string>(nameof(GoogleGeocodingApiKey));
            settings.SlackAppId = config.GetValue<string>(nameof(SlackAppId));
            settings.SlackAppSecret = config.GetValue<string>(nameof(SlackAppSecret));
            settings.MicrosoftAppId = config.GetValue<string>(nameof(MicrosoftAppId));
            settings.MicrosoftAppSecret = config.GetValue<string>(nameof(MicrosoftAppSecret));
            settings.FacebookAppId = config.GetValue<string>(nameof(FacebookAppId));
            settings.FacebookAppSecret = config.GetValue<string>(nameof(FacebookAppSecret));
            settings.GitHubAppId = config.GetValue<string>(nameof(GitHubAppId));
            settings.GitHubAppSecret = config.GetValue<string>(nameof(GitHubAppSecret));
            settings.StripeApiKey = config.GetValue<string>(nameof(StripeApiKey));
            settings.StorageFolder = config.GetValue<string>(nameof(StorageFolder), "|DataDirectory|\\storage");
            settings.BulkBatchSize = config.GetValue(nameof(BulkBatchSize), 1000);

            settings.EnableAccountCreation = config.GetValue(nameof(EnableAccountCreation), true);
            settings.EnableDailySummary = config.GetValue(nameof(EnableDailySummary), true);
            settings.AllowedOutboundAddresses = config.GetValueList(nameof(AllowedOutboundAddresses), "exceptionless.io").Select(v => v.ToLowerInvariant()).ToList();
            settings.TestEmailAddress = config.GetValue(nameof(TestEmailAddress), "noreply@exceptionless.io");
            settings.SmtpFrom = config.GetValue(nameof(SmtpFrom), "Exceptionless <noreply@exceptionless.io>");
            settings.SmtpHost = config.GetValue(nameof(SmtpHost), "localhost");
            settings.SmtpPort = config.GetValue(nameof(SmtpPort), String.Equals(settings.SmtpHost, "localhost") ? 25 : 587);
            settings.SmtpEncryption = config.GetValue(nameof(SmtpEncryption), settings.GetDefaultSmtpEncryption(settings.SmtpPort));
            settings.SmtpUser = config.GetValue<string>(nameof(SmtpUser));
            settings.SmtpPassword = config.GetValue<string>(nameof(SmtpPassword));

            if (String.IsNullOrWhiteSpace(settings.SmtpUser) != String.IsNullOrWhiteSpace(settings.SmtpPassword))
                throw new ArgumentException("Must specify both the SmtpUser and the SmtpPassword, or neither.");

            settings.AzureStorageConnectionString = configRoot.GetConnectionString(nameof(AzureStorageConnectionString));
            settings.EnableAzureStorage = config.GetValue(nameof(EnableAzureStorage), !String.IsNullOrEmpty(settings.AzureStorageConnectionString));

            settings.DisableBootstrapStartupActions = config.GetValue(nameof(DisableIndexConfiguration), false);
            settings.DisableIndexConfiguration = config.GetValue(nameof(DisableIndexConfiguration), false);
            settings.DisableSnapshotJobs = config.GetValue(nameof(DisableSnapshotJobs), !String.IsNullOrEmpty(settings.AppScopePrefix));
            settings.ElasticSearchConnectionString = configRoot.GetConnectionString(nameof(ElasticSearchConnectionString)) ?? "http://localhost:9200";
            settings.ElasticSearchNumberOfShards = config.GetValue(nameof(ElasticSearchNumberOfShards), 1);
            settings.ElasticSearchNumberOfReplicas = config.GetValue(nameof(ElasticSearchNumberOfReplicas), 0);
            settings.EnableElasticsearchMapperSizePlugin = config.GetValue(nameof(EnableElasticsearchMapperSizePlugin), false);

            settings.RedisConnectionString = configRoot.GetConnectionString(nameof(RedisConnectionString));
            settings.EnableRedis = config.GetValue(nameof(EnableRedis), !String.IsNullOrEmpty(settings.RedisConnectionString));

            settings.LdapConnectionString = configRoot.GetConnectionString(nameof(LdapConnectionString));
            settings.EnableActiveDirectoryAuth = config.GetValue(nameof(EnableActiveDirectoryAuth), !String.IsNullOrEmpty(settings.LdapConnectionString));

            settings.EnableWebSockets = config.GetValue(nameof(EnableWebSockets), true);
            settings.Version = FileVersionInfo.GetVersionInfo(typeof(Settings).Assembly.Location).ProductVersion;

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

        public ILoggerFactory GetLoggerFactory() {
            return new LoggerFactory();
        }
    }

    public enum WebsiteMode {
        Production,
        QA,
        Dev
    }

    public enum SmtpEncryption {
        None,
        StartTLS,
        SSL
    }
}