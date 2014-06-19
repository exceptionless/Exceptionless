using System;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using Exceptionless.Dependency;
using Exceptionless.Enrichments.Default;
using Exceptionless.Extras;
using Exceptionless.Extras.Storage;
using Exceptionless.Logging;
using Exceptionless.Storage;
using Exceptionless.Utility;

namespace Exceptionless {
    public static class ExceptionlessConfigurationExtensions {
        /// <summary>
        /// Reads the Exceptionless configuration from the app.config or web.config file.
        /// </summary>
        /// <param name="configuration">The configuration object you want to apply the attribute settings to.</param>
        public static void UseErrorEnrichment(this ExceptionlessConfiguration configuration) {
            configuration.RemoveEnrichment<SimpleErrorEnrichment>();
            configuration.AddEnrichment<Enrichments.ErrorEnrichment>();
        }

        public static void UseIsolatedStorage(this ExceptionlessConfiguration configuration) {
            configuration.Resolver.Register<IFileStorage, IsolatedStorageFileStorage>();
        }

        public static void UseFolderStorage(this ExceptionlessConfiguration configuration, string folder) {
            configuration.Resolver.Register<IFileStorage>(new FolderFileStorage(folder));
        }

        public static void UseTraceLogger(this ExceptionlessConfiguration configuration) {
            configuration.Resolver.Register<IExceptionlessLog, TraceExceptionlessLog>();
        }

        public static void UseFileLogger(this ExceptionlessConfiguration configuration, string logPath) {
            configuration.Resolver.Register<IExceptionlessLog>(new SafeExceptionlessLog(new FileExceptionlessLog(logPath)));
        }

        public static void UseIsolatedStorageLogger(this ExceptionlessConfiguration configuration) {
            configuration.Resolver.Register<IExceptionlessLog>(new SafeExceptionlessLog(new IsolatedStorageFileExceptionlessLog("exceptionless.log")));
        }

        public static void UseTraceLogEntriesEnrichment(this ExceptionlessConfiguration configuration, int maxEntriesToInclude = TraceLogEnrichment.DefaultMaxEntriesToInclude) {
            if (!Trace.Listeners.OfType<ExceptionlessTraceListener>().Any())
                Trace.Listeners.Add(new ExceptionlessTraceListener());

            configuration.Settings.Add(TraceLogEnrichment.MaxEntriesToIncludeKey, maxEntriesToInclude.ToString());
            configuration.AddEnrichment<TraceLogEnrichment>();
        }

        /// <summary>
        /// Reads the Exceptionless configuration from the app.config or web.config file.
        /// </summary>
        /// <param name="configuration">The configuration object you want to apply the attribute settings to.</param>
        public static void ReadFromConfig(this ExceptionlessConfiguration configuration) {
            ExceptionlessSection section = null;

            try {
                section = ConfigurationManager.GetSection("exceptionless") as ExceptionlessSection;
            } catch (Exception ex) {
                configuration.Resolver.GetLog().Error(typeof(ExceptionlessConfigurationExtensions), ex, String.Concat("An error occurred while retrieving the configuration section. Exception: ", ex.Message));
            }

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

            if (!String.IsNullOrEmpty(section.StoragePath))
                configuration.UseFolderStorage(section.StoragePath);

            if (!section.EnableLogging.HasValue || section.EnableLogging.Value) {
                if (!String.IsNullOrEmpty(section.LogPath))
                    configuration.UseFileLogger(section.LogPath);
                else
                    configuration.UseIsolatedStorageLogger();
            }

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