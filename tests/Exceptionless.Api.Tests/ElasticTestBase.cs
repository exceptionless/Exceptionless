using System;
using Exceptionless.Core.Repositories.Configuration;
using Xunit.Abstractions;

namespace Exceptionless.Api.Tests {
    public class ElasticTestBase : TestBase {
        protected readonly ExceptionlessElasticConfiguration _configuration;

        public ElasticTestBase(ITestOutputHelper output) : base(output) {
            _configuration = GetService<ExceptionlessElasticConfiguration>();
            _configuration.DeleteIndexesAsync().GetAwaiter().GetResult();
            _configuration.ConfigureIndexesAsync(beginReindexingOutdated: false).GetAwaiter().GetResult();
        }

        public override void Dispose() {
            _configuration.Dispose();
            base.Dispose();
        }
    }
}