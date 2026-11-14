using Exceptionless.Core.Models.BackgroundTasks;
using Foundatio.Xunit;
using Xunit;

namespace Exceptionless.Tests.Models;

public class BackgroundTaskTests : TestWithLoggingBase
{
    private static readonly DateTime UtcNow = new(2026, 11, 3, 4, 5, 6, DateTimeKind.Utc);

    public BackgroundTaskTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void Start_CalledTwice_KeepsTheFirstStartTime()
    {
        // Arrange
        var task = new BackgroundTask();

        // Act
        task.Start(UtcNow);
        task.Start(UtcNow.AddMinutes(5));

        // Assert
        Assert.Equal(BackgroundTaskStatus.Running, task.Status);
        Assert.Equal(UtcNow, task.StartedUtc);
    }

    [Theory]
    [InlineData(BackgroundTaskStatus.Completed, 100)]
    [InlineData(BackgroundTaskStatus.Failed, 40)]
    [InlineData(BackgroundTaskStatus.Cancelled, 40)]
    public void Finish_AnyOutcome_StopsTheTaskAndOnlyCompletionReachesOneHundredPercent(BackgroundTaskStatus status, int expectedProgress)
    {
        // Arrange
        var task = new BackgroundTask { Progress = 40 };
        task.Start(UtcNow);

        // Act
        task.Finish(status, "Done", UtcNow.AddMinutes(1));

        // Assert
        Assert.False(task.IsActive);
        Assert.Equal(status, task.Status);
        Assert.Equal("Done", task.Message);
        Assert.Equal(UtcNow.AddMinutes(1), task.CompletedUtc);
        Assert.Equal(expectedProgress, task.Progress);
    }
}
