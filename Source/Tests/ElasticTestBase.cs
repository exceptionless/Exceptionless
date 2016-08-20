using System;
using Exceptionless.Core.Repositories.Configuration;
using Nest;
using Xunit.Abstractions;

namespace Exceptionless.Api.Tests {
    public class ElasticTestBase : TestBase {
        protected readonly ExceptionlessElasticConfiguration _configuration;
        protected readonly IElasticClient _client;

        public ElasticTestBase(ITestOutputHelper output) : base(output) {
            _configuration = GetService<ExceptionlessElasticConfiguration>();
            _configuration.DeleteIndexesAsync().GetAwaiter().GetResult();
            _configuration.ConfigureIndexesAsync().GetAwaiter().GetResult();

            _client = _configuration.Client;
        }
    }
}