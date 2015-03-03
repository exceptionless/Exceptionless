using System;
using Exceptionless.Core.Jobs;
using Foundatio.Jobs;
using Xunit;

namespace Exceptionless.Api.Tests.Jobs {
    public class JobTests {
        [Fact]
        public void CanRunJobWithNoBootstrapper() {
            var job = JobRunner.CreateJobInstance(typeof(TestJob).AssemblyQualifiedName);
            Assert.NotNull(job);
            Assert.Equal(0, TestJob.RunCount);
            Assert.Equal(JobResult.Success, job.Run());
            Assert.Equal(1, TestJob.RunCount);
        }

        [Fact]
        public void CanRunJobWithBootstrapper() {
            var job = JobRunner.CreateJobInstance(typeof(EventPostsJob).AssemblyQualifiedName, Guid.NewGuid().ToString());
            Assert.Null(job);

            job = JobRunner.CreateJobInstance(typeof(EventPostsJob).AssemblyQualifiedName, typeof(TestJob).AssemblyQualifiedName);
            Assert.Null(job);

            job = JobRunner.CreateJobInstance(typeof(EventPostsJob).AssemblyQualifiedName);
            Assert.NotNull(job);

            job = JobRunner.CreateJobInstance(typeof(EventPostsJob).AssemblyQualifiedName, typeof(EventPostsJob).AssemblyQualifiedName);
            Assert.NotNull(job);
        }
    }
}