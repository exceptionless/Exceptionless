using System;
using System.Threading.Tasks;
using Exceptionless.Api.Tests.Utility;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Utility;
using Xunit;

namespace Exceptionless.Api.Tests.Miscellaneous {
    public class SystemHealthCheckerTests {
        [Fact]
        public async Task CheckAll() {
            var checker = IoC.GetInstance<SystemHealthChecker>();
            Assert.NotNull(checker);
            var health = await checker.CheckAllAsync().AnyContext();
            Assert.True(health.IsHealthy, health.Message);
        }
    }
}