namespace Exceptionless.Core.Utility {
    public interface IConnectionString {
        string ConnectionString { get; }
    }

    public interface IElasticsearchConnectionString : IConnectionString {
        bool EnableMapperSizePlugin { get; }
        int FieldsLimit { get; }
        int NumberOfReplicas { get; }
        int NumberOfShards { get; }
        string ServerUrl { get; }
    }

    public class DefaultConnectionString : IConnectionString {
        public DefaultConnectionString(string connectionString) {
            ConnectionString = connectionString;
        }

        public string ConnectionString { get; }
    }
}
