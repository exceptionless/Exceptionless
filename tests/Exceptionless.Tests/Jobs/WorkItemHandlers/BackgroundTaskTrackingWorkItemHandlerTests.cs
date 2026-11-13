using Exceptionless.Core.Jobs.WorkItemHandlers;
using Exceptionless.Core.Models.BackgroundTasks;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Services.BackgroundTasks;
using Exceptionless.Tests.Utility;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Utility;
using Foundatio.Xunit;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Exceptionless.Tests.Jobs.WorkItemHandlers;

public class BackgroundTaskTrackingWorkItemHandlerTests : TestWithLoggingBase, IDisposable
{
    private const string OrganizationId = "537650f3b77efe23a47914f4";
    private const string ProjectId = "537650f3b77efe23a47914f6";
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 3, 4, 5, 6, TimeSpan.Zero));
    private readonly BackgroundTaskTestHarness _harness;

    public BackgroundTaskTrackingWorkItemHandlerTests(ITestOutputHelper output) : base(output)
    {
        _harness = new BackgroundTaskTestHarness(_time, Log);
    }

    public void Dispose()
    {
        _harness.Dispose();
    }

    [Fact]
    public async Task HandleItemAsync_ProgressReports_UpdateTheTaskAndStillReachTheJobCallback()
    {
        // Arrange
        var task = await AddTaskAsync();
        var reported = new List<(int Progress, string? Message)>();
        BackgroundTask? duringWork = null;
        var handler = CreateHandler(async context =>
        {
            await context.ReportProgressAsync(0, "Starting");
            _time.Advance(TimeSpan.FromSeconds(30));
            await context.ReportProgressAsync(50, "Halfway");
            duringWork = await _harness.Tasks.GetAsync(task.Id);
        });

        // Act
        await handler.HandleItemAsync(CreateContext(task.Id, (progress, message) =>
        {
            reported.Add((progress, message));
            return Task.CompletedTask;
        }));

        // Assert
        Assert.Equal([(0, "Starting"), (50, "Halfway")], reported);
        Assert.Equal(BackgroundTaskStatus.Running, duringWork!.Status);
        Assert.Equal(50, duringWork.Progress);
        Assert.Equal("Halfway", duringWork.Message);
        Assert.NotNull(duringWork.StartedUtc);
        Assert.Contains(_harness.Publisher.Updates, u => u.Status == BackgroundTaskStatus.Running && u.Progress == 50);
    }

    [Fact]
    public async Task HandleItemAsync_WorkSucceeds_CompletesTheTaskAtOneHundredPercentWithTheLastMessage()
    {
        // Arrange
        var task = await AddTaskAsync();
        var handler = CreateHandler(async context =>
        {
            await context.ReportProgressAsync(50, "Events removed: 3");
            await context.ReportProgressAsync(100, "Events removed: 3, stacks removed: 1");
        });

        // Act
        await handler.HandleItemAsync(CreateContext(task.Id));
        var stored = await _harness.Tasks.GetAsync(task.Id);

        // Assert
        Assert.Equal(BackgroundTaskStatus.Completed, stored!.Status);
        Assert.Equal(100, stored.Progress);
        Assert.Equal("Events removed: 3, stacks removed: 1", stored.Message);
        Assert.NotNull(stored.CompletedUtc);
        Assert.Empty(await _harness.Tasks.GetActiveIdsAsync(OrganizationId));
        Assert.Equal(BackgroundTaskStatus.Completed, _harness.Publisher.Updates[^1].Status);
    }

    [Fact]
    public async Task HandleItemAsync_WorkThrows_FailsTheTaskAndRethrowsThenARetryBringsItBackToRunningAndCompletes()
    {
        // Arrange
        var task = await AddTaskAsync();
        int calls = 0;
        BackgroundTask? duringRetry = null;
        IReadOnlyList<string>? activeDuringRetry = null;
        var handler = CreateHandler(async context =>
        {
            if (++calls == 1)
                throw new InvalidOperationException("Elasticsearch unavailable");

            duringRetry = await _harness.Tasks.GetAsync(task.Id);
            activeDuringRetry = await _harness.Tasks.GetActiveIdsAsync(OrganizationId);
            await context.ReportProgressAsync(100, "Done");
        });

        // Act
        var thrown = await Record.ExceptionAsync(() => handler.HandleItemAsync(CreateContext(task.Id)));
        var failed = await _harness.Tasks.GetAsync(task.Id);
        var activeAfterFailure = await _harness.Tasks.GetActiveIdsAsync(OrganizationId);
        await handler.HandleItemAsync(CreateContext(task.Id));
        var completed = await _harness.Tasks.GetAsync(task.Id);

        // Assert
        Assert.IsType<InvalidOperationException>(thrown);
        Assert.Equal(BackgroundTaskStatus.Failed, failed!.Status);
        Assert.DoesNotContain("Elasticsearch unavailable", failed.Message);
        Assert.Empty(activeAfterFailure);
        Assert.Equal(BackgroundTaskStatus.Running, duringRetry!.Status);
        Assert.Null(duringRetry.CompletedUtc);
        Assert.Equal([task.Id], activeDuringRetry!.ToArray());
        Assert.Equal(BackgroundTaskStatus.Completed, completed!.Status);
        Assert.Empty(await _harness.Tasks.GetActiveIdsAsync(OrganizationId));
    }

    [Fact]
    public async Task HandleItemAsync_ResultIsNotASuccess_PassesItToTheJobAndKeepsTheTaskRunning()
    {
        // Arrange
        var task = await AddTaskAsync();
        var handler = CreateHandler(context =>
        {
            context.Result = JobResult.CancelledWithMessage("Shutting down");
            return Task.CompletedTask;
        });
        var context = CreateContext(task.Id);

        // Act
        await handler.HandleItemAsync(context);
        var stored = await _harness.Tasks.GetAsync(task.Id);

        // Assert
        Assert.False(context.Result.IsSuccess);
        Assert.Equal("Shutting down", context.Result.Message);
        Assert.Equal(BackgroundTaskStatus.Running, stored!.Status);
    }

    [Fact]
    public async Task HandleItemAsync_WorkItemWithoutATaskId_PassesStraightThroughWithoutTouchingAnyTask()
    {
        // Arrange
        var other = await AddTaskAsync();
        WorkItemContext? received = null;
        var handler = CreateHandler(async context =>
        {
            received = context;
            await context.ReportProgressAsync(10, "Working");
        });
        var context = new WorkItemContext(new ResetProjectDataWorkItem { OrganizationId = OrganizationId, ProjectId = ProjectId }, "job", EmptyLock.Empty, TestCancellationToken, (_, _) => Task.CompletedTask);

        // Act
        await handler.HandleItemAsync(context);
        var untouched = await _harness.Tasks.GetAsync(other.Id);

        // Assert
        Assert.Same(context, received);
        Assert.Equal(BackgroundTaskStatus.Queued, untouched!.Status);
        Assert.Empty(_harness.Publisher.Updates);
    }

    [Fact]
    public async Task HandleItemAsync_TaskRecordIsGone_StillRunsTheWork()
    {
        // Arrange
        bool ran = false;
        var handler = CreateHandler(async context =>
        {
            await context.ReportProgressAsync(100, "Done");
            ran = true;
        });

        // Act
        await handler.HandleItemAsync(CreateContext(Foundatio.Repositories.Utility.ObjectId.GenerateNewId().ToString()));

        // Assert
        Assert.True(ran);
        Assert.Empty(_harness.Publisher.Updates);
    }

    [Theory]
    [InlineData(BackgroundTaskStatus.Cancelled)]
    [InlineData(BackgroundTaskStatus.Completed)]
    public async Task HandleItemAsync_TaskIsCancelledOrCompleted_DoesNotRunOrRevive(BackgroundTaskStatus status)
    {
        // Arrange
        var task = await AddTaskAsync(canCancel: true);
        await _harness.Tasks.UpdateAsync(task.Id, current =>
        {
            current.Finish(status, "Finished", _time.GetUtcNow().UtcDateTime);
            return true;
        });
        bool ran = false;
        var handler = CreateHandler(_ =>
        {
            ran = true;
            return Task.CompletedTask;
        });
        var context = CreateContext(task.Id);

        // Act
        await handler.HandleItemAsync(context);

        // Assert
        Assert.False(ran);
        Assert.True(context.Result.IsSuccess);
        Assert.Equal(status, (await _harness.Tasks.GetAsync(task.Id))!.Status);
    }

    [Theory]
    [InlineData(BackgroundTaskStatus.Cancelled)]
    [InlineData(BackgroundTaskStatus.Completed)]
    public async Task HandleItemAsync_TaskStatusCannotBeChecked_DoesNotRunTheWorker(BackgroundTaskStatus status)
    {
        // Arrange
        using var harness = new BackgroundTaskTestHarness(_time, Log, new BackgroundTaskOptions { LockAcquireTimeout = TimeSpan.Zero }, lockAttempts: 1);
        var task = await AddTaskAsync(harness: harness);
        await harness.Tasks.UpdateAsync(task.Id, current =>
        {
            current.Finish(status, "Finished", _time.GetUtcNow().UtcDateTime);
            return true;
        });
        await using var held = await harness.LockProvider.AcquireAsync(BackgroundTaskService.GetUpdateLockResource(task.Id), TimeSpan.FromSeconds(30), TestCancellationToken);
        bool ran = false;
        var inner = new DelegateWorkItemHandler(_ =>
        {
            ran = true;
            return Task.CompletedTask;
        });
        var handler = new BackgroundTaskTrackingWorkItemHandler(inner, harness.Tasks, _time);

        // Act
        var exception = await Record.ExceptionAsync(() => handler.HandleItemAsync(CreateContext(task.Id)));

        // Assert
        Assert.IsType<LockAcquisitionTimeoutException>(exception);
        Assert.False(ran);
        Assert.Equal(status, (await harness.Tasks.GetAsync(task.Id))!.Status);
    }

    [Fact]
    public void Handler_DelegatesTheJobSettingsToTheWrappedHandler()
    {
        // Arrange
        var inner = new DelegateWorkItemHandler(_ => Task.CompletedTask) { AutoRenewLockOnProgress = true };
        var handler = new BackgroundTaskTrackingWorkItemHandler(inner, _harness.Tasks, _time);

        // Act
        handler.AutoRenewLockOnProgress = false;

        // Assert
        Assert.False(inner.AutoRenewLockOnProgress);
        Assert.Same(inner.Log, handler.Log);
    }

    private async Task<BackgroundTask> AddTaskAsync(bool canCancel = false, BackgroundTaskTestHarness? harness = null)
    {
        var task = new BackgroundTask
        {
            OrganizationId = OrganizationId,
            ProjectId = ProjectId,
            Type = BackgroundTaskTypes.ResetProjectData,
            Description = "Reset data for Test",
            CreatedByUserId = "537650f3b77efe23a47914f8",
            CanCancel = canCancel
        };
        await (harness ?? _harness).Tasks.AddAsync(task);
        return task;
    }

    private BackgroundTaskTrackingWorkItemHandler CreateHandler(Func<WorkItemContext, Task> work)
    {
        return new BackgroundTaskTrackingWorkItemHandler(new DelegateWorkItemHandler(work), _harness.Tasks, _time);
    }

    private WorkItemContext CreateContext(string taskId, Func<int, string?, Task>? progress = null)
    {
        var workItem = new ResetProjectDataWorkItem { OrganizationId = OrganizationId, ProjectId = ProjectId, TaskId = taskId };
        return new WorkItemContext(workItem, "job", EmptyLock.Empty, TestCancellationToken, progress ?? ((_, _) => Task.CompletedTask));
    }
}
