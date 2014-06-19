using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Exceptionless.Configuration;
using Exceptionless.Dependency;
using Exceptionless.Extensions;
using Exceptionless.Logging;

namespace Exceptionless {
    public static class ExceptionlessConfigurationExtensions {
        internal static Uri GetServiceEndPoint(this ExceptionlessConfiguration config) {
            var builder = new UriBuilder(config.ServerUrl) { Path = "/api/v2/" };

            // EnableSSL
            if (config.EnableSSL && builder.Port == 80 && !builder.Host.Contains("local")) {
                builder.Port = 443;
                builder.Scheme = "https";
            }

            return builder.Uri;
        }

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

        /// <summary>
        /// Reads the <see cref="ExceptionlessAttribute" /> and <see cref="ExceptionlessSettingAttribute" /> 
        /// from the passed in assembly.
        /// </summary>
        /// <param name="configuration">The configuration object you want to apply the attribute settings to.</param>
        /// <param name="assembly">The assembly that contains the Exceptionless configuration attributes.</param>
        public static void ReadFromAttributes(this ExceptionlessConfiguration configuration, Assembly assembly = null) {
            if (configuration == null)
                throw new ArgumentNullException("configuration");

            configuration.ReadFromAttributes(assembly != null ? new List<Assembly> { assembly } : null);
        }

        /// <summary>
        /// Reads the <see cref="ExceptionlessAttribute" /> and <see cref="ExceptionlessSettingAttribute" /> 
        /// from the passed in assemblies.
        /// </summary>
        /// <param name="configuration">The configuration object you want to apply the attribute settings to.</param>
        /// <param name="assemblies">A list of assemblies that should be checked for the Exceptionless configuration attributes.</param>
        public static void ReadFromAttributes(this ExceptionlessConfiguration configuration, ICollection<Assembly> assemblies = null) {
            if (configuration == null)
                throw new ArgumentNullException("configuration");

            if (assemblies == null)
                assemblies = new List<Assembly> { Assembly.GetCallingAssembly() };

            foreach (var assembly in assemblies) {
                object[] attributes = assembly.GetCustomAttributes(typeof(ExceptionlessAttribute), false);
                if (attributes.Length <= 0 || !(attributes[0] is ExceptionlessAttribute))
                    continue;

                var attr = attributes[0] as ExceptionlessAttribute;

                configuration.Enabled = attr.Enabled;

                if (attr.ApiKey != null)
                    configuration.ApiKey = attr.ApiKey;
                if (attr.ServerUrl != null)
                    configuration.ServerUrl = attr.ServerUrl;

                configuration.EnableSSL = attr.EnableSSL;
                break;
            }

            foreach (var assembly in assemblies) {
                object[] attributes = assembly.GetCustomAttributes(typeof(ExceptionlessSettingAttribute), false);
                foreach (ExceptionlessSettingAttribute attribute in attributes.OfType<ExceptionlessSettingAttribute>()) {
                    if (!String.IsNullOrEmpty(attribute.Name))
                        configuration.Settings[attribute.Name] = attribute.Value;
                }
            }
        }
    }
}