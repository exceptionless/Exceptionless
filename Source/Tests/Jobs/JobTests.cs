using System;
using System.Threading.Tasks;
using Exceptionless.Core.Jobs;
using Foundatio.Jobs;
using Foundatio.ServiceProviders;
using Xunit;

namespace Exceptionless.Api.Tests.Jobs {
    public class JobTests {
        public JobTests() {
            ServiceProvider.SetServiceProvider(typeof(JobBootstrapper));
        }

        [Fact]
        public async Task CanRunJobWithNoBootstrapperAsync() {
            var job = JobRunner.CreateJobInstance(typeof(TestJob).AssemblyQualifiedName);
            Assert.NotNull(job);
            Assert.Equal(0, TestJob.RunCount);
            Assert.Equal(JobResult.Success, await job.RunAsync());
            Assert.Equal(1, TestJob.RunCount);
        }

        [Fact]
        public void CanRunJobWithBootstrapper() {
            var job = JobRunner.CreateJobInstance(typeof(EventPostsJob).AssemblyQualifiedName);
            Assert.NotNull(job);
        }
    }
}