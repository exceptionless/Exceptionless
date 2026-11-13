using Exceptionless.Core.Models;
using Exceptionless.Core.Models.BackgroundTasks;
using Exceptionless.Core.Models.BulkOperations;
using Exceptionless.Core.Services.BulkOperations;
using Exceptionless.Tests.Utility;
using Foundatio.Jobs;
using Foundatio.Queues;
using Foundatio.Xunit;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Exceptionless.Tests.Services.BulkOperations;

public class BulkSelectionResourceTests(ITestOutputHelper output) : TestWithLoggingBase(output)
{
    private const string OrganizationId = "537650f3b77efe23a47914f4";
    private readonly FakeTimeProvider _time = new();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CreateAsync_ConflictingSelection_ReleasesOnlyTheRejectedSelectionWithoutChangingTheResult(bool cleanupFails)
    {
        // Arrange
        using var harness = new BackgroundTaskTestHarness(_time, Log);
        var resolver = new RecordingResolver { CleanupFails = cleanupFails };
        var service = CreateService(harness, resolver);
        var accepted = await service.CreateAsync(NewTask());

        // Act
        var rejected = await service.CreateAsync(NewTask());

        // Assert
        Assert.Null(rejected.Task);
        Assert.Equal(accepted.Task!.Id, rejected.Conflict?.Id);
        var release = Assert.Single(resolver.Releases);
        Assert.Equal("selection-2", release.PinnedState);
        Assert.Equal("selection-1", await service.GetPinnedSelectionAsync(accepted.Task.Id));
        Assert.True((await harness.Tasks.GetAsync(accepted.Task.Id))!.IsActive);
    }

    [Theory]
    [InlineData(BackgroundTaskStatus.Queued, BackgroundTaskStatus.Failed, true)]
    [InlineData(BackgroundTaskStatus.Running, BackgroundTaskStatus.Running, false)]
    [InlineData(BackgroundTaskStatus.Completed, BackgroundTaskStatus.Completed, true)]
    [InlineData(BackgroundTaskStatus.Cancelled, BackgroundTaskStatus.Cancelled, true)]
    public async Task CreateAsync_EnqueueAcknowledgementIsLost_UsesSavedStateToReleaseOrRetainTheSelection(
        BackgroundTaskStatus acceptedStatus, BackgroundTaskStatus expectedStatus, bool shouldRelease)
    {
        // Arrange
        var queue = new LostAcknowledgementQueue(new InMemoryQueueOptions<WorkItemData> { TimeProvider = _time, LoggerFactory = Log });
        using var harness = new BackgroundTaskTestHarness(_time, Log, queue: queue);
        var resolver = new RecordingResolver();
        var service = CreateService(harness, resolver);
        var task = NewTask();
        queue.AfterAccept = async () =>
        {
            await harness.Tasks.UpdateAsync(task.Id, current =>
            {
                if (acceptedStatus is BackgroundTaskStatus.Queued)
                    return false;

                current.Start(_time.GetUtcNow().UtcDateTime);
                current.Bulk!.Cursor = "latest-snapshot-cursor";
                if (acceptedStatus is not BackgroundTaskStatus.Running)
                    current.Finish(acceptedStatus, "Finished before the enqueue acknowledgement.", _time.GetUtcNow().UtcDateTime);

                return true;
            });
        };

        // Act
        var thrown = await Record.ExceptionAsync(async () => await service.CreateAsync(task));
        var saved = await harness.Tasks.GetAsync(task.Id);

        // Assert
        Assert.IsType<TimeoutException>(thrown);
        Assert.NotNull(saved);
        Assert.Equal(expectedStatus, saved.Status);
        Assert.Equal(shouldRelease ? 1 : 0, resolver.Releases.Count);
        Assert.All(resolver.Releases, release =>
        {
            Assert.Equal("selection-1", release.PinnedState);
            Assert.Equal(saved.Bulk!.Cursor, release.Cursor);
        });
        Assert.Equal("selection-1", await service.GetPinnedSelectionAsync(task.Id));
        Assert.Equal(1, (await queue.GetQueueStatsAsync()).Enqueued);
    }

    private BulkOperationService CreateService(BackgroundTaskTestHarness harness, RecordingResolver resolver) =>
        new(harness.Tasks, harness.Cache, harness.LockProvider, [new DeleteHandler()], [resolver], _time);

    private static BackgroundTask NewTask() => new()
    {
        OrganizationId = OrganizationId,
        ProjectId = "537650f3b77efe23a47914f6",
        Type = "test.delete",
        Bulk = new BulkOperationDetails { Selection = new BulkOperationSelection { Kind = BulkOperationSelectionKind.Filter } }
    };

    private sealed class RecordingResolver : IBulkSelectionResolver
    {
        private int _prepared;
        public bool CleanupFails { get; init; }
        public List<(string? PinnedState, string? Cursor)> Releases { get; } = [];
        public BulkOperationEntity Entity => BulkOperationEntity.Events;
        public Task<BulkOperationCheckResult> ValidateSelectionAsync(Organization organization, BulkOperationSelection selection) => Task.FromResult(BulkOperationCheckResult.Ok);

        public Task<BulkSelectionPreparation> PrepareAsync(BackgroundTask task, CancellationToken cancellationToken) =>
            Task.FromResult(new BulkSelectionPreparation(BulkOperationCheckResult.Ok, 1, $"selection-{++_prepared}"));

        public Task<BulkSelectionPage> GetPageAsync(BackgroundTask task, string? pinnedState, string? cursor, int size, CancellationToken cancellationToken) =>
            throw new NotSupportedException("These tests do not execute the queued selection.");

        public Task ReleaseAsync(BackgroundTask task, string? pinnedState, string? cursor)
        {
            Releases.Add((pinnedState, cursor));
            return CleanupFails ? Task.FromException(new TimeoutException("Snapshot cleanup unavailable")) : Task.CompletedTask;
        }
    }

    private sealed class DeleteHandler : IBulkOperationHandler
    {
        public string Type => "test.delete";
        public BulkOperationEntity Entity => BulkOperationEntity.Events;
        public bool IsDestructive => true;
        public Task<BulkOperationCheckResult> AuthorizeAsync(BulkOperationRequest request) => Task.FromResult(BulkOperationCheckResult.Ok);
        public Task<BulkOperationCheckResult> ValidateAsync(BulkOperationRequest request) => Task.FromResult(BulkOperationCheckResult.Ok);
        public string Describe(BackgroundTask task) => "Delete selected events";
        public Task<BulkBatchResult> ExecuteBatchAsync(BackgroundTask task, IReadOnlyCollection<string> ids, CancellationToken cancellationToken) =>
            throw new NotSupportedException("These tests do not execute the queued selection.");
    }

    private sealed class LostAcknowledgementQueue(InMemoryQueueOptions<WorkItemData> options) : InMemoryQueue<WorkItemData>(options)
    {
        public Func<Task>? AfterAccept { get; set; }

        protected override async Task<string?> EnqueueImplAsync(WorkItemData data, QueueEntryOptions options)
        {
            await base.EnqueueImplAsync(data, options);
            if (AfterAccept is not null)
                await AfterAccept();
            throw new TimeoutException("The queue accepted the work but its acknowledgement was lost.");
        }
    }
}
