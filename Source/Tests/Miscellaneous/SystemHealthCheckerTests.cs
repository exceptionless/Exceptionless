using System;
using System.Threading.Tasks;
using Exceptionless.Api.Tests.Utility;
using Exceptionless.Core.Utility;
using Foundatio.Logging.Xunit;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Api.Tests.Miscellaneous {
    public class SystemHealthCheckerTests : TestWithLoggingBase {
        private readonly SystemHealthChecker _checker = IoC.GetInstance<SystemHealthChecker>();
        public SystemHealthCheckerTests(ITestOutputHelper output) : base(output) { }

        [Fact]
        public async Task CheckCacheAsync() {
            var health = await _checker.CheckCacheAsync();
            Assert.True(health.IsHealthy, health.Message);
        }

        [Fact]
        public async Task CheckElasticsearchAsync() {
            var health = await _checker.CheckElasticsearchAsync();
            Assert.True(health.IsHealthy, health.Message);
        }
        
        [Fact]
        public async Task CheckStorageAsync() {
            var health = await _checker.CheckStorageAsync();
            Assert.True(health.IsHealthy, health.Message);
        }

        [Fact]
        public async Task CheckAllAsync() {
            var health = await _checker.CheckAllAsync();
            Assert.True(health.IsHealthy, health.Message);
        }
    }
}