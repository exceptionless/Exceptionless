using System;

namespace Exceptionless.Core.Utility {
    public interface IMetricsConnectionString {
        string ConnectionString { get; }
    }

    public class DefaultMetricsConnectionString : IMetricsConnectionString {
        public DefaultMetricsConnectionString(string connectionString) {
            ConnectionString = connectionString;
        }

        public string ConnectionString { get; }
    }
}
