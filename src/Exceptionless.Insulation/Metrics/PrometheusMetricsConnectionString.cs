using System;
using Exceptionless.Core.Utility;

namespace Exceptionless.Insulation.Metrics {
    public class PrometheusMetricsConnectionString : DefaultMetricsConnectionString {
        public PrometheusMetricsConnectionString(string connectionString) : base(connectionString) { }
    }
}
