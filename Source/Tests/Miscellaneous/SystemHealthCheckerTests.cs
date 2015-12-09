using System;
using System.Threading.Tasks;
using Exceptionless.Api.Tests.Utility;
using Exceptionless.Core.Utility;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Api.Tests.Miscellaneous {
    public class SystemHealthCheckerTests : CaptureTests {
        private readonly SystemHealthChecker _checker = IoC.GetInstance<SystemHealthChecker>();
        public SystemHealthCheckerTests(CaptureFixture fixture, ITestOutputHelper output) : base(fixture, output) { }

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
        public async Task CheckMessageBusAsync() {
            var health = await _checker.CheckMessageBusAsync();
            Assert.True(health.IsHealthy, health.Message);
        }


        [Fact]
        public async Task CheckQueueAsync() {
           var health = await _checker.CheckQueueAsync();
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