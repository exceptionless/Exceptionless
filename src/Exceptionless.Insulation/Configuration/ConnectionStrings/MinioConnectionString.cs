using Exceptionless.Core.Utility;

namespace Exceptionless.Insulation.Configuration.ConnectionStrings {
    public class MinioConnectionString : DefaultConnectionString {
        public const string ProviderName = "minio";

        public MinioConnectionString(string connectionString) : base(connectionString) { }
    }
}