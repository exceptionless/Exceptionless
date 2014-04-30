using System;
using Exceptionless.Core.Caching;
using Xunit;

namespace Exceptionless.Api.Tests.Caching {
    public class AzureCacheClientTests {
        [Fact]
        public void CanSetAndGetValue() {
            var cache = new AzureCacheClient();
            cache.Set("test", 1);
            var value = cache.Get<int>("test");
            Assert.Equal(1, value);
        }
    }
}
