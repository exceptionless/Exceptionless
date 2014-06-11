using System;
using System.Configuration;
using System.Linq;
using Exceptionless.Configuration;
using Exceptionless.Extensions;

namespace Exceptionless {
    public static class ExceptionlessConfigurationExtensions {
        /// <summary>
        /// Reads the Exceptionless configuration from the app.config or web.config file.
        /// </summary>
        /// <param name="configuration">The configuration object you want to apply the attribute settings to.</param>
        public static void UseErrorEnrichment(this ExceptionlessConfiguration configuration) {
            configuration.RemoveEnrichment<Enrichments.Default.SimpleErrorEnrichment>();
            configuration.AddEnrichment<Enrichments.ErrorEnrichment>();
        }

        /// <summary>
        /// Reads the Exceptionless configuration from the app.config or web.config file.
        /// </summary>
        /// <param name="configuration">The configuration object you want to apply the attribute settings to.</param>
        public static void ReadFromConfig(this ExceptionlessConfiguration configuration) {
            var section = ConfigurationManager.GetSection("exceptionless") as ExceptionlessSection;
            if (section == null)
                return;

            configuration.Enabled = section.Enabled;

            // Only update if it is not null
            if (!String.IsNullOrEmpty(section.ApiKey))
                configuration.ApiKey = section.ApiKey;

            // If an appsetting is present for ApiKey, then it will override the other api keys
            string apiKeyOverride = ConfigurationManager.AppSettings["Exceptionless:ApiKey"] ?? String.Empty;
            if (!String.IsNullOrEmpty(apiKeyOverride))
                configuration.ApiKey = apiKeyOverride;

            if (!String.IsNullOrEmpty(section.ServerUrl))
                configuration.ServerUrl = section.ServerUrl;

            if (section.EnableSSL.HasValue)
                configuration.EnableSSL = section.EnableSSL.Value;

            //if (!String.IsNullOrEmpty(section.QueuePath))
            //    configuration.QueuePath = section.QueuePath;

            //if (section.EnableLogging.HasValue)
            //    configuration.EnableLogging = section.EnableLogging.Value;

            //if (!String.IsNullOrEmpty(section.LogPath))
            //    configuration.LogPath = section.LogPath;

            //// if a log path is specified and enable logging setting isn't specified, then enable logging.
            //if (!String.IsNullOrEmpty(section.LogPath) && !section.EnableLogging.HasValue)
            //    configuration.EnableLogging = true;

            foreach (var tag in section.Tags.SplitAndTrim(',').Where(tag => !String.IsNullOrEmpty(tag)))
                configuration.DefaultTags.Add(tag);

            if (section.ExtendedData != null) {
                foreach (NameValueConfigurationElement setting in section.ExtendedData) {
                    if (!String.IsNullOrEmpty(setting.Name))
                        configuration.DefaultData[setting.Name] = setting.Value;
                }
            }

            if (section.Settings != null) {
                foreach (NameValueConfigurationElement setting in section.Settings) {
                    if (!String.IsNullOrEmpty(setting.Name))
                        configuration.Settings[setting.Name] = setting.Value;
                }
            }
        }
    }
}