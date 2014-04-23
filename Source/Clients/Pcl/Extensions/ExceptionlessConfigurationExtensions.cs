using System;
using System.Linq;
using System.Reflection;
using Exceptionless.Configuration;
using Exceptionless.Dependency;
using Exceptionless.Logging;

namespace Exceptionless {
    public static class ExceptionlessConfigurationExtensions {
        internal static Uri GetServiceEndPoint(this ExceptionlessConfiguration config) {
            var builder = new UriBuilder(config.ServerUrl) { Path = "/api/v1/" };

            // EnableSSL
            if (config.Settings.GetBoolean("EnableSSL", false) && builder.Port == 80 && !builder.Host.Contains("local")) {
                builder.Port = 443;
                builder.Scheme = "https";
            }

            return builder.Uri;
        }

        public static void UseDebugLogger(this ExceptionlessConfiguration configuration) {
            configuration.Resolver.Register<IExceptionlessLog, DebugExceptionlessLog>();
        }

        /// <summary>
        /// Reads the <see cref="ExceptionlessAttribute" /> and <see cref="ExceptionlessSettingAttribute" /> 
        /// from the calling assembly.
        /// </summary>
        /// <param name="configuration">The configuration object you want to apply the attribute settings to.</param>
        public static void ReadFromAttributes(this ExceptionlessConfiguration configuration) {
            configuration.ReadFromAttributes(Assembly.GetCallingAssembly());
        }

        /// <summary>
        /// Reads the <see cref="ExceptionlessAttribute" /> and <see cref="ExceptionlessSettingAttribute" /> 
        /// from the entry assembly.
        /// </summary>
        /// <param name="configuration">The configuration object you want to apply the attribute settings to.</param>
        /// <param name="entryAssembly">The entry assembly that contains the Exceptionless configuration attributes.</param>
        public static void ReadFromAttributes(this ExceptionlessConfiguration configuration, Assembly entryAssembly) {
            if (configuration == null)
                throw new ArgumentNullException("configuration");

            if(entryAssembly == null)
                throw new ArgumentNullException("entryAssembly");

            object[] attributes = entryAssembly.GetCustomAttributes(typeof(ExceptionlessAttribute), false);
            if (attributes.Length > 0 && attributes[0] is ExceptionlessAttribute) {
                var attr = attributes[0] as ExceptionlessAttribute;

                if (attr.ApiKey != null)
                    configuration.ApiKey = attr.ApiKey;
                if (attr.QueuePath != null)
                    configuration.QueuePath = attr.QueuePath;
                if (attr.ServerUrl != null)
                    configuration.ServerUrl = attr.ServerUrl;
                
                configuration.EnableSSL = attr.EnableSSL;
                configuration.EnableLogging = attr.EnableLogging;
                
                if (attr.LogPath != null)
                    configuration.LogPath = attr.LogPath;
               
                configuration.Enabled = attr.Enabled;
            }

            attributes = entryAssembly.GetCustomAttributes(typeof(ExceptionlessSettingAttribute), false);
            foreach (ExceptionlessSettingAttribute attribute in attributes.OfType<ExceptionlessSettingAttribute>()) {
                if (!String.IsNullOrEmpty(attribute.Name))
                    configuration[attribute.Name] = attribute.Value;
            }
        }
    }
}