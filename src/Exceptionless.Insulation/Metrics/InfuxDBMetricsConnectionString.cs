using System.Collections.Generic;

namespace Exceptionless.Insulation.Metrics {
    public class InfuxDBMetricsConnectionString : HttpMetricsConnectionString {
        public InfuxDBMetricsConnectionString(string connectionString, IDictionary<string, string> settings) : base(connectionString, settings) {
            if (settings.TryGetValue("database", out string database) || settings.TryGetValue("catalog", out database)) {
                Database = database;
            }
        }

        public string Database { get; } = "exceptionless";

        protected override int? DefaultPort => 8086;
    }
}
