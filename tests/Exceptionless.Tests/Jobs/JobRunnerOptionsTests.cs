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

    [Fact]
    public void Constructor_MigrationRerunArguments_SelectsOnlyMigrationRerun()
    {
        var options = new JobRunnerOptions([nameof(JobRunnerOptions.Migration), "--rerun", "5"]);

        Assert.True(options.Migration);
        Assert.False(options.AllJobs);
        Assert.Equal("5", options.RerunMigrationId);
        Assert.False(options.RunDataSeedStartupAction);
        Assert.Empty(options.ConfigurationArguments);
    }

    [Fact]
    public void Constructor_InvalidMultipleArguments_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new JobRunnerOptions([nameof(JobRunnerOptions.Migration), "5"]));
    }
}
