using System.Collections.Generic;

namespace Exceptionless.Insulation.Metrics {
    public class InfuxDBMetricsConnectionString : HttpMetricsConnectionString {
        public InfuxDBMetricsConnectionString(IDictionary<string, string> settings) : base(settings) {
            if (settings.TryGetValue("database", out string database) || settings.TryGetValue("catalog", out database)) {
                Database = database;
            }
        }

        public string Database { get; } = "exceptionless";

        protected override int? DefaultPort => 8086;
    }
}
