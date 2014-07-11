using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Exceptionless.Dependency;
using Exceptionless.Enrichments.Default;
using Exceptionless.Extras;
using Exceptionless.Extras.Storage;
using Exceptionless.Logging;
using Exceptionless.Storage;
using Exceptionless.Utility;

namespace Exceptionless {
    public static class ExceptionlessExtraConfigurationExtensions {
        /// <summary>
        /// Reads the Exceptionless configuration from the app.config or web.config file.
        /// </summary>
        /// <param name="config">The configuration object you want to apply the attribute settings to.</param>
        public static void UseErrorEnrichment(this ExceptionlessConfiguration config) {
            config.RemoveEnrichment<SimpleErrorEnrichment>();
            config.AddEnrichment<Enrichments.ErrorEnrichment>();
        }

        public static void UseIsolatedStorage(this ExceptionlessConfiguration config) {
            config.Resolver.Register<IFileStorage, IsolatedStorageFileStorage>();
        }

        public static void UseFolderStorage(this ExceptionlessConfiguration config, string folder) {
            config.Resolver.Register<IFileStorage>(new FolderFileStorage(folder));
        }

        public static void UseTraceLogger(this ExceptionlessConfiguration config) {
            config.Resolver.Register<IExceptionlessLog, TraceExceptionlessLog>();
        }

        public static void UseFileLogger(this ExceptionlessConfiguration config, string logPath) {
            config.Resolver.Register<IExceptionlessLog>(new SafeExceptionlessLog(new FileExceptionlessLog(logPath)));
        }

        public static void UseIsolatedStorageLogger(this ExceptionlessConfiguration config) {
            config.Resolver.Register<IExceptionlessLog>(new SafeExceptionlessLog(new IsolatedStorageFileExceptionlessLog("exceptionless.log")));
        }

        public static void UseTraceLogEntriesEnrichment(this ExceptionlessConfiguration config, int maxEntriesToInclude = TraceLogEnrichment.DefaultMaxEntriesToInclude) {
            if (!Trace.Listeners.OfType<ExceptionlessTraceListener>().Any())
                Trace.Listeners.Add(new ExceptionlessTraceListener());

            config.Settings.Add(TraceLogEnrichment.MaxEntriesToIncludeKey, maxEntriesToInclude.ToString());
            config.AddEnrichment<TraceLogEnrichment>();
        }

        public static void ReadAllConfig(this ExceptionlessConfiguration config, params Assembly[] configAttributesAssemblies) {
            config.UseIsolatedStorage();
            if (configAttributesAssemblies == null || configAttributesAssemblies.Length == 0)
                config.ReadFromAttributes(Assembly.GetEntryAssembly(), Assembly.GetCallingAssembly());
            else
                config.ReadFromAttributes(configAttributesAssemblies);
            config.ReadFromConfigSection();
            config.ApplySavedServerSettings();
        }

        /// <summary>
        /// Reads the Exceptionless configuration from the app.config or web.config file.
        /// </summary>
        /// <param name="config">The configuration object you want to apply the attribute settings to.</param>
        public static void ReadFromConfigSection(this ExceptionlessConfiguration config) {
            ExceptionlessSection section = null;

            try {
                section = ConfigurationManager.GetSection("exceptionless") as ExceptionlessSection;
            } catch (Exception ex) {
                config.Resolver.GetLog().Error(typeof(ExceptionlessExtraConfigurationExtensions), ex, String.Concat("An error occurred while retrieving the configuration section. Exception: ", ex.Message));
            }

            if (section == null)
                return;

            config.Enabled = section.Enabled;

            // Only update if it is not null
            if (!String.IsNullOrEmpty(section.ApiKey))
                config.ApiKey = section.ApiKey;

            // If an appsetting is present for ApiKey, then it will override the other api keys
            string apiKeyOverride = ConfigurationManager.AppSettings["Exceptionless:ApiKey"] ?? String.Empty;
            if (!String.IsNullOrEmpty(apiKeyOverride))
                config.ApiKey = apiKeyOverride;

            if (!String.IsNullOrEmpty(section.ServerUrl))
                config.ServerUrl = section.ServerUrl;

            if (section.EnableSSL.HasValue)
                config.EnableSSL = section.EnableSSL.Value;

            if (!String.IsNullOrEmpty(section.StoragePath))
                config.UseFolderStorage(section.StoragePath);

            if (section.EnableLogging.HasValue && section.EnableLogging.Value) {
                if (!String.IsNullOrEmpty(section.LogPath))
                    config.UseFileLogger(section.LogPath);
                else if (!String.IsNullOrEmpty(section.StoragePath))
                    config.UseFileLogger(Path.Combine(section.StoragePath, "exceptionless.log"));
                else
                    config.UseIsolatedStorageLogger();
            }

            foreach (var tag in section.Tags.SplitAndTrim(',').Where(tag => !String.IsNullOrEmpty(tag)))
                config.DefaultTags.Add(tag);

            if (section.ExtendedData != null) {
                foreach (NameValueConfigurationElement setting in section.ExtendedData) {
                    if (!String.IsNullOrEmpty(setting.Name))
                        config.DefaultData[setting.Name] = setting.Value;
                }
            }

            if (section.Settings != null) {
                foreach (NameValueConfigurationElement setting in section.Settings) {
                    if (!String.IsNullOrEmpty(setting.Name))
                        config.Settings[setting.Name] = setting.Value;
                }
            }
        }
    }
}