using System;
using Exceptionless.Core.Utility;

namespace Exceptionless.Insulation.Configuration.ConnectionStrings {
    public class AzureStorageConnectionString : DefaultConnectionString {
        public const string ProviderName = "azurestorage";

        public AzureStorageConnectionString(string connectionString) : base(connectionString) { }
    }
}
