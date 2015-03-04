using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using Exceptionless.Core.Extensions;

namespace Exceptionless.Core {
    public class Settings {
        public bool EnableSSL { get; private set; }

        public string BaseURL { get; private set; }

        public string InternalProjectId { get; private set; }

        public WebsiteMode WebsiteMode { get; private set; }

        public string TestEmailAddress { get; private set; }

        public List<string> AllowedOutboundAddresses { get; private set; }

        public bool RunJobsInProcess { get; private set; }

        public bool LogJobLocks { get; private set; }

        public bool LogJobEvents { get; private set; }

        public bool LogJobCompleted { get; private set; }

        public bool LogStackingInfo { get; private set; }

        public bool AppendMachineNameToDatabase { get; private set; }

        public bool SaveIncomingErrorsToDisk { get; private set; }

        public string IncomingErrorPath { get; private set; }

        public bool EnableLogErrorReporting { get; private set; }

        public bool EnableSignalR { get; private set; }

        public int BotThrottleLimit { get; private set; }

        public int ApiThrottleLimit { get; private set; }

        public long MaximumEventPostSize { get; private set; }

        public bool EnableDailySummary { get; private set; }

        public bool ShouldAutoUpgradeDatabase { get; private set; }

        public string MetricsServerName { get; private set; }

        public int MetricsServerPort { get; private set; }

        public bool EnableMetricsReporting { get; private set; }

        public string RedisConnectionString { get; private set; }

        public bool EnableRedis { get; private set; }

        public string MongoConnectionString { get; private set; }

        public string ElasticSearchConnectionString { get; set; }

        public string GeoIPDatabasePath { get; set; }

        public string Version { get; private set; }

        public bool EnableIntercom { get { return !String.IsNullOrEmpty(IntercomAppSecret); } }

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

        public bool EnableBilling { get { return !String.IsNullOrEmpty(StripeApiKey); } }

        public string StripeApiKey { get; private set; }

        public string StorageFolder { get; private set; }

        public string AzureStorageConnectionString { get; set; }

        public bool EnableAzureStorage { get; private set; }

        public int BulkBatchSize { get; private set; }
        
        private static Settings Init() {
            var settings = new Settings();
            settings.EnableSSL = GetBool("EnableSSL");

            string value = GetString("BaseURL");
            if (!String.IsNullOrEmpty(value)) {
                if (value.EndsWith("/"))
                    value = value.Substring(0, value.Length - 1);

                if (settings.EnableSSL && value.StartsWith("http:"))
                    value = value.ReplaceFirst("http:", "https:");
                else if (!settings.EnableSSL && value.StartsWith("https:"))
                    value = value.ReplaceFirst("https:", "http:");

                settings.BaseURL = value;
            }

            settings.InternalProjectId = GetString("InternalProjectId");
            settings.WebsiteMode = GetEnum<WebsiteMode>("WebsiteMode", WebsiteMode.Dev);
            settings.TestEmailAddress = GetString("TestEmailAddress");
            settings.AllowedOutboundAddresses = GetStringList("AllowedOutboundAddresses", "exceptionless.io").Select(v => v.ToLowerInvariant()).ToList();
            settings.GeoIPDatabasePath = GetString("GeoIPDatabasePath"); 
            settings.RunJobsInProcess = GetBool("RunJobsInProcess", true);
            settings.LogJobLocks = GetBool("LogJobLocks");
            settings.LogJobEvents = GetBool("LogJobEvents");
            settings.LogJobCompleted = GetBool("LogJobCompleted");
            settings.LogStackingInfo = GetBool("LogStackingInfo");
            settings.AppendMachineNameToDatabase = GetBool("AppendMachineNameToDatabase");
            settings.SaveIncomingErrorsToDisk = GetBool("SaveIncomingErrorsToDisk");
            settings.IncomingErrorPath = GetString("IncomingErrorPath");
            settings.EnableLogErrorReporting = GetBool("EnableLogErrorReporting");
            settings.EnableSignalR = GetBool("EnableSignalR", true);
            settings.BotThrottleLimit = GetInt("BotThrottleLimit", 25);
            settings.ApiThrottleLimit = GetInt("ApiThrottleLimit", Int32.MaxValue);
            settings.MaximumEventPostSize = GetInt("MaximumEventPostSize", Int32.MaxValue);
            settings.EnableDailySummary = GetBool("EnableDailySummary");
            settings.ShouldAutoUpgradeDatabase = GetBool("ShouldAutoUpgradeDatabase", true);
            settings.MetricsServerName = GetString("MetricsServerName") ?? "127.0.0.1";
            settings.MetricsServerPort = GetInt("MetricsServerPort", 12000);
            settings.EnableMetricsReporting = GetBool("EnableMetricsReporting");
            settings.IntercomAppSecret = GetString("IntercomAppSecret");
            settings.EnableAccountCreation = GetBool("EnableAccountCreation", true);
            settings.GoogleAppId = GetString("GoogleAppId");
            settings.GoogleAppSecret = GetString("GoogleAppSecret");
            settings.MicrosoftAppId = GetString("MicrosoftAppId");
            settings.MicrosoftAppSecret = GetString("MicrosoftAppSecret");
            settings.FacebookAppId = GetString("FacebookAppId");
            settings.FacebookAppSecret = GetString("FacebookAppSecret");
            settings.GitHubAppId = GetString("GitHubAppId");
            settings.GitHubAppSecret = GetString("GitHubAppSecret");
            settings.StripeApiKey = GetString("StripeApiKey");
            settings.StorageFolder = GetString("StorageFolder");
            settings.BulkBatchSize = GetInt("BulkBatchSize", 1000);

            string connectionString = GetConnectionString("RedisConnectionString");
            if (!String.IsNullOrEmpty(connectionString)) {
                settings.RedisConnectionString = connectionString;
                settings.EnableRedis = GetBool("EnableRedis", !String.IsNullOrEmpty(settings.RedisConnectionString));
            }

            connectionString = GetConnectionString("AzureStorageConnectionString");
            if (!String.IsNullOrEmpty(connectionString)) {
                settings.AzureStorageConnectionString = connectionString;
                settings.EnableAzureStorage = GetBool("EnableAzureStorage", !String.IsNullOrEmpty(settings.AzureStorageConnectionString));
            }

            connectionString = GetConnectionString("MongoConnectionString");
            if (!String.IsNullOrEmpty(connectionString))
                settings.MongoConnectionString = connectionString;

            connectionString = GetConnectionString("ElasticSearchConnectionString");
            if (!String.IsNullOrEmpty(connectionString))
                settings.ElasticSearchConnectionString = connectionString;

            settings.Version = FileVersionInfo.GetVersionInfo(typeof(Settings).Assembly.Location).ProductVersion;

            return settings;
        }

        #region Singleton

        protected Settings() {}

        private static readonly Lazy<Settings> _instance = new Lazy<Settings>(Init);

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        [DebuggerNonUserCode]
        public static Settings Current { get { return _instance.Value; } }

        #endregion

        private static bool GetBool(string name, bool defaultValue = false) {
            string value = GetEnvironmentalVariable(name);
            if (String.IsNullOrEmpty(value))
                return ConfigurationManager.AppSettings.GetBool(name, defaultValue);
            
            bool boolean;
            return Boolean.TryParse(value, out boolean) ? boolean : defaultValue;
        }

        private static string GetConnectionString(string name) {
            string value = GetEnvironmentalVariable(name);
            if (!String.IsNullOrEmpty(value))
                return value;

            var connectionString = ConfigurationManager.ConnectionStrings[name];
            return connectionString != null ? connectionString.ConnectionString : null;
        }

        private static T GetEnum<T>(string name, T? defaultValue = null) where T : struct {
            string value = GetEnvironmentalVariable(name);
            if (String.IsNullOrEmpty(value))
                return ConfigurationManager.AppSettings.GetEnum(name, defaultValue);

            try {
                return (T)Enum.Parse(typeof(T), value, true);
            } catch (ArgumentException ex) {
                if (defaultValue.HasValue && defaultValue is T)
                    return (T)defaultValue;

                string message = String.Format("Configuration key '{0}' has value '{1}' that could not be parsed as a member of the {2} enum type.", name, value, typeof(T).Name);
                throw new ConfigurationErrorsException(message, ex);
            }
        }

        private static int GetInt(string name, int defaultValue = 0) {
            string value = GetEnvironmentalVariable(name);
            if (String.IsNullOrEmpty(value))
                return ConfigurationManager.AppSettings.GetInt(name, defaultValue);

            int number;
            return Int32.TryParse(value, out number) ? number : defaultValue;
        }

        private static string GetString(string name) {
            return GetEnvironmentalVariable(name) ?? ConfigurationManager.AppSettings[name];
        }

        private static List<string> GetStringList(string name, string defaultValues = null, char[] separators = null) {
            string value = GetEnvironmentalVariable(name);
            if (String.IsNullOrEmpty(value))
                return ConfigurationManager.AppSettings.GetStringList(name, defaultValues, separators);

            if (separators == null)
                separators = new[] { ',' };

            return value.Split(separators, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
        }

        private static string GetEnvironmentalVariable(string name) {
            if (String.IsNullOrEmpty(name))
                return null;

            try {
                return Environment.GetEnvironmentVariable(name);
            } catch (Exception) {
                return null;
            }
        }
    }

    public enum WebsiteMode {
        Production,
        QA,
        Dev
    }
}