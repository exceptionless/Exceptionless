using Exceptionless.Core.Jobs.WorkItemHandlers;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.BackgroundTasks;
using Exceptionless.Core.Models.BulkOperations;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Services.BackgroundTasks;
using Exceptionless.Core.Services.BulkOperations;
using Exceptionless.Tests.Utility;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Queues;
using Foundatio.Utility;
using Foundatio.Xunit;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Exceptionless.Tests.Jobs.WorkItemHandlers;

public class BulkOperationWorkItemHandlerTests(ITestOutputHelper output) : TestWithLoggingBase(output)
{
    private const string OrganizationId = "537650f3b77efe23a47914f4";
    private const string ProjectId = "537650f3b77efe23a47914f6";
    private readonly FakeTimeProvider _time = new();

    [Theory]
    [InlineData(1, BackgroundTaskStatus.Completed, 1)]
    [InlineData(3, BackgroundTaskStatus.Failed, 0)]
    public async Task HandleItemAsync_StartupTemporarilyFails_UsesBoundedContinuationWithoutAbandoningAnActiveTask(int startupFailures, BackgroundTaskStatus expectedStatus, int expectedBatches)
    {
        // Arrange
        var queue = new InMemoryQueue<WorkItemData>(new InMemoryQueueOptions<WorkItemData>
        {
            TimeProvider = _time,
            LoggerFactory = Log,
            Retries = 2,
            RetryDelay = TimeSpan.Zero
        });
        var locks = new FaultingLockProvider();
        using var harness = new BackgroundTaskTestHarness(_time, Log, decorateLockProvider: locks.Wrap, queue: queue, lockAttempts: 1);
        using var subscription = queue.Dequeued.AddSyncHandler((_, _) =>
        {
            if (startupFailures-- <= 0)
                return;

            locks.AcquireFailuresRemaining = 1;
            locks.NextAcquireFailure = new TimeoutException("Task startup temporarily unavailable");
        });
        int batches = 0;
        var operationHandler = new DelegateBulkHandler(batch =>
        {
            batches++;
            return Task.FromResult(new BulkBatchResult(batch.Count, 0));
        });
        var resolver = new RemainingIdsResolver([]);
        var service = CreateService(harness, operationHandler, resolver);
        var created = await service.CreateAsync(NewTask(["first"]));
        var job = CreateJob(harness, service, resolver, batchSize: 1);

        // Act
        await job.RunUntilEmptyAsync(TestCancellationToken);
        var stored = await harness.Tasks.GetAsync(created.Task!.Id);
        var stats = await queue.GetQueueStatsAsync();

        // Assert
        Assert.NotNull(stored);
        Assert.Equal(expectedStatus, stored.Status);
        Assert.Equal(expectedBatches, batches);
        Assert.Equal(expectedBatches, stored.Processed);
        Assert.Equal(expectedBatches, stored.Succeeded);
        Assert.Equal(0, stored.Failed);
        Assert.Equal(0, stats.Abandoned);
        Assert.Equal(0, stats.Deadletter);
        Assert.Empty(await harness.Tasks.GetActiveIdsAsync(OrganizationId));
    }

    [Theory]
    [InlineData(BulkOperationSelectionKind.Ids, false)]
    [InlineData(BulkOperationSelectionKind.Filter, false)]
    [InlineData(BulkOperationSelectionKind.Ids, true)]
    [InlineData(BulkOperationSelectionKind.Filter, true)]
    public async Task HandleItemAsync_CompletedBatchCheckpointTemporarilyFails_PreservesItsResult(BulkOperationSelectionKind kind, bool checkpointCommitted)
    {
        // Arrange
        string[] ids = ["first", "second", "third"];
        var remaining = ids.ToHashSet(StringComparer.Ordinal);
        var locks = new FaultingLockProvider();
        using var harness = new BackgroundTaskTestHarness(_time, Log, decorateLockProvider: locks.Wrap, lockAttempts: 1);
        var executedIds = new List<string>();
        int batches = 0;
        var operationHandler = new DelegateBulkHandler(batch =>
        {
            executedIds.AddRange(batch);
            int succeeded = batch.Count(remaining.Remove);
            if (++batches == 1)
            {
                if (checkpointCommitted)
                    locks.NextReleaseFailure = new TimeoutException("Checkpoint saved but lock release failed");
                else
                    locks.NextAcquireFailure = new TimeoutException("Checkpoint temporarily unavailable");
            }

            return Task.FromResult(new BulkBatchResult(succeeded, batch.Count - succeeded));
        });
        var resolver = new RemainingIdsResolver(remaining);
        var service = CreateService(harness, operationHandler, resolver);
        var created = await service.CreateAsync(NewTask(ids, kind));
        var job = CreateJob(harness, service, resolver, batchSize: 2);

        // Act
        await job.RunUntilEmptyAsync(TestCancellationToken);
        var stored = await harness.Tasks.GetAsync(created.Task!.Id);

        // Assert
        Assert.Empty(remaining);
        Assert.NotNull(stored);
        Assert.Equal(BackgroundTaskStatus.Completed, stored.Status);
        Assert.Equal(ids.Length, stored.Processed);
        Assert.Equal(ids.Length, stored.Succeeded);
        Assert.Equal(0, stored.Failed);
        Assert.Equal(ids, executedIds);
    }

    [Fact]
    public async Task HandleItemAsync_HostStopsAfterSuccessfulBatch_PersistsItsResultBeforeStopping()
    {
        // Arrange
        string[] ids = ["first", "second", "third"];
        var remaining = ids.ToHashSet(StringComparer.Ordinal);
        using var shutdown = new CancellationTokenSource();
        var locks = new FaultingLockProvider();
        using var harness = new BackgroundTaskTestHarness(_time, Log, decorateLockProvider: locks.Wrap, lockAttempts: 1);
        int batches = 0;
        var executedIds = new List<string>();
        var operationHandler = new DelegateBulkHandler(batch =>
        {
            executedIds.AddRange(batch);
            int succeeded = batch.Count(remaining.Remove);
            if (++batches == 1)
            {
                locks.NextAcquireFailure = new TimeoutException("Checkpoint temporarily unavailable");
                shutdown.Cancel();
            }

            return Task.FromResult(new BulkBatchResult(succeeded, batch.Count - succeeded));
        });
        var resolver = new RemainingIdsResolver(remaining);
        var service = CreateService(harness, operationHandler, resolver);
        var created = await service.CreateAsync(NewTask(ids));
        var handler = CreateHandler(harness, service, resolver, batchSize: 2);
        var interruptedContext = CreateContext(created.Task!.Id, shutdown.Token);

        // Act
        await handler.HandleItemAsync(interruptedContext);
        var interrupted = await harness.Tasks.GetAsync(created.Task.Id);

        // Assert
        Assert.False(interruptedContext.Result.IsSuccess);
        Assert.NotNull(interrupted);
        Assert.Equal(BackgroundTaskStatus.Running, interrupted.Status);
        Assert.Equal(2, interrupted.Processed);
        Assert.Equal(2, interrupted.Succeeded);

        // Act
        await handler.HandleItemAsync(CreateContext(created.Task.Id, TestCancellationToken));
        var completed = await harness.Tasks.GetAsync(created.Task.Id);

        // Assert
        Assert.NotNull(completed);
        Assert.Equal(BackgroundTaskStatus.Completed, completed.Status);
        Assert.Equal(ids.Length, completed.Succeeded);
        Assert.Equal(0, completed.Failed);
        Assert.Equal(ids, executedIds);
        Assert.Empty(remaining);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HandleItemAsync_CompletedBatchCheckpointKeepsFailing_StopsWithoutReplayingOrDiscardingPriorProgress(bool dependencyCancelled)
    {
        // Arrange
        string[] ids = ["first", "second", "third", "fourth", "fifth"];
        var remaining = ids.ToHashSet(StringComparer.Ordinal);
        var locks = new FaultingLockProvider();
        using var harness = new BackgroundTaskTestHarness(_time, Log, decorateLockProvider: locks.Wrap, lockAttempts: 1);
        int batches = 0;
        var operationHandler = new DelegateBulkHandler(batch =>
        {
            int succeeded = batch.Count(remaining.Remove);
            if (++batches == 2)
            {
                locks.AcquireFailuresRemaining = BulkOperationWorkItemHandler.MaxTransientFailures;
                locks.NextAcquireFailure = dependencyCancelled
                    ? new TaskCanceledException("Checkpoint cancelled by dependency")
                    : new TimeoutException("Checkpoint unavailable for every attempt");
            }

            return Task.FromResult(new BulkBatchResult(succeeded, batch.Count - succeeded));
        });
        var resolver = new RemainingIdsResolver(remaining);
        var service = CreateService(harness, operationHandler, resolver);
        var created = await service.CreateAsync(NewTask(ids));
        var job = CreateJob(harness, service, resolver, batchSize: 2);

        // Act
        await job.RunUntilEmptyAsync(TestCancellationToken);
        var stored = await harness.Tasks.GetAsync(created.Task!.Id);
        var stats = await harness.Queue.GetQueueStatsAsync();

        // Assert
        Assert.NotNull(stored);
        Assert.Equal(BackgroundTaskStatus.Failed, stored.Status);
        Assert.Equal(ids.Length, stored.Total);
        Assert.Equal(2, stored.Processed);
        Assert.Equal(2, stored.Succeeded);
        Assert.Equal(0, stored.Failed);
        Assert.Contains("Some items may have been processed", stored.Message);
        Assert.Equal(["fifth"], remaining);
        Assert.Equal(2, batches);
        Assert.Equal(1, stats.Enqueued);
        Assert.Equal(1, stats.Completed);
        Assert.Empty(await harness.Tasks.GetActiveIdsAsync(OrganizationId));
    }

    [Theory]
    [InlineData(BatchFailure.None)]
    [InlineData(BatchFailure.Temporary)]
    [InlineData(BatchFailure.Permanent)]
    public async Task HandleItemAsync_TaskRecordDisappearsDuringBatch_PublishesLostAndStops(BatchFailure failure)
    {
        // Arrange
        using var harness = new BackgroundTaskTestHarness(_time, Log);
        var task = NewTask(["first", "second"]);
        int batches = 0;
        var operationHandler = new DelegateBulkHandler(async batch =>
        {
            batches++;
            await harness.Cache.RemoveAsync(BackgroundTaskService.GetTaskCacheKey(task.Id));
            if (failure is BatchFailure.Temporary)
                throw new TimeoutException("Batch interrupted after the task record was lost");
            if (failure is BatchFailure.Permanent)
                throw new InvalidOperationException("Batch rejected after the task record was lost");

            return new BulkBatchResult(batch.Count, 0);
        });
        var resolver = new RemainingIdsResolver([]);
        var service = CreateService(harness, operationHandler, resolver);
        await service.CreateAsync(task);
        var job = CreateJob(harness, service, resolver, batchSize: 1);

        // Act
        await job.RunUntilEmptyAsync(TestCancellationToken);
        var stored = await harness.Tasks.GetAsync(task.Id);

        // Assert
        Assert.Null(stored);
        Assert.Equal(1, batches);
        var lost = Assert.Single(harness.Publisher.Updates, update => update.Status is BackgroundTaskStatus.Failed);
        Assert.Equal(task.Id, lost.Id);
        Assert.Equal(task.OrganizationId, lost.OrganizationId);
        Assert.Equal(task.Type, lost.Type);
        Assert.Equal(task.Description, lost.Description);
        Assert.Contains("progress was lost", lost.Message);
        Assert.Equal(1, (await harness.Queue.GetQueueStatsAsync()).Completed);
    }

    [Fact]
    public async Task HandleItemAsync_TaskRecordDisappearsAfterFinalCheckpoint_PublishesLostInsteadOfCompletion()
    {
        // Arrange
        using var harness = new BackgroundTaskTestHarness(_time, Log);
        var task = NewTask(["first"]);
        int batches = 0;
        var operationHandler = new DelegateBulkHandler(batch =>
        {
            batches++;
            return Task.FromResult(new BulkBatchResult(batch.Count, 0));
        });
        var resolver = new RemainingIdsResolver([]);
        var service = CreateService(harness, operationHandler, resolver);
        await service.CreateAsync(task);
        using var subscription = harness.Queue.LockRenewed.AddHandler(async (_, _) =>
        {
            var checkpoint = await harness.Tasks.GetAsync(task.Id);
            if (checkpoint is { Processed: 1 })
                await harness.Cache.RemoveAsync(BackgroundTaskService.GetTaskCacheKey(task.Id));
        });
        var job = CreateJob(harness, service, resolver, batchSize: 1);

        // Act
        await job.RunUntilEmptyAsync(TestCancellationToken);
        var stored = await harness.Tasks.GetAsync(task.Id);

        // Assert
        Assert.Null(stored);
        Assert.Equal(1, batches);
        Assert.Single(harness.Publisher.Updates, update => update.Status is BackgroundTaskStatus.Failed);
        Assert.DoesNotContain(harness.Publisher.Updates, update => update.Status is BackgroundTaskStatus.Completed);
        Assert.Equal(1, (await harness.Queue.GetQueueStatsAsync()).Completed);
    }

    [Fact]
    public async Task HandleItemAsync_ContinuationCannotBeQueued_DoesNotLeaveAnActiveTaskWithoutWork()
    {
        // Arrange
        var queue = new FaultingEnqueueQueue(new InMemoryQueueOptions<WorkItemData>
        {
            TimeProvider = _time,
            LoggerFactory = Log,
            Retries = 2,
            RetryDelay = TimeSpan.Zero
        });
        using var harness = new BackgroundTaskTestHarness(_time, Log, queue: queue);
        int calls = 0;
        var operationHandler = new DelegateBulkHandler(batch => ++calls % 2 == 0
            ? Task.FromException<BulkBatchResult>(new TimeoutException("Search temporarily unavailable"))
            : Task.FromResult(new BulkBatchResult(batch.Count, 0)));
        var resolver = new RemainingIdsResolver([]);
        var service = CreateService(harness, operationHandler, resolver);
        var created = await service.CreateAsync(NewTask(["first", "second", "third", "fourth"]));
        var job = CreateJob(harness, service, resolver, batchSize: 1);
        queue.FailEnqueue = true;

        // Act
        await job.RunUntilEmptyAsync(TestCancellationToken);
        var stored = await harness.Tasks.GetAsync(created.Task!.Id);
        var stats = await queue.GetQueueStatsAsync();
        var active = await harness.Tasks.GetActiveIdsAsync(OrganizationId);

        // Assert
        Assert.Equal(0, stats.Queued);
        Assert.Equal(0, stats.Working);
        Assert.Equal(0, stats.Deadletter);
        Assert.NotNull(stored);
        Assert.Equal(BackgroundTaskStatus.Failed, stored.Status);
        Assert.Equal(4, stored.Total);
        Assert.Equal(1, stored.Processed);
        Assert.Equal(1, stored.Succeeded);
        Assert.Equal(0, stored.Failed);
        Assert.DoesNotContain(stored.Id, active);
    }

    [Fact]
    public async Task HandleItemAsync_HostStopsBeforeContinuation_KeepsTheTaskAvailableForQueueRetry()
    {
        // Arrange
        using var shutdown = new CancellationTokenSource();
        using var harness = new BackgroundTaskTestHarness(_time, Log);
        var operationHandler = new DelegateBulkHandler(_ =>
        {
            shutdown.Cancel();
            return Task.FromException<BulkBatchResult>(new TimeoutException("Search interrupted during shutdown"));
        });
        var resolver = new RemainingIdsResolver([]);
        var service = CreateService(harness, operationHandler, resolver);
        var created = await service.CreateAsync(NewTask(["first"]));
        var handler = CreateHandler(harness, service, resolver, batchSize: 1, retryDelay: TimeSpan.FromSeconds(5));
        var context = CreateContext(created.Task!.Id, shutdown.Token);

        // Act
        var thrown = await Record.ExceptionAsync(() => handler.HandleItemAsync(context));
        var stored = await harness.Tasks.GetAsync(created.Task.Id);
        var stats = await harness.Queue.GetQueueStatsAsync();

        // Assert
        Assert.Null(thrown);
        Assert.False(context.Result.IsSuccess);
        Assert.NotNull(stored);
        Assert.Equal(BackgroundTaskStatus.Running, stored.Status);
        Assert.Null(stored.CompletedUtc);
        Assert.Equal(1, stored.Bulk!.RetryCount);
        Assert.Contains(stored.Id, await harness.Tasks.GetActiveIdsAsync(OrganizationId));
        Assert.Equal(1, stats.Enqueued);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task HandleItemAsync_SelectionIsRejected_FailsWithoutLosingCompletedCountsOrRetrying(int successfulBatches)
    {
        // Arrange
        const string message = "Please upgrade your plan to use premium search features.";
        string[] ids = ["first", "second", "third"];
        var remaining = ids.ToHashSet(StringComparer.Ordinal);
        using var harness = new BackgroundTaskTestHarness(_time, Log);
        var operationHandler = new DelegateBulkHandler(batch => Task.FromResult(new BulkBatchResult(batch.Count(remaining.Remove), 0)));
        var resolver = new RemainingIdsResolver(remaining)
        {
            Failure = call => call == successfulBatches + 1 ? new BulkSelectionRejectedException(BulkOperationCheckResult.PlanLimit(message)) : null
        };
        var service = CreateService(harness, operationHandler, resolver);
        var created = await service.CreateAsync(NewTask(ids, BulkOperationSelectionKind.Filter));
        var job = CreateJob(harness, service, resolver, batchSize: 1);

        // Act
        await job.RunUntilEmptyAsync(TestCancellationToken);
        var stored = await harness.Tasks.GetAsync(created.Task!.Id);
        var stats = await harness.Queue.GetQueueStatsAsync();

        // Assert
        Assert.NotNull(stored);
        Assert.Equal(BackgroundTaskStatus.Failed, stored.Status);
        Assert.Equal(message, stored.Message);
        Assert.Equal(ids.Length, stored.Total);
        Assert.Equal(successfulBatches, stored.Processed);
        Assert.Equal(successfulBatches, stored.Succeeded);
        Assert.Equal(0, stored.Failed);
        Assert.Equal(0, stored.Bulk!.RetryCount);
        Assert.Equal(ids.Length - successfulBatches, remaining.Count);
        Assert.Equal(1, stats.Enqueued);
        Assert.Equal(1, stats.Completed);
        Assert.Empty(await harness.Tasks.GetActiveIdsAsync(OrganizationId));
    }

    private BulkOperationService CreateService(BackgroundTaskTestHarness harness, IBulkOperationHandler handler, IBulkSelectionResolver resolver)
    {
        return new BulkOperationService(harness.Tasks, harness.Cache, harness.LockProvider, [handler], [resolver], _time);
    }

    private WorkItemJob CreateJob(BackgroundTaskTestHarness harness, BulkOperationService service, IBulkSelectionResolver resolver, int batchSize)
    {
        var handler = CreateHandler(harness, service, resolver, batchSize);
        var handlers = new Foundatio.Jobs.WorkItemHandlers();
        handlers.Register<BulkOperationWorkItem>(() => handler);
        return new WorkItemJob(harness.Queue, harness.Publisher, handlers, Log);
    }

    private BulkOperationWorkItemHandler CreateHandler(BackgroundTaskTestHarness harness, BulkOperationService service, IBulkSelectionResolver resolver, int batchSize, TimeSpan? retryDelay = null)
    {
        return new BulkOperationWorkItemHandler(service, harness.Tasks, [resolver],
            new BulkOperationOptions { BatchSize = batchSize, RetryDelay = retryDelay ?? TimeSpan.Zero }, harness.LockProvider, _time, Log);
    }

    private static WorkItemContext CreateContext(string taskId, CancellationToken cancellationToken)
    {
        var workItem = new BulkOperationWorkItem { TaskId = taskId, OrganizationId = OrganizationId };
        return new WorkItemContext(workItem, "test-job", EmptyLock.Empty, cancellationToken, static (_, _) => Task.CompletedTask);
    }

    private static BackgroundTask NewTask(IReadOnlyCollection<string> ids, BulkOperationSelectionKind kind = BulkOperationSelectionKind.Ids)
    {
        return new BackgroundTask
        {
            OrganizationId = OrganizationId,
            ProjectId = ProjectId,
            Type = "test.delete",
            Description = "Delete selected items",
            CreatedByUserId = "537650f3b77efe23a47914f8",
            Bulk = new BulkOperationDetails
            {
                Selection = new BulkOperationSelection { Kind = kind, Ids = kind is BulkOperationSelectionKind.Ids ? ids.ToList() : null }
            }
        };
    }

    private sealed class DelegateBulkHandler(Func<IReadOnlyCollection<string>, Task<BulkBatchResult>> execute) : IBulkOperationHandler
    {
        public string Type => "test.delete";
        public BulkOperationEntity Entity => BulkOperationEntity.Events;
        public bool IsDestructive => true;
        public Task<BulkOperationCheckResult> AuthorizeAsync(BulkOperationRequest request) => Task.FromResult(BulkOperationCheckResult.Ok);
        public Task<BulkOperationCheckResult> ValidateAsync(BulkOperationRequest request) => Task.FromResult(BulkOperationCheckResult.Ok);
        public string Describe(BackgroundTask task) => "Delete selected items";

        public Task<BulkBatchResult> ExecuteBatchAsync(BackgroundTask task, IReadOnlyCollection<string> ids, CancellationToken cancellationToken) => execute(ids);
    }

    private sealed class RemainingIdsResolver(HashSet<string> remaining) : IBulkSelectionResolver
    {
        private int _calls;

        public Func<int, Exception?>? Failure { get; init; }
        public BulkOperationEntity Entity => BulkOperationEntity.Events;
        public Task<BulkOperationCheckResult> ValidateSelectionAsync(Organization organization, BulkOperationSelection selection) => Task.FromResult(BulkOperationCheckResult.Ok);
        public Task<BulkSelectionPreparation> PrepareAsync(BackgroundTask task, CancellationToken cancellationToken) => Task.FromResult(new BulkSelectionPreparation(BulkOperationCheckResult.Ok, remaining.Count, "{}"));

        public Task<BulkSelectionPage> GetPageAsync(BackgroundTask task, string? pinnedState, string? cursor, int size, CancellationToken cancellationToken)
        {
            if (Failure?.Invoke(++_calls) is { } failure)
                return Task.FromException<BulkSelectionPage>(failure);

            string[] ids = remaining.Order(StringComparer.Ordinal).Take(size).ToArray();
            return Task.FromResult(new BulkSelectionPage(ids, ids.LastOrDefault()));
        }
    }

    private sealed class FaultingEnqueueQueue(InMemoryQueueOptions<WorkItemData> options) : InMemoryQueue<WorkItemData>(options)
    {
        public bool FailEnqueue { get; set; }

        protected override Task<string?> EnqueueImplAsync(WorkItemData data, QueueEntryOptions options)
        {
            return FailEnqueue
                ? Task.FromException<string?>(new TimeoutException("Continuation queue unavailable"))
                : base.EnqueueImplAsync(data, options);
        }
    }

    private sealed class FaultingLockProvider : ILockProvider
    {
        private ILockProvider _inner = null!;

        public Exception? NextAcquireFailure { get; set; }
        public Exception? NextReleaseFailure { get; set; }
        public int AcquireFailuresRemaining { get; set; } = 1;

        public ILockProvider Wrap(ILockProvider inner)
        {
            _inner = inner;
            return this;
        }

        public async Task<ILock> AcquireAsync(string resource, TimeSpan? timeUntilExpires = null, bool releaseOnDispose = true, CancellationToken cancellationToken = default)
        {
            if (NextAcquireFailure is { } failure)
            {
                if (--AcquireFailuresRemaining == 0)
                    NextAcquireFailure = null;

                throw failure;
            }

            var acquired = await _inner.AcquireAsync(resource, timeUntilExpires, releaseOnDispose, cancellationToken);
            if (NextReleaseFailure is not { } releaseFailure)
                return acquired;

            NextReleaseFailure = null;
            return new FaultingReleaseLock(acquired, releaseFailure);
        }

        public Task<ILock?> TryAcquireAsync(string resource, TimeSpan? timeUntilExpires = null, bool releaseOnDispose = true, CancellationToken cancellationToken = default) => _inner.TryAcquireAsync(resource, timeUntilExpires, releaseOnDispose, cancellationToken);
        public Task<bool> IsLockedAsync(string resource) => _inner.IsLockedAsync(resource);
        public Task ReleaseAsync(string resource, string lockId) => _inner.ReleaseAsync(resource, lockId);
        public Task ReleaseAsync(string resource) => _inner.ReleaseAsync(resource);
        public Task RenewAsync(string resource, string lockId, TimeSpan? timeUntilExpires = null) => _inner.RenewAsync(resource, lockId, timeUntilExpires);
    }

    private sealed class FaultingReleaseLock(ILock inner, Exception failure) : ILock
    {
        public string LockId => inner.LockId;
        public string Resource => inner.Resource;
        public DateTime AcquiredTimeUtc => inner.AcquiredTimeUtc;
        public TimeSpan TimeWaitedForLock => inner.TimeWaitedForLock;
        public int RenewalCount => inner.RenewalCount;
        public Task RenewAsync(TimeSpan? timeUntilExpires = null) => inner.RenewAsync(timeUntilExpires);
        public Task ReleaseAsync() => inner.ReleaseAsync();

        public async ValueTask DisposeAsync()
        {
            await inner.DisposeAsync();
            throw failure;
        }
    }

    public enum BatchFailure
    {
        None,
        Temporary,
        Permanent
    }
}
