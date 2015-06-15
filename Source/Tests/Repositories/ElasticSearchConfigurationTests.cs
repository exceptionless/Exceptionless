using System;
using Exceptionless.Api.Tests.Utility;
using Exceptionless.Core.Repositories.Configuration;
using Nest;
using Xunit;

namespace Exceptionless.Api.Tests.Repositories {
    public class ElasticSearchConfigurationTests {
        public readonly IElasticClient _client = IoC.GetInstance<IElasticClient>();
        public readonly ElasticSearchConfiguration _configuration = IoC.GetInstance<ElasticSearchConfiguration>();
        public readonly StackIndex _stackIndex = new StackIndex();

        [Fact]
        public void CanCreateStackAlias() {
            _configuration.ConfigureIndexes(_client, true);
            var index = _client.GetIndex(descriptor => descriptor.Index(_stackIndex.VersionedName));
            Assert.True(index.IsValid);
            Assert.True(index.Indices.Count > 0);

            var alias = _client.GetAlias(descriptor => descriptor.Alias(_stackIndex.Name));
            Assert.True(alias.IsValid);
            Assert.True(alias.Indices.Count > 0);
        }
    }
}
