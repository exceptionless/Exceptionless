using System;
using Exceptionless.Core.Utility;

namespace Exceptionless.Insulation.Configuration.ConnectionStrings {
    public class RedisConnectionString : DefaultConnectionString {
        public const string ProviderName = "redis";

        public RedisConnectionString(string connectionString) : base(connectionString) { }
    }
}