using System;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.Core.Utility;

namespace Exceptionless.Insulation.Metrics {
    public static class MetricsConnectionString {
        public static IMetricsConnectionString Parse(string connectionString) {
            if (string.IsNullOrEmpty(connectionString)) return null;
            var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var option in connectionString
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(kvp => kvp.Contains('='))
                .Select(kvp => kvp.Split(new[] { '=' }, 2))) {
                string optionKey = option[0].Trim();
                string optionValue = option[1].Trim();
                if (String.IsNullOrEmpty(optionValue)) {
                    options[String.Empty] = optionKey;
                }
                else if(!String.IsNullOrEmpty(optionKey)) {
                    options[optionKey] = optionValue;
                }
            }

            if (options.TryGetValue("provider", out string provider) || options.TryGetValue("reporter", out provider)) {
                if (String.Equals(provider, "statsd", StringComparison.OrdinalIgnoreCase)) {
                    return new StatsDMetricsConnectionString(options);
                }

                if (String.Equals(provider, "http", StringComparison.OrdinalIgnoreCase)) {
                    return new HttpMetricsConnectionString(options);
                }

                if (String.Equals(provider, "influxdb", StringComparison.OrdinalIgnoreCase)) {
                    return new InfuxDBMetricsConnectionString(options);
                }

                if (String.Equals(provider, "prometheus", StringComparison.OrdinalIgnoreCase)) {
                    return new PrometheusMetricsConnectionString();
                }

                if (String.Equals(provider, "graphite", StringComparison.OrdinalIgnoreCase)) {
                    return new GraphiteMetricsConnectionString(options);
                }

                throw new InvalidOperationException($"The metrics provider {provider} cannot be recoganized.");
            }

            throw new InvalidOperationException("The metrics provider is required in the connection string.");
        }
    }
}
