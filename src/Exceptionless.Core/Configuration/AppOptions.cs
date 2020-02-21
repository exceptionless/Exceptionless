using System;
using System.Collections.Generic;
using System.Diagnostics;
using Exceptionless.Core.Configuration;
using Exceptionless.Core.Extensions;
using Microsoft.Extensions.Configuration;
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
        public string AppScope { get; internal set; }

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

        public string MaxMindGeoIpKey { get; internal set; }

        public int BulkBatchSize { get; internal set; }

        public CacheOptions CacheOptions { get; internal set; }
        public MessageBusOptions MessageBusOptions { get; internal set; }
        public MetricOptions MetricOptions { get; internal set; }
        public QueueOptions QueueOptions { get; internal set; }
        public StorageOptions StorageOptions { get; internal set; }
        public EmailOptions EmailOptions { get; internal set; }
        public ElasticsearchOptions ElasticsearchOptions { get; internal set; }
        public IntercomOptions IntercomOptions { get; internal set; }
        public SlackOptions SlackOptions { get; internal set; }
        public StripeOptions StripeOptions { get; internal set; }
        public AuthOptions AuthOptions { get; internal set; }

        public static AppOptions ReadFromConfiguration(IConfiguration config) {
            var options = new AppOptions();
            options.BaseURL = config.GetValue<string>(nameof(options.BaseURL))?.TrimEnd('/');
            options.InternalProjectId = config.GetValue(nameof(options.InternalProjectId), "54b56e480ef9605a88a13153");
            options.ExceptionlessApiKey = config.GetValue<string>(nameof(options.ExceptionlessApiKey));
            options.ExceptionlessServerUrl = config.GetValue<string>(nameof(options.ExceptionlessServerUrl));

            options.AppMode = config.GetValue(nameof(options.AppMode), AppMode.Production);
            options.AppScope = options.AppMode.ToScope();
            options.RunJobsInProcess = config.GetValue(nameof(options.RunJobsInProcess), options.AppMode == AppMode.Development);
            options.JobsIterationLimit = config.GetValue(nameof(options.JobsIterationLimit), -1);
            options.BotThrottleLimit = config.GetValue(nameof(options.BotThrottleLimit), 25).NormalizeValue();

            options.ApiThrottleLimit = config.GetValue(nameof(options.ApiThrottleLimit), options.AppMode == AppMode.Development ? Int32.MaxValue : 3500).NormalizeValue();
            options.EnableArchive = config.GetValue(nameof(options.EnableArchive), true);
            options.EventSubmissionDisabled = config.GetValue(nameof(options.EventSubmissionDisabled), false);
            options.DisabledPipelineActions = config.GetValueList(nameof(options.DisabledPipelineActions));
            options.DisabledPlugins = config.GetValueList(nameof(options.DisabledPlugins));
            options.MaximumEventPostSize = config.GetValue(nameof(options.MaximumEventPostSize), 200000).NormalizeValue();
            options.MaximumRetentionDays = config.GetValue(nameof(options.MaximumRetentionDays), 180).NormalizeValue();
            options.ApplicationInsightsKey = config.GetValue<string>(nameof(options.ApplicationInsightsKey));

            options.GoogleGeocodingApiKey = config.GetValue<string>(nameof(options.GoogleGeocodingApiKey));
            options.MaxMindGeoIpKey = config.GetValue<string>(nameof(options.MaxMindGeoIpKey));

            options.BulkBatchSize = config.GetValue(nameof(options.BulkBatchSize), 1000);

            options.EnableRepositoryNotifications = config.GetValue(nameof(options.EnableRepositoryNotifications), true);
            options.EnableWebSockets = config.GetValue(nameof(options.EnableWebSockets), true);

            try {
                var versionInfo = FileVersionInfo.GetVersionInfo(typeof(AppOptions).Assembly.Location);
                options.Version = versionInfo.FileVersion;
                options.InformationalVersion = versionInfo.ProductVersion;
            }
            catch { }

            options.CacheOptions = CacheOptions.ReadFromConfiguration(config, options);
            options.MessageBusOptions = MessageBusOptions.ReadFromConfiguration(config, options);
            options.MetricOptions = MetricOptions.ReadFromConfiguration(config);
            options.QueueOptions = QueueOptions.ReadFromConfiguration(config, options);
            options.StorageOptions = StorageOptions.ReadFromConfiguration(config, options);
            options.EmailOptions = EmailOptions.ReadFromConfiguration(config, options);
            options.ElasticsearchOptions = ElasticsearchOptions.ReadFromConfiguration(config, options);
            options.IntercomOptions = IntercomOptions.ReadFromConfiguration(config);
            options.SlackOptions = SlackOptions.ReadFromConfiguration(config);
            options.StripeOptions = StripeOptions.ReadFromConfiguration(config);
            options.AuthOptions = AuthOptions.ReadFromConfiguration(config);

            return options;
        }
    }

    public enum AppMode {
        Development,
        Staging,
        Production
    }
}