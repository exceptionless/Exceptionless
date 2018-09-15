using System;
using Exceptionless.Core.Utility;

namespace Exceptionless.Insulation.Configuration.ConnectionStrings {
    public class PrometheusConnectionString : DefaultConnectionString {
        public const string ProviderName = "prometheus";

        public PrometheusConnectionString(string connectionString) : base(connectionString) { }
    }
}
