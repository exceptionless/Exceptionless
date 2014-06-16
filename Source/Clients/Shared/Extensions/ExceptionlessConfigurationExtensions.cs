using System;
using System.Linq;
using System.Reflection;
using Exceptionless.Configuration;
using Exceptionless.Dependency;
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

        public static void UseDebugLogger(this ExceptionlessConfiguration configuration) {
            configuration.Resolver.Register<IExceptionlessLog, DebugExceptionlessLog>();
        }

        public static void UseLogger(this ExceptionlessConfiguration configuration, IExceptionlessLog logger) {
            configuration.Resolver.Register<IExceptionlessLog>(new SafeExceptionlessLog(logger));
        }

        /// <summary>
        /// Reads the <see cref="ExceptionlessAttribute" /> and <see cref="ExceptionlessSettingAttribute" /> 
        /// from the assembly.
        /// </summary>
        /// <param name="configuration">The configuration object you want to apply the attribute settings to.</param>
        /// <param name="assembly">The assembly that contains the Exceptionless configuration attributes.</param>
        public static void ReadFromAttributes(this ExceptionlessConfiguration configuration, Assembly assembly = null) {
            if (configuration == null)
                throw new ArgumentNullException("configuration");

            if (assembly == null)
                assembly = Assembly.GetCallingAssembly();

            object[] attributes = assembly.GetCustomAttributes(typeof(ExceptionlessAttribute), false);
            if (attributes.Length > 0 && attributes[0] is ExceptionlessAttribute) {
                var attr = attributes[0] as ExceptionlessAttribute;

                configuration.Enabled = attr.Enabled;

                if (attr.ApiKey != null)
                    configuration.ApiKey = attr.ApiKey;
                if (attr.ServerUrl != null)
                    configuration.ServerUrl = attr.ServerUrl;
                
                configuration.EnableSSL = attr.EnableSSL;
            }

            attributes = assembly.GetCustomAttributes(typeof(ExceptionlessSettingAttribute), false);
            foreach (ExceptionlessSettingAttribute attribute in attributes.OfType<ExceptionlessSettingAttribute>()) {
                if (!String.IsNullOrEmpty(attribute.Name))
                    configuration.Settings[attribute.Name] = attribute.Value;
            }
        }
    }
}