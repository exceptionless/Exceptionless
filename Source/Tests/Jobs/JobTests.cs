using System;
using System.Threading.Tasks;
using Exceptionless.Core.Jobs;
using Foundatio.Jobs;
using Foundatio.Logging.Xunit;
using Foundatio.ServiceProviders;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Api.Tests.Jobs {
    public class JobTests : TestWithLoggingBase {
        public JobTests(ITestOutputHelper output) : base(output) {
            ServiceProvider.SetServiceProvider(typeof(JobBootstrapper));
        }

        [Fact]
        public async Task CanRunJobWithNoBootstrapperAsync() {
            var job = new JobRunner(Log).CreateJobInstance(typeof(TestJob).AssemblyQualifiedName);
            Assert.NotNull(job);
            Assert.Equal(0, TestJob.RunCount);
            Assert.Equal(JobResult.Success, await job.RunAsync());
            Assert.Equal(1, TestJob.RunCount);
        }

        [Fact]
        public void CanRunJobWithBootstrapper() {
            var job = new JobRunner(Log).CreateJobInstance(typeof(EventPostsJob).AssemblyQualifiedName);
            Assert.NotNull(job);
        }
    }
}