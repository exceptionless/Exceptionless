using System;
using System.Threading.Tasks;
using Exceptionless.Api.Tests.Utility;
using Exceptionless.Core;
using Exceptionless.Core.Migrations;
using Exceptionless.Core.Utility;
using Xunit;

namespace Exceptionless.Api.Tests.Miscellaneous {
    public class SystemHealthCheckerTests {
        public SystemHealthCheckerTests() {
            if (Settings.Current.ShouldAutoUpgradeDatabase)
                MongoMigrationChecker.EnsureLatest(Settings.Current.MongoConnectionString, Settings.Current.MongoDatabaseName);
        }

        [Fact]
        public async Task CheckAll() {
            var checker = IoC.GetInstance<SystemHealthChecker>();
            Assert.NotNull(checker);
            var health = await checker.CheckAllAsync();
            Assert.True(health.IsHealthy, health.Message);
        }
    }
}