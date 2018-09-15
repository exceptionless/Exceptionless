using System;
using Exceptionless.Core.Utility;

namespace Exceptionless.Insulation.Configuration.ConnectionStrings {
    public class AliyunConnectionString : DefaultConnectionString {
        public const string ProviderName = "aliyun";

        public AliyunConnectionString(string connectionString) : base(connectionString) { }
    }
}