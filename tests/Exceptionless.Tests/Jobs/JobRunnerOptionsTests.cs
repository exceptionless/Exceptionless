using Exceptionless.Job;
using Xunit;

namespace Exceptionless.Tests.Jobs;

public sealed class JobRunnerOptionsTests
{
    [Fact]
    public void RunDataSeedStartupAction_AllJobs_ReturnsFalse()
    {
        var options = new JobRunnerOptions([]);

        Assert.False(options.RunDataSeedStartupAction);
    }

    [Fact]
    public void RunDataSeedStartupAction_MigrationJob_ReturnsFalse()
    {
        var options = new JobRunnerOptions([nameof(JobRunnerOptions.Migration)]);

        Assert.False(options.RunDataSeedStartupAction);
    }

    [Fact]
    public void RunDataSeedStartupAction_NonMigrationJob_ReturnsTrue()
    {
        var options = new JobRunnerOptions([nameof(JobRunnerOptions.EventPosts)]);

        Assert.True(options.RunDataSeedStartupAction);
    }
}
