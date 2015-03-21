using System;
using Exceptionless.Api.Tests.Utility;
using Exceptionless.Core.Utility;
using Xunit;

namespace Exceptionless.Api.Tests.Miscellaneous {
    public class SystemHealthCheckerTests {
        [Fact]
        public void CheckAll() {
            var checker = IoC.GetInstance<SystemHealthChecker>();
            Assert.NotNull(checker);
            var health = checker.CheckAll();
            Assert.True(health.IsHealthy, health.Message);
        }
    }
}