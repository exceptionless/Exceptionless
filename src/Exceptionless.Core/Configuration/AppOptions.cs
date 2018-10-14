using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Utility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Stripe;

namespace Exceptionless.Core {
    public class AppOptions {
        public string BaseURL { get; internal set; }

        /// <summary>
        /// Internal project id keeps us from recursively logging to our self
        /// </summary>
        public string InternalProjectId { get; internal set; }

        /// <summary>
        /// Configures the exceptionless client api key, which logs all internal errors and log messages.
        /// </summary>
        public string ExceptionlessApiKey { get; internal set; }

        /// <summary>
        /// Configures the exceptionless client server url, which logs all internal errors and log messages.
        /// </summary>
        public string ExceptionlessServerUrl { get; internal set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public AppMode AppMode { get; internal set; }

        public string AppScope { get; internal set; }

        public bool HasAppScope => !String.IsNullOrEmpty(AppScope);

        public string AppScopePrefix => HasAppScope ? AppScope + "-" : String.Empty;

        public string QueueScope { get; set; }

        public string QueueScopePrefix => !String.IsNullOrEmpty(QueueScope) ? QueueScope + "-" : AppScopePrefix;

        public bool RunJobsInProcess { get; internal set; }

        public int JobsIterationLimit { get; set; }

        public int BotThrottleLimit { get; internal set; }

        public int ApiThrottleLimit { get; internal set; }

        public bool EnableArchive { get; internal set; }

        public bool EventSubmissionDisabled { get; internal set; }

        internal List<string> DisabledPipelineActions { get; set; }
        internal List<string> DisabledPlugins { get; set; }

        /// <summary>
        /// In bytes
        /// </summary>
        public long MaximumEventPostSize { get; internal set; }

        public int MaximumRetentionDays { get; internal set; }

        public string ApplicationInsightsKey { get; internal set; }

        public IConnectionString CacheConnectionString { get; set; }

        public IConnectionString LdapConnectionString { get; set; }

        public IConnectionString MessagingConnectionString { get; set; }

        public IConnectionString MetricsConnectionString { get; set; }

        public bool EnableMetricsReporting { get; internal set; }

        public IConnectionString StorageConnectionString { get; set; }

        public IConnectionString QueueConnectionString { get; set; }

        public bool EnableSnapshotJobs { get; set; }

        public bool DisableIndexConfiguration { get; set; }

        public bool EnableBootstrapStartupActions { get; internal set; }

        public bool EnableActiveDirectoryAuth { get; internal set; }

        public bool EnableRepositoryNotifications { get; internal set; }

        public bool EnableWebSockets { get; internal set; }

        public string Version { get; internal set; }

        public string InformationalVersion { get; internal set; }

        public bool EnableIntercom => !String.IsNullOrEmpty(IntercomAppSecret);

        public string IntercomAppSecret { get; internal set; }

        public string MicrosoftAppId { get; internal set; }

        public string MicrosoftAppSecret { get; internal set; }

        public string FacebookAppId { get; internal set; }

        public string FacebookAppSecret { get; internal set; }

        public string GitHubAppId { get; internal set; }

        public string GitHubAppSecret { get; internal set; }

        public string GoogleAppId { get; internal set; }

        public string GoogleAppSecret { get; internal set; }

        public string GoogleGeocodingApiKey { get; internal set; }

        public string SlackAppId { get; internal set; }

        public string SlackAppSecret { get; internal set; }

        public bool EnableSlack => !String.IsNullOrEmpty(SlackAppId);

        public bool EnableBilling => !String.IsNullOrEmpty(StripeApiKey);

        public string StripeApiKey { get; internal set; }

        public string StripeWebHookSigningSecret { get; set; }

        public int BulkBatchSize { get; internal set; }

        public bool EnableAccountCreation { get; internal set; }

        public bool EnableDailySummary { get; internal set; }

        /// <summary>
        /// All emails that do not match the AllowedOutboundAddresses will be sent to this address in QA mode
        /// </summary>
        public string TestEmailAddress { get; internal set; }

        /// <summary>
        /// Email addresses that match this comma delimited list of domains and email addresses will be allowed to be sent out in QA mode
        /// </summary>
        public List<string> AllowedOutboundAddresses { get; internal set; }

        public string SmtpFrom { get; internal set; }

        public string SmtpHost { get; internal set; }

        public int SmtpPort { get; internal set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public SmtpEncryption SmtpEncryption { get; internal set; }

        public string SmtpUser { get; internal set; }

        public string SmtpPassword { get; internal set; }

        public static AppOptions Current { get; internal set; }

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

    public class ConfigureAppOptions : IConfigureOptions<AppOptions> {
        private readonly IConfiguration _configuration;

        public ConfigureAppOptions(IConfiguration configuration) {
            _configuration = configuration;
        }

        public void Configure(AppOptions options) {
            options.BaseURL = _configuration.GetValue<string>(nameof(options.BaseURL))?.TrimEnd('/');
            options.InternalProjectId = _configuration.GetValue(nameof(options.InternalProjectId), "54b56e480ef9605a88a13153");
            options.ExceptionlessApiKey = _configuration.GetValue<string>(nameof(options.ExceptionlessApiKey));
            options.ExceptionlessServerUrl = _configuration.GetValue<string>(nameof(options.ExceptionlessServerUrl));

            options.AppMode = _configuration.GetValue(nameof(options.AppMode), AppMode.Production);
            options.QueueScope = _configuration.GetValue(nameof(options.QueueScope), String.Empty);
            options.AppScope = _configuration.GetValue(nameof(options.AppScope), String.Empty);
            options.RunJobsInProcess = _configuration.GetValue(nameof(options.RunJobsInProcess), options.AppMode == AppMode.Development);
            options.JobsIterationLimit = _configuration.GetValue(nameof(options.JobsIterationLimit), -1);
            options.BotThrottleLimit = _configuration.GetValue(nameof(options.BotThrottleLimit), 25).NormalizeValue();

            options.ApiThrottleLimit = _configuration.GetValue(nameof(options.ApiThrottleLimit), options.AppMode == AppMode.Development ? Int32.MaxValue : 3500).NormalizeValue();
            options.EnableArchive = _configuration.GetValue(nameof(options.EnableArchive), true);
            options.EventSubmissionDisabled = _configuration.GetValue(nameof(options.EventSubmissionDisabled), false);
            options.DisabledPipelineActions = _configuration.GetValueList(nameof(options.DisabledPipelineActions), String.Empty);
            options.DisabledPlugins = _configuration.GetValueList(nameof(options.DisabledPlugins), String.Empty);
            options.MaximumEventPostSize = _configuration.GetValue(nameof(options.MaximumEventPostSize), 200000).NormalizeValue();
            options.MaximumRetentionDays = _configuration.GetValue(nameof(options.MaximumRetentionDays), 180).NormalizeValue();
            options.ApplicationInsightsKey = _configuration.GetValue<string>(nameof(options.ApplicationInsightsKey));

            options.IntercomAppSecret = _configuration.GetValue<string>(nameof(options.IntercomAppSecret));
            options.GoogleAppId = _configuration.GetValue<string>(nameof(options.GoogleAppId));
            options.GoogleAppSecret = _configuration.GetValue<string>(nameof(options.GoogleAppSecret));
            options.GoogleGeocodingApiKey = _configuration.GetValue<string>(nameof(options.GoogleGeocodingApiKey));
            options.SlackAppId = _configuration.GetValue<string>(nameof(options.SlackAppId));
            options.SlackAppSecret = _configuration.GetValue<string>(nameof(options.SlackAppSecret));
            options.MicrosoftAppId = _configuration.GetValue<string>(nameof(options.MicrosoftAppId));
            options.MicrosoftAppSecret = _configuration.GetValue<string>(nameof(options.MicrosoftAppSecret));
            options.FacebookAppId = _configuration.GetValue<string>(nameof(options.FacebookAppId));
            options.FacebookAppSecret = _configuration.GetValue<string>(nameof(options.FacebookAppSecret));
            options.GitHubAppId = _configuration.GetValue<string>(nameof(options.GitHubAppId));
            options.GitHubAppSecret = _configuration.GetValue<string>(nameof(options.GitHubAppSecret));
            options.StripeApiKey = _configuration.GetValue<string>(nameof(options.StripeApiKey));
            options.StripeWebHookSigningSecret = _configuration.GetValue<string>(nameof(options.StripeWebHookSigningSecret));
            if (options.EnableBilling)
                StripeConfiguration.SetApiKey(options.StripeApiKey);

            options.BulkBatchSize = _configuration.GetValue(nameof(options.BulkBatchSize), 1000);

            options.EnableRepositoryNotifications = _configuration.GetValue(nameof(options.EnableRepositoryNotifications), true);
            options.EnableWebSockets = _configuration.GetValue(nameof(options.EnableWebSockets), true);
            options.EnableBootstrapStartupActions = _configuration.GetValue(nameof(options.EnableBootstrapStartupActions), true);
            options.EnableAccountCreation = _configuration.GetValue(nameof(options.EnableAccountCreation), true);
            options.EnableDailySummary = _configuration.GetValue(nameof(options.EnableDailySummary), options.AppMode == AppMode.Production);
            options.AllowedOutboundAddresses = _configuration.GetValueList(nameof(options.AllowedOutboundAddresses), "exceptionless.io").Select(v => v.ToLowerInvariant()).ToList();
            options.TestEmailAddress = _configuration.GetValue(nameof(options.TestEmailAddress), "noreply@exceptionless.io");
            options.SmtpFrom = _configuration.GetValue(nameof(options.SmtpFrom), "Exceptionless <noreply@exceptionless.io>");
            options.SmtpHost = _configuration.GetValue(nameof(options.SmtpHost), "localhost");
            options.SmtpPort = _configuration.GetValue(nameof(options.SmtpPort), String.Equals(options.SmtpHost, "localhost") ? 25 : 587);
            options.SmtpEncryption = _configuration.GetValue(nameof(options.SmtpEncryption), options.GetDefaultSmtpEncryption(options.SmtpPort));
            options.SmtpUser = _configuration.GetValue<string>(nameof(options.SmtpUser));
            options.SmtpPassword = _configuration.GetValue<string>(nameof(options.SmtpPassword));

            if (String.IsNullOrWhiteSpace(options.SmtpUser) != String.IsNullOrWhiteSpace(options.SmtpPassword))
                throw new ArgumentException("Must specify both the SmtpUser and the SmtpPassword, or neither.");

            string cacheConnectionString = _configuration.GetConnectionString("Cache");
            if (!String.IsNullOrEmpty(cacheConnectionString))
                options.CacheConnectionString = new DefaultConnectionString(cacheConnectionString);

            string ldapConnectionString = _configuration.GetConnectionString("Ldap");
            if (!String.IsNullOrEmpty(ldapConnectionString))
                options.LdapConnectionString = new DefaultConnectionString(ldapConnectionString);

            options.EnableActiveDirectoryAuth = _configuration.GetValue(nameof(options.EnableActiveDirectoryAuth), options.LdapConnectionString != null);

            string messagingConnectionString = _configuration.GetConnectionString("Messaging");
            if (!String.IsNullOrEmpty(messagingConnectionString))
                options.MessagingConnectionString = new DefaultConnectionString(messagingConnectionString);

            string metricsConnectionString = _configuration.GetConnectionString("Metric");
            if (!String.IsNullOrEmpty(metricsConnectionString))
                options.MetricsConnectionString = new DefaultConnectionString(metricsConnectionString);

            options.EnableMetricsReporting = _configuration.GetValue(nameof(options.EnableMetricsReporting), options.MetricsConnectionString != null);

            string storageConnectionString = _configuration.GetConnectionString("Storage");
            if (!String.IsNullOrEmpty(storageConnectionString))
                options.StorageConnectionString = new DefaultConnectionString(storageConnectionString);

            string queueConnectionString = _configuration.GetConnectionString("Queue");
            if (!String.IsNullOrEmpty(queueConnectionString))
                options.QueueConnectionString = new DefaultConnectionString(queueConnectionString);

            options.DisableIndexConfiguration = _configuration.GetValue(nameof(options.DisableIndexConfiguration), false);
            options.EnableSnapshotJobs = _configuration.GetValue(nameof(options.EnableSnapshotJobs), String.IsNullOrEmpty(options.AppScopePrefix) && options.AppMode == AppMode.Production);

            try {
                var versionInfo = FileVersionInfo.GetVersionInfo(typeof(AppOptions).Assembly.Location);
                options.Version = versionInfo.FileVersion;
                options.InformationalVersion = versionInfo.ProductVersion;
            }
            catch { }

            AppOptions.Current = options;
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