using System;
using System.Threading.Tasks;
using Exceptionless.Api.Tests.Utility;
using Exceptionless.Core.Utility;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Api.Tests.Miscellaneous {
    public class SystemHealthCheckerTests : CaptureTests {
        public SystemHealthCheckerTests(CaptureFixture fixture, ITestOutputHelper output) : base(fixture, output) { }

        [Fact]
        public async Task CheckAllAsync() {
            var checker = IoC.GetInstance<SystemHealthChecker>();
            Assert.NotNull(checker);

            var health = await checker.CheckAllAsync();
            Assert.True(health.IsHealthy, health.Message);
        }
    }
}