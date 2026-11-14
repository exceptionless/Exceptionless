using Exceptionless.Core.Models.BackgroundTasks;
using Exceptionless.Core.Models.BulkOperations;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Services.BackgroundTasks;
using Exceptionless.Core.Services.BulkOperations;
using Exceptionless.Tests.Utility;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Queues;
using Foundatio.Repositories.Utility;
using Foundatio.Serializer;
using Foundatio.Xunit;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace Exceptionless.Tests.Services.BackgroundTasks;

public class BackgroundTaskServiceTests : TestWithLoggingBase, IDisposable
{
    private const string OrganizationId = "537650f3b77efe23a47914f4";
    private const string OtherOrganizationId = "537650f3b77efe23a47914f5";
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 3, 4, 5, 6, TimeSpan.Zero));
    private readonly BackgroundTaskTestHarness _harness;

    public BackgroundTaskServiceTests(ITestOutputHelper output) : base(output)
    {
        _harness = new BackgroundTaskTestHarness(_time, Log);
    }

    public void Dispose()
    {
        _harness.Dispose();
    }

    private BackgroundTaskService Tasks => _harness.Tasks;

    [Fact]
    public async Task AddAsync_NewTask_StoresItQueuedAndIndexesIt()
    {
        // Arrange
        var task = NewTask();

        // Act
        await Tasks.AddAsync(task);
        var stored = await Tasks.GetAsync(task.Id);
        var listed = await Tasks.GetByOrganizationAsync(OrganizationId);
        var active = await Tasks.GetActiveIdsAsync(OrganizationId);

        // Assert
        Assert.True(ObjectId.TryParse(task.Id, out _));
        Assert.NotNull(stored);
        Assert.Equal(BackgroundTaskStatus.Queued, stored.Status);
        Assert.Equal(_time.GetUtcNow().UtcDateTime, stored.CreatedUtc);
        Assert.Equal(stored.CreatedUtc, stored.UpdatedUtc);
        Assert.Equal([task.Id], listed.Select(t => t.Id).ToArray());
        Assert.Equal([task.Id], active.ToArray());
        Assert.Empty(_harness.Publisher.Updates);
    }

    [Theory]
    [InlineData(true, typeof(MaxEntrySizeExceededCacheException))]
    [InlineData(false, typeof(InvalidOperationException))]
    public async Task CreateAndEnqueueAsync_TaskCannotBeStored_DoesNotQueueOrPublish(bool throwOnRejection, Type expectedExceptionType)
    {
        // Arrange
        using var harness = new BackgroundTaskTestHarness(_time, Log, configureCache: options =>
        {
            options.MaxEntrySize = 1;
            options.SizeCalculator = value => value is BackgroundTask ? 2 : 1;
            options.ShouldThrowOnMaxEntrySizeExceeded = throwOnRejection;
        });
        var task = NewTask();

        // Act
        var thrown = await Record.ExceptionAsync(async () => await harness.Tasks.CreateAndEnqueueAsync(task,
            current => new ResetProjectDataWorkItem { OrganizationId = OrganizationId, ProjectId = task.ProjectId!, TaskId = current.Id }));
        var stored = await harness.Tasks.GetAsync(task.Id);
        var active = await harness.Tasks.GetActiveIdsAsync(OrganizationId);
        var queued = await harness.Queue.GetQueueStatsAsync();

        // Assert
        Assert.IsType(expectedExceptionType, thrown);
        Assert.Null(stored);
        Assert.Empty(active);
        Assert.Equal(0, queued.Enqueued);
        Assert.Empty(harness.Publisher.Updates);
    }

    [Theory]
    [InlineData(true, typeof(MaxEntrySizeExceededCacheException))]
    [InlineData(false, typeof(InvalidOperationException))]
    public async Task CreateAndEnqueueAsync_IndexCannotBeStored_DoesNotQueueOrLeaveAnActiveTask(bool throwOnRejection, Type expectedExceptionType)
    {
        // Arrange
        using var harness = new BackgroundTaskTestHarness(_time, Log, configureCache: options =>
        {
            options.MaxEntrySize = 1;
            options.SizeCalculator = value => value is IDictionary<string, DateTime?> ? 2 : 1;
            options.ShouldThrowOnMaxEntrySizeExceeded = throwOnRejection;
        });
        var task = NewTask();

        // Act
        var thrown = await Record.ExceptionAsync(async () => await harness.Tasks.CreateAndEnqueueAsync(task,
            current => new ResetProjectDataWorkItem { OrganizationId = OrganizationId, ProjectId = task.ProjectId!, TaskId = current.Id }));
        var stored = await harness.Tasks.GetAsync(task.Id);
        var active = await harness.Tasks.GetActiveIdsAsync(OrganizationId);
        var queued = await harness.Queue.GetQueueStatsAsync();

        // Assert
        Assert.IsType(expectedExceptionType, thrown);
        Assert.NotNull(stored);
        Assert.Equal(BackgroundTaskStatus.Failed, stored.Status);
        Assert.Empty(active);
        Assert.Equal(0, queued.Enqueued);
    }

    [Fact]
    public async Task GetByIdsAsync_MissingAndRepeatedIdsAcrossBatches_ReturnsExistingTasksInInputOrder()
    {
        // Arrange
        var tasks = new List<BackgroundTask>();
        for (int index = 0; index <= BackgroundTaskService.ReadBatchSize; index++)
        {
            var task = NewTask();
            await Tasks.AddAsync(task);
            tasks.Add(task);
        }

        string[] requested = [.. tasks.Select(task => task.Id).Reverse(), ObjectId.GenerateNewId().ToString(), tasks[0].Id];

        // Act
        var found = await Tasks.GetByIdsAsync(requested);

        // Assert
        Assert.Equal(tasks.Select(task => task.Id).Reverse(), found.Select(task => task.Id));
    }

    [Fact]
    public async Task GetByOrganizationAsync_FinishedTasks_ReturnsTheNewestFirstUpToTheLimit()
    {
        // Arrange
        var ids = new List<string>();
        for (int index = 0; index < 4; index++)
        {
            var task = NewTask();
            await Tasks.AddAsync(task);
            await Tasks.UpdateAsync(task.Id, current =>
            {
                current.Finish(BackgroundTaskStatus.Completed, "Done", _time.GetUtcNow().UtcDateTime);
                return true;
            });
            ids.Add(task.Id);
            _time.Advance(TimeSpan.FromSeconds(5));
        }

        // Act
        var newestTwo = await Tasks.GetByOrganizationAsync(OrganizationId, limit: 2);

        // Assert
        Assert.Equal([ids[3], ids[2]], newestTwo.Select(t => t.Id).ToArray());
    }

    [Fact]
    public async Task GetByOrganizationAsync_HistoryExceedsFormerPageLimit_ReturnsNewestTask()
    {
        // Arrange
        const int historyCount = 50_001;
        var utcNow = _time.GetUtcNow().UtcDateTime;
        var firstCreatedUtc = utcNow.AddSeconds(1 - historyCount);
        var ids = Enumerable.Range(0, historyCount)
            .Select(index => ObjectId.GenerateNewId(firstCreatedUtc.AddSeconds(index)).ToString())
            .ToArray();
        var newest = NewTask();
        newest.Id = ids[^1];
        newest.CreatedUtc = utcNow;
        newest.UpdatedUtc = utcNow;
        newest.Finish(BackgroundTaskStatus.Completed, "Done", utcNow);
        await _harness.Cache.SetAsync(BackgroundTaskService.GetTaskCacheKey(newest.Id), newest, BackgroundTaskService.TaskLifetime);
        await _harness.Cache.ListAddAsync(BackgroundTaskService.GetOrganizationIndexCacheKey(OrganizationId), ids, BackgroundTaskService.TaskLifetime);

        // Act
        var listed = await Tasks.GetByOrganizationAsync(OrganizationId, limit: 1);

        // Assert
        Assert.Equal(newest.Id, Assert.Single(listed).Id);
    }

    [Fact]
    public async Task GetByOrganizationAsync_ActiveTasks_DoNotConsumeTheFinishedHistoryLimit()
    {
        // Arrange
        var oldest = NewTask();
        await Tasks.AddAsync(oldest);
        var finishedIds = new List<string>();
        for (int index = 0; index <= BackgroundTaskService.ListLimit; index++)
        {
            _time.Advance(TimeSpan.FromSeconds(1));
            var task = NewTask();
            await Tasks.AddAsync(task);
            await Tasks.UpdateAsync(task.Id, current =>
            {
                current.Finish(BackgroundTaskStatus.Completed, "Done", _time.GetUtcNow().UtcDateTime);
                return true;
            });
            finishedIds.Add(task.Id);
        }

        _time.Advance(TimeSpan.FromSeconds(1));
        var newest = NewTask();
        await Tasks.AddAsync(newest);

        // Act
        var listed = await Tasks.GetByOrganizationAsync(OrganizationId);

        // Assert
        Assert.Equal(BackgroundTaskService.ListLimit + 2, listed.Count);
        Assert.Equal(newest.Id, listed[0].Id);
        Assert.Equal(oldest.Id, listed[^1].Id);
        Assert.Equal(finishedIds.Skip(1).Reverse(), listed.Where(task => !task.IsActive).Select(task => task.Id));
        Assert.Equal(listed.Count, listed.Select(task => task.Id).Distinct().Count());
    }

    [Fact]
    public async Task GetByOrganizationAsync_ExpiredAndForeignIds_AreSkippedAndExpiredIdsCleanedFromTheIndex()
    {
        // Arrange
        var kept = NewTask();
        var expired = NewTask();
        var foreign = NewTask(OtherOrganizationId);
        await Tasks.AddAsync(kept);
        _time.Advance(TimeSpan.FromSeconds(1));
        await Tasks.AddAsync(expired);
        _time.Advance(TimeSpan.FromSeconds(1));
        await Tasks.AddAsync(foreign);
        await _harness.Cache.RemoveAsync(BackgroundTaskService.GetTaskCacheKey(expired.Id));
        // A stray id from another organization must never be listed.
        await _harness.Cache.ListAddAsync(BackgroundTaskService.GetOrganizationIndexCacheKey(OrganizationId), [foreign.Id]);

        // Act
        var listed = await Tasks.GetByOrganizationAsync(OrganizationId);
        var indexed = await _harness.Cache.GetListAsync<string>(BackgroundTaskService.GetOrganizationIndexCacheKey(OrganizationId));

        // Assert
        Assert.Equal([kept.Id], listed.Select(t => t.Id).ToArray());
        Assert.DoesNotContain(expired.Id, indexed.Value);
        Assert.Contains(kept.Id, indexed.Value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task UpdateAsync_NotificationWaits_PersistsOrderedUpdatesWithoutHoldingTheLock(int clockAdjustmentSeconds)
    {
        // Arrange
        using var harness = new BackgroundTaskTestHarness(_time, Log, new BackgroundTaskOptions { LockAcquireTimeout = TimeSpan.Zero }, lockAttempts: 1);
        var task = NewTask();
        await harness.Tasks.AddAsync(task);
        var publishing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releasePublication = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.Publisher.BeforePublish = () =>
        {
            harness.Publisher.BeforePublish = null;
            publishing.SetResult();
            return releasePublication.Task;
        };

        // Act
        var firstUpdate = harness.Tasks.UpdateAsync(task.Id, current =>
        {
            current.Start(_time.GetUtcNow().UtcDateTime);
            current.Progress = 20;
            return true;
        });
        Exception? exception;
        try
        {
            await publishing.Task.WaitAsync(TestCancellationToken);
            _time.AdjustTime(_time.GetUtcNow().AddSeconds(clockAdjustmentSeconds));
            exception = await Record.ExceptionAsync(async () => await harness.Tasks.UpdateAsync(task.Id, current =>
            {
                current.Progress = 40;
                return true;
            }));
        }
        finally
        {
            releasePublication.SetResult();
            await firstUpdate;
        }

        var stored = await harness.Tasks.GetAsync(task.Id);
        var updates = harness.Publisher.Updates;

        // Assert
        Assert.Null(exception);
        Assert.NotNull(stored);
        Assert.Equal(40, stored.Progress);
        Assert.Equal([40, 20], updates.Select(update => update.Progress));
        Assert.Equal(stored.UpdatedUtc, updates[0].UpdatedUtc);
        Assert.True(updates[0].UpdatedUtc > updates[1].UpdatedUtc);
    }

    [Fact]
    public async Task UpdateAsync_UpdateReturnsFalse_SavesAndPublishesNothing()
    {
        // Arrange
        var task = NewTask();
        await Tasks.AddAsync(task);
        _time.Advance(TimeSpan.FromMinutes(1));

        // Act
        var result = await Tasks.UpdateAsync(task.Id, current =>
        {
            current.Progress = 50;
            return false;
        });
        var stored = await Tasks.GetAsync(task.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, stored!.Progress);
        Assert.Equal(stored.CreatedUtc, stored.UpdatedUtc);
        Assert.Empty(_harness.Publisher.Updates);
    }

    [Fact]
    public async Task UpdateAsync_ChangeThatLeavesTheTaskActive_SavesStampsAndPublishesIt()
    {
        // Arrange
        var task = NewTask();
        await Tasks.AddAsync(task);
        _time.Advance(TimeSpan.FromMinutes(1));

        // Act
        var result = await Tasks.UpdateAsync(task.Id, current =>
        {
            current.Start(_time.GetUtcNow().UtcDateTime);
            current.Progress = 40;
            current.Message = "Working";
            return true;
        });
        var stored = await Tasks.GetAsync(task.Id);
        var active = await Tasks.GetActiveIdsAsync(OrganizationId);

        // Assert
        Assert.Equal(40, stored!.Progress);
        Assert.Equal(_time.GetUtcNow().UtcDateTime, result!.UpdatedUtc);
        Assert.Equal(result.UpdatedUtc, stored.UpdatedUtc);
        Assert.Equal([task.Id], active.ToArray());
        var update = Assert.Single(_harness.Publisher.Updates);
        Assert.Equal(task.Id, update.Id);
        Assert.Equal(OrganizationId, update.OrganizationId);
        Assert.Equal(BackgroundTaskStatus.Running, update.Status);
        Assert.Equal(40, update.Progress);
        Assert.Equal("Working", update.Message);
        Assert.Equal(task.CanCancel, update.CanCancel);
    }

    [Fact]
    public async Task UpdateAsync_LongRunningTask_KeepsIndexesForTheSavedRecordLifetime()
    {
        // Arrange
        ObservingHistoryCacheClient? cache = null;
        using var harness = new BackgroundTaskTestHarness(_time, Log,
            decorateCache: inner => cache = new ObservingHistoryCacheClient(inner));
        var tasks = harness.Tasks;
        var task = NewTask();
        await tasks.AddAsync(task);
        _time.Advance(TimeSpan.FromDays(6));

        // Act
        await tasks.UpdateAsync(task.Id, current =>
        {
            current.Start(_time.GetUtcNow().UtcDateTime);
            current.Progress = 50;
            return true;
        });
        _time.Advance(TimeSpan.FromDays(2));
        var stored = await tasks.GetAsync(task.Id);
        var active = await tasks.GetActiveIdsAsync(OrganizationId);
        var listed = await tasks.GetByOrganizationAsync(OrganizationId);

        // Assert
        Assert.NotNull(stored);
        Assert.Equal(BackgroundTaskStatus.Running, stored.Status);
        Assert.Equal([task.Id], active);
        Assert.Equal(task.Id, Assert.Single(listed).Id);

        // Arrange
        IReadOnlyList<BackgroundTask> duringHistoryRefresh = [];
        cache!.BeforeHistoryRefresh = async () => duringHistoryRefresh = await tasks.GetByOrganizationAsync(OrganizationId);

        // Act
        await tasks.UpdateAsync(task.Id, current =>
        {
            current.Finish(BackgroundTaskStatus.Completed, "Done", _time.GetUtcNow().UtcDateTime);
            return true;
        });
        _time.Advance(TimeSpan.FromDays(6));
        var finished = await tasks.GetByOrganizationAsync(OrganizationId);
        var finishedActive = await tasks.GetActiveIdsAsync(OrganizationId);

        // Assert
        Assert.Equal(task.Id, Assert.Single(duringHistoryRefresh).Id);
        Assert.Equal(BackgroundTaskStatus.Completed, Assert.Single(finished).Status);
        Assert.Empty(finishedActive);

        // Act
        _time.Advance(TimeSpan.FromDays(1) + TimeSpan.FromSeconds(1));
        var expired = await tasks.GetAsync(task.Id);
        var expiredList = await tasks.GetByOrganizationAsync(OrganizationId);

        // Assert
        Assert.Null(expired);
        Assert.Empty(expiredList);
    }

    [Fact]
    public async Task UpdateAsync_TaskFinishes_RemovesItFromTheActiveIndexAndPublishesTheFinalState()
    {
        // Arrange
        var task = NewTask();
        await Tasks.AddAsync(task);

        // Act
        await Tasks.UpdateAsync(task.Id, current =>
        {
            current.Finish(BackgroundTaskStatus.Completed, "Done", _time.GetUtcNow().UtcDateTime);
            return true;
        });
        var active = await Tasks.GetActiveIdsAsync(OrganizationId);
        var listed = await Tasks.GetByOrganizationAsync(OrganizationId);

        // Assert
        Assert.Empty(active);
        Assert.Equal(BackgroundTaskStatus.Completed, Assert.Single(listed).Status);
        var update = Assert.Single(_harness.Publisher.Updates);
        Assert.Equal(BackgroundTaskStatus.Completed, update.Status);
        Assert.Equal(100, update.Progress);
    }

    [Theory]
    [InlineData(true, typeof(MaxEntrySizeExceededCacheException))]
    [InlineData(false, typeof(InvalidOperationException))]
    public async Task UpdateAsync_TaskCannotBeStored_PreservesTheStoredStateAndActiveIndex(bool throwOnRejection, Type expectedExceptionType)
    {
        // Arrange
        using var harness = new BackgroundTaskTestHarness(_time, Log, configureCache: options =>
        {
            options.MaxEntrySize = 1;
            options.SizeCalculator = value => value is BackgroundTask { Status: BackgroundTaskStatus.Completed } ? 2 : 1;
            options.ShouldThrowOnMaxEntrySizeExceeded = throwOnRejection;
        });
        var task = NewTask();
        await harness.Tasks.AddAsync(task);

        // Act
        var thrown = await Record.ExceptionAsync(async () => await harness.Tasks.UpdateAsync(task.Id, current =>
        {
            current.Finish(BackgroundTaskStatus.Completed, "Done", _time.GetUtcNow().UtcDateTime);
            return true;
        }));
        var stored = await harness.Tasks.GetAsync(task.Id);
        var active = await harness.Tasks.GetActiveIdsAsync(OrganizationId);

        // Assert
        Assert.IsType(expectedExceptionType, thrown);
        Assert.Equal(BackgroundTaskStatus.Queued, stored!.Status);
        Assert.Null(stored.CompletedUtc);
        Assert.Equal([task.Id], active.ToArray());
        Assert.Empty(harness.Publisher.Updates);
    }

    [Fact]
    public async Task UpdateAsync_UnknownTask_ReturnsNullWithoutRunningTheUpdate()
    {
        // Arrange
        bool called = false;

        // Act
        var result = await Tasks.UpdateAsync(ObjectId.GenerateNewId().ToString(), _ =>
        {
            called = true;
            return true;
        });

        // Assert
        Assert.Null(result);
        Assert.False(called);
        Assert.Empty(_harness.Publisher.Updates);
    }

    [Fact]
    public async Task UpdateAsync_PublisherIsDown_StillSavesTheChange()
    {
        // Arrange
        var task = NewTask();
        await Tasks.AddAsync(task);
        _harness.Publisher.Failure = new TimeoutException("Message bus unavailable");

        // Act
        var result = await Tasks.UpdateAsync(task.Id, current =>
        {
            current.Progress = 10;
            return true;
        });

        // Assert
        Assert.Equal(10, (await Tasks.GetAsync(task.Id))!.Progress);
        Assert.Equal(10, result!.Progress);
    }

    [Fact]
    public async Task UpdateAsync_LockBusyAtFirst_RetriesWithThePolicyAndSucceeds()
    {
        // Arrange
        var counting = new CountingLockProvider();
        using var harness = new BackgroundTaskTestHarness(TimeProvider.System, Log, decorateLockProvider: counting.Wrap);
        var task = NewTask();
        await harness.Tasks.AddAsync(task);
        var held = await harness.LockProvider.AcquireAsync(BackgroundTaskService.GetUpdateLockResource(task.Id), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(1));
        counting.Reset();
        counting.OnAttempt = attempts =>
        {
            // Release only after one attempt gave up, so the update can only succeed through a retry.
            if (attempts == 2)
                held.ReleaseAsync().GetAwaiter().GetResult();
        };

        // Act
        var result = await harness.Tasks.UpdateAsync(task.Id, current =>
        {
            current.Progress = 25;
            return true;
        });

        // Assert
        Assert.Equal(25, result!.Progress);
        Assert.Equal(2, counting.Attempts);
    }

    [Fact]
    public async Task UpdateAsync_LockNeverReleased_GivesUpAfterTheConfiguredAttempts()
    {
        // Arrange
        var counting = new CountingLockProvider();
        using var harness = new BackgroundTaskTestHarness(TimeProvider.System, Log, decorateLockProvider: counting.Wrap, lockAttempts: 3);
        var task = NewTask();
        await harness.Tasks.AddAsync(task);
        await using var held = await harness.LockProvider.AcquireAsync(BackgroundTaskService.GetUpdateLockResource(task.Id), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(1));
        counting.Reset();

        // Act
        var thrown = await Record.ExceptionAsync(async () => await harness.Tasks.UpdateAsync(task.Id, _ => true));

        // Assert
        Assert.IsType<LockAcquisitionTimeoutException>(thrown);
        Assert.Equal(3, counting.Attempts);
    }

    [Fact]
    public async Task CancelAsync_QueuedTask_CancelsImmediatelyAndRemembersTheRequest()
    {
        // Arrange
        var task = NewTask(canCancel: true);
        await Tasks.AddAsync(task);

        // Act
        var result = await Tasks.CancelAsync(task.Id);

        // Assert
        Assert.False(result.IsNotCancellable);
        Assert.Equal(BackgroundTaskStatus.Cancelled, result.Task!.Status);
        Assert.NotNull(result.Task.CompletedUtc);
        Assert.True(await Tasks.IsCancelRequestedAsync(task.Id));
        Assert.Empty(await Tasks.GetActiveIdsAsync(OrganizationId));
        Assert.Equal(BackgroundTaskStatus.Cancelled, Assert.Single(_harness.Publisher.Updates).Status);
    }

    [Fact]
    public async Task CancelAsync_RunningTaskWithRecentProgress_OnlyRequestsCancellationForTheNextBatch()
    {
        // Arrange
        var task = NewTask(canCancel: true);
        await Tasks.AddAsync(task);
        await Tasks.UpdateAsync(task.Id, current =>
        {
            current.Start(_time.GetUtcNow().UtcDateTime);
            return true;
        });
        _time.Advance(BackgroundTaskService.StaleAfter - TimeSpan.FromMinutes(1));

        // Act
        var result = await Tasks.CancelAsync(task.Id);

        // Assert
        Assert.Equal(BackgroundTaskStatus.Running, result.Task!.Status);
        Assert.True(await Tasks.IsCancelRequestedAsync(task.Id));
        Assert.Equal([task.Id], (await Tasks.GetActiveIdsAsync(OrganizationId)).ToArray());
    }

    [Theory]
    [InlineData(true, typeof(MaxEntrySizeExceededCacheException))]
    [InlineData(false, typeof(InvalidOperationException))]
    public async Task CancelAsync_RequestCannotBeStored_DoesNotAcknowledgeCancellation(bool throwOnRejection, Type expectedExceptionType)
    {
        // Arrange
        using var harness = new BackgroundTaskTestHarness(_time, Log, configureCache: options =>
        {
            options.MaxEntrySize = 1;
            options.SizeCalculator = value => value is bool ? 2 : 1;
            options.ShouldThrowOnMaxEntrySizeExceeded = throwOnRejection;
        });
        var task = NewTask(canCancel: true);
        await harness.Tasks.AddAsync(task);
        await harness.Tasks.UpdateAsync(task.Id, current =>
        {
            current.Start(_time.GetUtcNow().UtcDateTime);
            return true;
        });
        int publishedBefore = harness.Publisher.Updates.Count;

        // Act
        var thrown = await Record.ExceptionAsync(async () => await harness.Tasks.CancelAsync(task.Id));
        var stored = await harness.Tasks.GetAsync(task.Id);
        bool cancellationRequested = await harness.Tasks.IsCancelRequestedAsync(task.Id);

        // Assert
        Assert.IsType(expectedExceptionType, thrown);
        Assert.Equal(BackgroundTaskStatus.Running, stored!.Status);
        Assert.False(cancellationRequested);
        Assert.Equal(publishedBefore, harness.Publisher.Updates.Count);
    }

    [Fact]
    public async Task CancelAsync_RunningTaskThatStoppedReportingProgress_CancelsImmediately()
    {
        // Arrange
        var task = NewTask(canCancel: true);
        await Tasks.AddAsync(task);
        await Tasks.UpdateAsync(task.Id, current =>
        {
            current.Start(_time.GetUtcNow().UtcDateTime);
            return true;
        });
        _time.Advance(BackgroundTaskService.StaleAfter + TimeSpan.FromMinutes(1));

        // Act
        var result = await Tasks.CancelAsync(task.Id);

        // Assert
        Assert.Equal(BackgroundTaskStatus.Cancelled, result.Task!.Status);
        Assert.Empty(await Tasks.GetActiveIdsAsync(OrganizationId));
    }

    [Fact]
    public async Task CancelAsync_TaskThatCannotBeCancelled_IsRejectedAndLeftRunning()
    {
        // Arrange
        var task = NewTask(canCancel: false);
        await Tasks.AddAsync(task);

        // Act
        var result = await Tasks.CancelAsync(task.Id);

        // Assert
        Assert.True(result.IsNotCancellable);
        Assert.Equal(BackgroundTaskStatus.Queued, (await Tasks.GetAsync(task.Id))!.Status);
        Assert.False(await Tasks.IsCancelRequestedAsync(task.Id));
        Assert.Empty(_harness.Publisher.Updates);
    }

    [Fact]
    public async Task CancelAsync_AbandonedTaskThatCannotBeCancelled_CanStillBeCleared()
    {
        // Arrange
        var task = NewTask(canCancel: false);
        await Tasks.AddAsync(task);
        _time.Advance(BackgroundTaskService.StaleAfter + TimeSpan.FromMinutes(1));

        // Act
        var result = await Tasks.CancelAsync(task.Id);

        // Assert
        Assert.False(result.IsNotCancellable);
        Assert.Equal(BackgroundTaskStatus.Cancelled, result.Task!.Status);
    }

    [Fact]
    public async Task CancelAsync_AbandonedRunningTask_SaysItStoppedResponding()
    {
        // Arrange
        var task = NewTask(canCancel: false);
        await Tasks.AddAsync(task);
        await Tasks.UpdateAsync(task.Id, current =>
        {
            current.Start(_time.GetUtcNow().UtcDateTime);
            return true;
        });
        _time.Advance(BackgroundTaskService.StaleAfter + TimeSpan.FromMinutes(1));

        // Act
        var result = await Tasks.CancelAsync(task.Id);

        // Assert
        Assert.Equal(BackgroundTaskStatus.Cancelled, result.Task!.Status);
        Assert.Equal("Cancelled after it stopped responding.", result.Task.Message);
    }

    [Fact]
    public async Task CancelAsync_FinishedOrUnknownTask_ChangesNothing()
    {
        // Arrange
        var task = NewTask(canCancel: true);
        await Tasks.AddAsync(task);
        await Tasks.UpdateAsync(task.Id, current =>
        {
            current.Finish(BackgroundTaskStatus.Completed, "Done", _time.GetUtcNow().UtcDateTime);
            return true;
        });
        int publishedBefore = _harness.Publisher.Updates.Count;

        // Act
        var finished = await Tasks.CancelAsync(task.Id);
        var unknown = await Tasks.CancelAsync(ObjectId.GenerateNewId().ToString());

        // Assert
        Assert.Equal(BackgroundTaskStatus.Completed, finished.Task!.Status);
        Assert.False(await Tasks.IsCancelRequestedAsync(task.Id));
        Assert.Null(unknown.Task);
        Assert.Equal(publishedBefore, _harness.Publisher.Updates.Count);
    }

    [Fact]
    public async Task CreateAndEnqueueAsync_Success_QueuesTheWorkItemWithTheTaskIdAndPublishesQueued()
    {
        // Arrange
        var task = NewTask();

        // Act
        var created = await Tasks.CreateAndEnqueueAsync(task, t => new ResetProjectDataWorkItem { OrganizationId = OrganizationId, ProjectId = "p1", TaskId = t.Id });
        var entry = await _harness.Queue.DequeueAsync(TestCancellationToken);

        // Assert
        Assert.Same(task, created);
        var workItem = Assert.IsType<ResetProjectDataWorkItem>(_harness.Queue.Serializer.Deserialize(entry!.Value.Data, typeof(ResetProjectDataWorkItem)));
        Assert.Equal(task.Id, workItem.TaskId);
        var update = Assert.Single(_harness.Publisher.Updates);
        Assert.Equal(BackgroundTaskStatus.Queued, update.Status);
        Assert.Equal(task.Id, update.Id);
    }

    [Fact]
    public async Task CreateAndEnqueueAsync_WorkItemFactoryThrows_FailsTheTaskWithoutQueueingIt()
    {
        // Arrange
        var task = NewTask();
        var failure = new InvalidOperationException("Unable to prepare the work item");

        // Act
        var thrown = await Record.ExceptionAsync(async () => await Tasks.CreateAndEnqueueAsync<ResetProjectDataWorkItem>(task, _ => throw failure));
        var stored = await Tasks.GetAsync(task.Id);
        var active = await Tasks.GetActiveIdsAsync(OrganizationId);
        var queued = await _harness.Queue.GetQueueStatsAsync();

        // Assert
        Assert.Same(failure, thrown);
        Assert.NotNull(stored);
        Assert.Equal(BackgroundTaskStatus.Failed, stored.Status);
        Assert.NotNull(stored.CompletedUtc);
        Assert.Empty(active);
        Assert.Equal(0, queued.Enqueued);
        Assert.Equal(BackgroundTaskStatus.Failed, Assert.Single(_harness.Publisher.Updates).Status);
    }

    [Fact]
    public async Task EnqueueAsync_QueueRejectsTheWorkItem_FailsTheTaskSoItIsNotLeftWaiting()
    {
        // Arrange
        var queue = new Mock<IQueue<WorkItemData>>();
        queue.SetupGet(q => q.Serializer).Returns(_harness.Queue.Serializer);
        queue.Setup(q => q.EnqueueAsync(It.IsAny<WorkItemData>(), It.IsAny<QueueEntryOptions>())).ThrowsAsync(new TimeoutException("Queue unavailable"));
        using var harness = new BackgroundTaskTestHarness(_time, Log, queue: queue.Object);
        var task = NewTask();

        // Act
        var thrown = await Record.ExceptionAsync(async () => await harness.Tasks.CreateAndEnqueueAsync(task, t => new ResetProjectDataWorkItem { OrganizationId = OrganizationId, ProjectId = "p1", TaskId = t.Id }));

        // Assert
        Assert.IsType<TimeoutException>(thrown);
        var stored = await harness.Tasks.GetAsync(task.Id);
        Assert.Equal(BackgroundTaskStatus.Failed, stored!.Status);
        Assert.NotNull(stored.CompletedUtc);
        Assert.Empty(await harness.Tasks.GetActiveIdsAsync(OrganizationId));
        Assert.Equal(BackgroundTaskStatus.Failed, Assert.Single(harness.Publisher.Updates).Status);
    }

    [Theory]
    [InlineData(BackgroundTaskStatus.Running, 40, 10)]
    [InlineData(BackgroundTaskStatus.Completed, 100, 4)]
    [InlineData(BackgroundTaskStatus.Cancelled, 40, 10)]
    public async Task EnqueueAsync_AcknowledgementFailsAfterTaskAdvances_PreservesTheStoredState(BackgroundTaskStatus status, int expectedProgress, int expectedTotal)
    {
        // Arrange
        var queue = new LostAcknowledgementQueue(new InMemoryQueueOptions<WorkItemData> { TimeProvider = _time, LoggerFactory = Log });
        using var harness = new BackgroundTaskTestHarness(_time, Log, queue: queue);
        var task = NewTask(canCancel: true);
        task.Type = DeleteEventsBulkOperationHandler.OperationType;
        task.Total = 10;
        task.Bulk = new BulkOperationDetails
        {
            Selection = new BulkOperationSelection
            {
                Kind = BulkOperationSelectionKind.Ids,
                Ids = Enumerable.Range(0, 10).Select(_ => ObjectId.GenerateNewId().ToString()).ToList()
            }
        };
        queue.AfterEnqueue = async () =>
        {
            await harness.Tasks.UpdateAsync(task.Id, current =>
            {
                current.Start(_time.GetUtcNow().UtcDateTime);
                current.Progress = 40;
                current.Processed = 4;
                current.Succeeded = 3;
                current.Failed = 1;
                current.Bulk!.Cursor = "4";
                current.Message = "Processed four events";
                if (status is BackgroundTaskStatus.Completed)
                    current.Total = current.Processed;

                if (status is not BackgroundTaskStatus.Running)
                    current.Finish(status, current.Message, _time.GetUtcNow().UtcDateTime);

                return true;
            });
        };

        // Act
        var thrown = await Record.ExceptionAsync(async () => await harness.Tasks.CreateAndEnqueueAsync(task,
            current => new BulkOperationWorkItem { OrganizationId = OrganizationId, TaskId = current.Id, Type = current.Type }));
        var stored = await harness.Tasks.GetAsync(task.Id);
        var active = await harness.Tasks.GetActiveIdsAsync(OrganizationId);
        var queued = await queue.GetQueueStatsAsync();

        // Assert
        Assert.IsType<TimeoutException>(thrown);
        Assert.NotNull(stored);
        Assert.Equal(status, stored.Status);
        Assert.Equal(expectedProgress, stored.Progress);
        Assert.Equal(expectedTotal, stored.Total);
        Assert.Equal(4, stored.Processed);
        Assert.Equal(3, stored.Succeeded);
        Assert.Equal(1, stored.Failed);
        Assert.Equal("4", stored.Bulk!.Cursor);
        Assert.Equal("Processed four events", stored.Message);
        Assert.Equal(status is BackgroundTaskStatus.Running, active.Contains(task.Id));
        Assert.Equal(1, queued.Enqueued);
        Assert.Equal(status, Assert.Single(harness.Publisher.Updates).Status);
    }

    [Fact]
    public void CacheKeysAndLockResources_ShareThePrefixAndAreNamedByKind()
    {
        // Arrange
        string[] cacheKeys =
        [
            BackgroundTaskService.GetTaskCacheKey("a"),
            BackgroundTaskService.GetCancelCacheKey("a"),
            BackgroundTaskService.GetOrganizationIndexCacheKey("o"),
            BackgroundTaskService.GetActiveIndexCacheKey("o"),
            BulkOperationService_GetPinnedSelectionCacheKey("a")
        ];
        string[] lockResources = [BackgroundTaskService.GetUpdateLockResource("a"), BackgroundTaskService.GetCreateLockResource("o")];

        // Act
        var all = cacheKeys.Concat(lockResources).ToList();

        // Assert
        Assert.All(all, key => Assert.StartsWith(BackgroundTaskService.KeyPrefix + ":", key));
        Assert.Equal(all.Count, all.Distinct().Count());
    }

    private static string BulkOperationService_GetPinnedSelectionCacheKey(string id) => Exceptionless.Core.Services.BulkOperations.BulkOperationService.GetPinnedSelectionCacheKey(id);

    private static BackgroundTask NewTask(string organizationId = OrganizationId, bool canCancel = false)
    {
        return new BackgroundTask
        {
            OrganizationId = organizationId,
            ProjectId = "537650f3b77efe23a47914f6",
            Type = BackgroundTaskTypes.ResetProjectData,
            Description = "Reset data for Test",
            CreatedByUserId = "537650f3b77efe23a47914f8",
            CanCancel = canCancel
        };
    }

    private sealed class ObservingHistoryCacheClient(ICacheClient inner) : ScopedCacheClient(inner, null), ICacheClient
    {
        public Func<Task>? BeforeHistoryRefresh { get; set; }

        public new async Task<long> ListAddAsync<T>(string key, IEnumerable<T> values, TimeSpan? expiresIn = null) where T : notnull
        {
            if (key == BackgroundTaskService.GetOrganizationIndexCacheKey(OrganizationId) && BeforeHistoryRefresh is { } observe)
            {
                BeforeHistoryRefresh = null;
                await observe();
            }

            return await base.ListAddAsync(key, values, expiresIn);
        }
    }

    private sealed class LostAcknowledgementQueue(InMemoryQueueOptions<WorkItemData> options) : InMemoryQueue<WorkItemData>(options)
    {
        public Func<Task>? AfterEnqueue { get; set; }

        protected override async Task<string?> EnqueueImplAsync(WorkItemData data, QueueEntryOptions options)
        {
            await base.EnqueueImplAsync(data, options);
            if (AfterEnqueue is { } afterEnqueue)
                await afterEnqueue();

            throw new TimeoutException("The work item was queued but its acknowledgement was lost");
        }
    }

    private sealed class CountingLockProvider
    {
        private int _attempts;

        public int Attempts => Volatile.Read(ref _attempts);

        /// <summary>Called with the attempt number (since the last reset) as an attempt to acquire a lock starts.</summary>
        public Action<int>? OnAttempt { get; set; }

        public void Reset() => Volatile.Write(ref _attempts, 0);

        public ILockProvider Wrap(ILockProvider inner)
        {
            var mock = new Mock<ILockProvider>();
            mock.Setup(p => p.AcquireAsync(It.IsAny<string>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns<string, TimeSpan?, bool, CancellationToken>(async (resource, expires, releaseOnDispose, cancellationToken) =>
                {
                    int attempt = Interlocked.Increment(ref _attempts);
                    OnAttempt?.Invoke(attempt);
                    return await inner.AcquireAsync(resource, expires, releaseOnDispose, cancellationToken);
                });
            return mock.Object;
        }
    }
}
