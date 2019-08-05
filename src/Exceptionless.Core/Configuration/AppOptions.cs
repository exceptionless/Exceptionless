using System;
using System.Collections.Generic;
using System.Diagnostics;
using Exceptionless.Core.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

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

        public bool EnableRepositoryNotifications { get; internal set; }

        public bool EnableWebSockets { get; internal set; }

        public string Version { get; internal set; }

        public string InformationalVersion { get; internal set; }

        public string GoogleGeocodingApiKey { get; internal set; }

        public int BulkBatchSize { get; internal set; }
        
        public static AppOptions ReadFromConfiguration(IConfiguration config) {
            var options = new AppOptions();
            var configureOptions = new ConfigureAppOptions(config);
            configureOptions.Configure(options);
            return options;
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
            options.RunJobsInProcess = _configuration.GetValue(nameof(options.RunJobsInProcess), options.AppMode == AppMode.Development);
            options.JobsIterationLimit = _configuration.GetValue(nameof(options.JobsIterationLimit), -1);
            options.BotThrottleLimit = _configuration.GetValue(nameof(options.BotThrottleLimit), 25).NormalizeValue();

            options.ApiThrottleLimit = _configuration.GetValue(nameof(options.ApiThrottleLimit), options.AppMode == AppMode.Development ? Int32.MaxValue : 3500).NormalizeValue();
            options.EnableArchive = _configuration.GetValue(nameof(options.EnableArchive), true);
            options.EventSubmissionDisabled = _configuration.GetValue(nameof(options.EventSubmissionDisabled), false);
            options.DisabledPipelineActions = _configuration.GetValueList(nameof(options.DisabledPipelineActions));
            options.DisabledPlugins = _configuration.GetValueList(nameof(options.DisabledPlugins));
            options.MaximumEventPostSize = _configuration.GetValue(nameof(options.MaximumEventPostSize), 200000).NormalizeValue();
            options.MaximumRetentionDays = _configuration.GetValue(nameof(options.MaximumRetentionDays), 180).NormalizeValue();
            options.ApplicationInsightsKey = _configuration.GetValue<string>(nameof(options.ApplicationInsightsKey));

            options.GoogleGeocodingApiKey = _configuration.GetValue<string>(nameof(options.GoogleGeocodingApiKey));

            options.BulkBatchSize = _configuration.GetValue(nameof(options.BulkBatchSize), 1000);
            
            options.EnableRepositoryNotifications = _configuration.GetValue(nameof(options.EnableRepositoryNotifications), true);
            options.EnableWebSockets = _configuration.GetValue(nameof(options.EnableWebSockets), true);

            try {
                var versionInfo = FileVersionInfo.GetVersionInfo(typeof(AppOptions).Assembly.Location);
                options.Version = versionInfo.FileVersion;
                options.InformationalVersion = versionInfo.ProductVersion;
            } catch { }
        }
    }

    public enum AppMode {
        Development,
        Staging,
        Production
    }
}