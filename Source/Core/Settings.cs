#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

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

        public string AppStatsServerName { get; private set; }

        public int AppStatsServerPort { get; private set; }

        public bool EnableAppStats { get; private set; }

        public string RedisConnectionString { get; private set; }

        public bool EnableRedis { get; private set; }

        public string MongoConnectionString { get; private set; }

        public string ElasticSearchConnectionString { get; set; }

        public string GeoIPDatabasePath { get; set; }

        public string Version { get; private set; }

        public bool EnableIntercom { get { return !String.IsNullOrEmpty(IntercomAppId) && !String.IsNullOrEmpty(IntercomAppSecret); ; } }

        public string IntercomAppId { get; private set; }

        public string IntercomAppSecret { get; private set; }

        public bool EnableAccountCreation { get; private set; }

        public string GoogleAnalyticsId { get; private set; }

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

        public string StripePublishableApiKey { get; private set; }

        public string StorageFolder { get; private set; }

        public string AzureStorageConnectionString { get; set; }

        public bool EnableAzureStorage { get; private set; }

        public int BulkBatchSize { get; private set; }
        
        private static Settings Init() {
            var settings = new Settings();

            settings.EnableSSL = ConfigurationManager.AppSettings.GetBool("EnableSSL", false);

            string value = ConfigurationManager.AppSettings["BaseURL"];
            if (!String.IsNullOrEmpty(value)) {
                if (value.EndsWith("/"))
                    value = value.Substring(0, value.Length - 1);

                if (settings.EnableSSL && value.StartsWith("http:"))
                    value = value.ReplaceFirst("http:", "https:");
                else if (!settings.EnableSSL && value.StartsWith("https:"))
                    value = value.ReplaceFirst("https:", "http:");

                settings.BaseURL = value;
            }

            settings.InternalProjectId = ConfigurationManager.AppSettings["InternalProjectId"];
            settings.WebsiteMode = ConfigurationManager.AppSettings.GetEnum<WebsiteMode>("WebsiteMode", WebsiteMode.Dev);
            settings.TestEmailAddress = ConfigurationManager.AppSettings["TestEmailAddress"];
            settings.AllowedOutboundAddresses = ConfigurationManager.AppSettings.GetStringList("AllowedOutboundAddresses", "exceptionless.com").Select(v => v.ToLowerInvariant()).ToList();
            settings.GeoIPDatabasePath = ConfigurationManager.AppSettings["GeoIPDatabasePath"]; 
            settings.RunJobsInProcess = ConfigurationManager.AppSettings.GetBool("RunJobsInProcess", true);
            settings.LogJobLocks = ConfigurationManager.AppSettings.GetBool("LogJobLocks", false);
            settings.LogJobEvents = ConfigurationManager.AppSettings.GetBool("LogJobEvents", false);
            settings.LogJobCompleted = ConfigurationManager.AppSettings.GetBool("LogJobCompleted", false);
            settings.LogStackingInfo = ConfigurationManager.AppSettings.GetBool("LogStackingInfo", false);
            settings.AppendMachineNameToDatabase = ConfigurationManager.AppSettings.GetBool("AppendMachineNameToDatabase", false);
            settings.SaveIncomingErrorsToDisk = ConfigurationManager.AppSettings.GetBool("SaveIncomingErrorsToDisk", false);
            settings.IncomingErrorPath = ConfigurationManager.AppSettings["IncomingErrorPath"];
            settings.EnableLogErrorReporting = ConfigurationManager.AppSettings.GetBool("EnableLogErrorReporting", false);
            settings.EnableSignalR = ConfigurationManager.AppSettings.GetBool("EnableSignalR", true);
            settings.BotThrottleLimit = ConfigurationManager.AppSettings.GetInt("BotThrottleLimit", 25);
            settings.ApiThrottleLimit = ConfigurationManager.AppSettings.GetInt("ApiThrottleLimit", Int32.MaxValue);
            settings.MaximumEventPostSize = ConfigurationManager.AppSettings.GetInt("MaximumEventPostSize", Int32.MaxValue);
            settings.EnableDailySummary = ConfigurationManager.AppSettings.GetBool("EnableDailySummary", false);
            settings.ShouldAutoUpgradeDatabase = ConfigurationManager.AppSettings.GetBool("ShouldAutoUpgradeDatabase", true);
            settings.AppStatsServerName = ConfigurationManager.AppSettings["AppStatsServerName"] ?? "127.0.0.1";
            settings.AppStatsServerPort = ConfigurationManager.AppSettings.GetInt("AppStatsServerPort", 12000);
            settings.EnableAppStats = ConfigurationManager.AppSettings.GetBool("EnableAppStats", false);
            settings.IntercomAppId = ConfigurationManager.AppSettings["IntercomAppId"];
            settings.IntercomAppSecret = ConfigurationManager.AppSettings["IntercomAppSecret"];
            settings.EnableAccountCreation = ConfigurationManager.AppSettings.GetBool("EnableAccountCreation", true);
            settings.GoogleAppId = ConfigurationManager.AppSettings["GoogleAppId"];
            settings.GoogleAppSecret = ConfigurationManager.AppSettings["GoogleAppSecret"];
            settings.MicrosoftAppId = ConfigurationManager.AppSettings["MicrosoftAppId"];
            settings.MicrosoftAppSecret = ConfigurationManager.AppSettings["MicrosoftAppSecret"];
            settings.FacebookAppId = ConfigurationManager.AppSettings["FacebookAppId"];
            settings.FacebookAppSecret = ConfigurationManager.AppSettings["FacebookAppSecret"];
            settings.GitHubAppId = ConfigurationManager.AppSettings["GitHubAppId"];
            settings.GitHubAppSecret = ConfigurationManager.AppSettings["GitHubAppSecret"];
            settings.StripeApiKey = ConfigurationManager.AppSettings["StripeApiKey"];
            settings.StripePublishableApiKey = ConfigurationManager.AppSettings["StripePublishableApiKey"];
            settings.StorageFolder = ConfigurationManager.AppSettings["StorageFolder"];
            settings.BulkBatchSize = ConfigurationManager.AppSettings.GetInt("BulkBatchSize", 1000);

            var redisConnectionString = ConfigurationManager.ConnectionStrings["RedisConnectionString"];
            if (redisConnectionString != null)
                settings.RedisConnectionString = redisConnectionString.ConnectionString;
            settings.EnableRedis = ConfigurationManager.AppSettings.GetBool("EnableRedis", !String.IsNullOrEmpty(settings.RedisConnectionString));

            var azureConnectionInfo = ConfigurationManager.ConnectionStrings["AzureStorageConnectionString"];
            if (azureConnectionInfo != null)
                settings.AzureStorageConnectionString = azureConnectionInfo.ConnectionString;
            settings.EnableAzureStorage = ConfigurationManager.AppSettings.GetBool("EnableAzureStorage", !String.IsNullOrEmpty(settings.AzureStorageConnectionString));

            var mongoConnectionString = ConfigurationManager.ConnectionStrings["MongoConnectionString"];
            if (mongoConnectionString != null)
                settings.MongoConnectionString = mongoConnectionString.ConnectionString;

            var elasticSearchConnectionString = ConfigurationManager.ConnectionStrings["ElasticSearchConnectionString"];
            if (elasticSearchConnectionString != null)
                settings.ElasticSearchConnectionString = elasticSearchConnectionString.ConnectionString;

            settings.Version = ThisAssembly.AssemblyInformationalVersion;

            return settings;
        }

        #region Singleton

        protected Settings() {}

        private static readonly Lazy<Settings> _instance = new Lazy<Settings>(Init);

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        [DebuggerNonUserCode]
        public static Settings Current { get { return _instance.Value; } }

        #endregion
    }

    public enum WebsiteMode {
        Production,
        QA,
        Dev
    }
}