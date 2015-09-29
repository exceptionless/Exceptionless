using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Exceptionless.Core.Extensions;

namespace Exceptionless.Core {
    public class Settings : SettingsBase<Settings> {
        public bool EnableSSL { get; private set; }

        public string BaseURL { get; private set; }

        public string InternalProjectId { get; private set; }

        public WebsiteMode WebsiteMode { get; private set; }

        public string TestEmailAddress { get; private set; }

        public List<string> AllowedOutboundAddresses { get; private set; }

        public bool RunJobsInProcess { get; private set; }

        public int BotThrottleLimit { get; private set; }

        public int ApiThrottleLimit { get; private set; }

        public long MaximumEventPostSize { get; private set; }

        public bool EnableDailySummary { get; private set; }

        public string MetricsServerName { get; private set; }

        public int MetricsServerPort { get; private set; }

        public bool EnableMetricsReporting { get; private set; }

        public string RedisConnectionString { get; private set; }

        public bool EnableRedis { get; private set; }
        
        public string ElasticSearchConnectionString { get; private set; }

        public string Version { get; private set; }

        public bool EnableIntercom => !String.IsNullOrEmpty(IntercomAppSecret);

        public string IntercomAppSecret { get; private set; }

        public bool EnableAccountCreation { get; private set; }

        public string MicrosoftAppId { get; private set; }

        public string MicrosoftAppSecret { get; private set; }

        public string FacebookAppId { get; private set; }

        public string FacebookAppSecret { get; private set; }

        public string GitHubAppId { get; private set; }

        public string GitHubAppSecret { get; private set; }

        public string GoogleAppId { get; private set; }

        public string GoogleAppSecret { get; private set; }

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

            EnableSSL = GetBool("EnableSSL");

            string value = GetString("BaseURL");
            if (!String.IsNullOrEmpty(value)) {
                if (value.EndsWith("/"))
                    value = value.Substring(0, value.Length - 1);

                if (EnableSSL && value.StartsWith("http:"))
                    value = value.ReplaceFirst("http:", "https:");
                else if (!EnableSSL && value.StartsWith("https:"))
                    value = value.ReplaceFirst("https:", "http:");

                BaseURL = value;
            }

            InternalProjectId = GetString("InternalProjectId");
            WebsiteMode = GetEnum<WebsiteMode>("WebsiteMode", WebsiteMode.Dev);
            TestEmailAddress = GetString("TestEmailAddress");
            AllowedOutboundAddresses = GetStringList("AllowedOutboundAddresses", "exceptionless.io").Select(v => v.ToLowerInvariant()).ToList();
            RunJobsInProcess = GetBool("RunJobsInProcess", true);
            BotThrottleLimit = GetInt("BotThrottleLimit", 25);
            ApiThrottleLimit = GetInt("ApiThrottleLimit", Int32.MaxValue);
            MaximumEventPostSize = GetInt("MaximumEventPostSize", Int32.MaxValue);
            EnableDailySummary = GetBool("EnableDailySummary");
            MetricsServerName = GetString("MetricsServerName") ?? "127.0.0.1";
            MetricsServerPort = GetInt("MetricsServerPort", 8125);
            EnableMetricsReporting = GetBool("EnableMetricsReporting");
            IntercomAppSecret = GetString("IntercomAppSecret");
            EnableAccountCreation = GetBool("EnableAccountCreation", true);
            GoogleAppId = GetString("GoogleAppId");
            GoogleAppSecret = GetString("GoogleAppSecret");
            MicrosoftAppId = GetString("MicrosoftAppId");
            MicrosoftAppSecret = GetString("MicrosoftAppSecret");
            FacebookAppId = GetString("FacebookAppId");
            FacebookAppSecret = GetString("FacebookAppSecret");
            GitHubAppId = GetString("GitHubAppId");
            GitHubAppSecret = GetString("GitHubAppSecret");
            StripeApiKey = GetString("StripeApiKey");
            StorageFolder = GetString("StorageFolder");
            BulkBatchSize = GetInt("BulkBatchSize", 1000);

            SmtpHost = GetString("SmtpHost");
            SmtpPort = GetInt("SmtpPort", 587);
            SmtpEnableSsl = GetBool("SmtpEnableSsl", true);
            SmtpUser = GetString("SmtpUser");
            SmtpPassword = GetString("SmtpPassword");

            AzureStorageConnectionString = GetConnectionString("AzureStorageConnectionString");
            EnableAzureStorage = GetBool("EnableAzureStorage", !String.IsNullOrEmpty(AzureStorageConnectionString));
            
            ElasticSearchConnectionString = GetConnectionString("ElasticSearchConnectionString");

            RedisConnectionString = GetConnectionString("RedisConnectionString");
            EnableRedis = GetBool("EnableRedis", !String.IsNullOrEmpty(RedisConnectionString));

            Version = FileVersionInfo.GetVersionInfo(typeof(Settings).Assembly.Location).ProductVersion;
        }
    }

    public enum WebsiteMode {
        Production,
        QA,
        Dev
    }
}