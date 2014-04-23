using System;
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
    }
}