using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Exceptionless.Configuration;
using Exceptionless.Dependency;
using Exceptionless.Extensions;
using Exceptionless.Logging;
using Exceptionless.Storage;

namespace Exceptionless {
    public static class ExceptionlessConfigurationExtensions {
        public static void AddExclusions(this ExceptionlessConfiguration config, params string[] exclusions) {
            config.DataExclusions.AddRange(exclusions);
        }

        public static string GetQueueName(this ExceptionlessConfiguration config) {
            // TODO: Ensure the api key has been set before this is called.
            return config.ApiKey.Substring(0, 8);
        }

        public static void UseDebugLogger(this ExceptionlessConfiguration configuration) {
            configuration.Resolver.Register<IExceptionlessLog, DebugExceptionlessLog>();
        }

        public static void UseLogger(this ExceptionlessConfiguration configuration, IExceptionlessLog logger) {
            configuration.Resolver.Register<IExceptionlessLog>(new SafeExceptionlessLog(logger));
        }

        public static void ApplySavedServerSettings(this ExceptionlessConfiguration config) {
            SettingsManager.ApplySavedServerSettings(config);
        }

        public static void UseInMemoryStorage(this ExceptionlessConfiguration config) {
            config.Resolver.Register<IFileStorage, InMemoryFileStorage>();
        }

        /// <summary>
        /// Reads the <see cref="ExceptionlessAttribute" /> and <see cref="ExceptionlessSettingAttribute" /> 
        /// from the passed in assembly.
        /// </summary>
        /// <param name="config">The configuration object you want to apply the attribute settings to.</param>
        /// <param name="assemblies">The assembly that contains the Exceptionless configuration attributes.</param>
        public static void ReadFromAttributes(this ExceptionlessConfiguration config, params Assembly[] assemblies) {
            if (config == null)
                throw new ArgumentNullException("config");

            config.ReadFromAttributes(assemblies.ToList());
        }

        /// <summary>
        /// Reads the <see cref="ExceptionlessAttribute" /> and <see cref="ExceptionlessSettingAttribute" /> 
        /// from the passed in assemblies.
        /// </summary>
        /// <param name="config">The configuration object you want to apply the attribute settings to.</param>
        /// <param name="assemblies">A list of assemblies that should be checked for the Exceptionless configuration attributes.</param>
        public static void ReadFromAttributes(this ExceptionlessConfiguration config, ICollection<Assembly> assemblies = null) {
            if (config == null)
                throw new ArgumentNullException("config");

            if (assemblies == null)
                assemblies = new List<Assembly> { Assembly.GetCallingAssembly() };

            assemblies = assemblies.Where(a => a != null).Distinct().ToList();

            foreach (var assembly in assemblies) {
                object[] attributes = assembly.GetCustomAttributes(typeof(ExceptionlessAttribute), false);
                if (attributes.Length <= 0 || !(attributes[0] is ExceptionlessAttribute))
                    continue;

                var attr = attributes[0] as ExceptionlessAttribute;

                config.Enabled = attr.Enabled;
                
                if (attr.ApiKey != null)
                    config.ApiKey = attr.ApiKey;
                if (attr.ServerUrl != null)
                    config.ServerUrl = attr.ServerUrl;
                
                config.EnableSSL = attr.EnableSSL;
                break;
            }

            foreach (var assembly in assemblies) {
                object[] attributes = assembly.GetCustomAttributes(typeof(ExceptionlessSettingAttribute), false);
                foreach (ExceptionlessSettingAttribute attribute in attributes.OfType<ExceptionlessSettingAttribute>()) {
                    if (!String.IsNullOrEmpty(attribute.Name))
                        config.Settings[attribute.Name] = attribute.Value;
                }
            }
        }
    }
}

namespace Exceptionless.Extensions {
    public static class ExceptionlessConfigurationExtensions {
        public static Uri GetServiceEndPoint(this ExceptionlessConfiguration config) {
            var builder = new UriBuilder(config.ServerUrl) {
                Path = "/api/v2/"
            };

            // EnableSSL
            if (config.EnableSSL && builder.Port == 80 && !builder.Host.Contains("local")) {
                builder.Port = 443;
                builder.Scheme = "https";
            }

            return builder.Uri;
        }
    }
}