using System;
using System.Threading.Tasks;
using Exceptionless.Core.Repositories.Configuration;
using Foundatio.Utility;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Api.Tests.Repositories {
    public sealed class IndexTests : TestBase {
        private readonly ExceptionlessElasticConfiguration _configuration;
        public IndexTests(ITestOutputHelper output) : base(output) {
            _configuration = GetService<ExceptionlessElasticConfiguration>();
            _configuration.DeleteIndexesAsync().GetAwaiter().GetResult();
        }

        [Fact]
        public async Task CanCreateOrganizationIndex() {
            await _configuration.Organizations.ConfigureAsync();
        }

        [Fact]
        public async Task CanCreateStackIndex() {
            await _configuration.Stacks.ConfigureAsync();
        }

        [Fact]
        public async Task CanCreateEventIndex() {
            await _configuration.Events.ConfigureAsync();
            await _configuration.Events.EnsureIndexAsync(SystemClock.UtcNow);
        }
    }
}