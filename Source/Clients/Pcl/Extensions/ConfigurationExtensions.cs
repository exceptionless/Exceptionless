using System;

namespace Exceptionless.Extensions {
    public static class ConfigurationExtensions {
        public static Uri GetServiceEndPoint(this ExceptionlessConfiguration config) {
            var builder = new UriBuilder(config.ServerUrl) { Path = "/api/v1/" };

            // EnableSSL
            if (config.Settings.GetBoolean("EnableSSL", false) && builder.Port == 80 && !builder.Host.Contains("local")) {
                builder.Port = 443;
                builder.Scheme = "https";
            }

            return builder.Uri;
        }
    }
}