using Exceptionless.Core.Models.BackgroundTasks;
using Exceptionless.Core.Models.BulkOperations;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Services.BackgroundTasks;
using Exceptionless.Core.Services.BulkOperations;
using Exceptionless.Tests.Utility;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Queues;
using Foundatio.Serializer;
using Foundatio.Xunit;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace Exceptionless.Tests.Services.BulkOperations;

public class BulkOperationServiceTests : TestWithLoggingBase, IDisposable
{
    private const string OrganizationId = "537650f3b77efe23a47914f4";
    private const string OtherOrganizationId = "537650f3b77efe23a47914f5";
    private const string ProjectId = "537650f3b77efe23a47914f6";
    private const string OtherProjectId = "537650f3b77efe23a47914f7";
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 3, 4, 5, 6, TimeSpan.Zero));
    private readonly BackgroundTaskTestHarness _harness;
    private readonly BulkOperationService _service;

    public BulkOperationServiceTests(ITestOutputHelper output) : base(output)
    {
        _harness = new BackgroundTaskTestHarness(_time, Log);
        _service = CreateService(_harness);
    }

    public void Dispose()
    {
        _harness.Dispose();
    }

    [Fact]
    public async Task CreateAsync_IdSelection_CountsDescribesAndQueuesAnUncancelledCancellableTask()
    {
        // Arrange
        var task = NewTask("test.destructive", ProjectId, ids: 3);

        // Act
        var result = await _service.CreateAsync(task);
        var entry = await _harness.Queue.DequeueAsync(TestCancellationToken);

        // Assert
        Assert.Same(task, result.Task);
        Assert.Equal(3, task.Total);
        Assert.Equal("Record 3", task.Description);
        Assert.True(task.CanCancel);
        Assert.Equal(BackgroundTaskStatus.Queued, task.Status);
        var workItem = Assert.IsType<BulkOperationWorkItem>(_harness.Queue.Serializer.Deserialize(entry!.Value.Data, typeof(BulkOperationWorkItem)));
        Assert.Equal(task.Id, workItem.TaskId);
        Assert.Equal(OrganizationId, workItem.OrganizationId);
        Assert.Equal(BackgroundTaskStatus.Queued, Assert.Single(_harness.Publisher.Updates).Status);
    }

    [Fact]
    public async Task CreateAsync_UnknownTypeOrMissingBulkDetails_Throws()
    {
        // Arrange
        var unknown = NewTask("test.unknown", ProjectId);
        var withoutDetails = NewTask("test.destructive", ProjectId);
        withoutDetails.Bulk = null;

        // Act
        var unknownThrown = await Record.ExceptionAsync(async () => await _service.CreateAsync(unknown));
        var detailsThrown = await Record.ExceptionAsync(async () => await _service.CreateAsync(withoutDetails));

        // Assert
        Assert.IsType<ArgumentException>(unknownThrown);
        Assert.IsType<ArgumentException>(detailsThrown);
        Assert.Empty(await _harness.Tasks.GetByOrganizationAsync(OrganizationId));
    }

    [Fact]
    public async Task CreateAsync_DestructiveTaskActiveOnTheSameProject_Conflicts()
    {
        // Arrange
        var first = await _service.CreateAsync(NewTask("test.destructive", ProjectId));

        // Act
        var second = await _service.CreateAsync(NewTask("test.destructive", ProjectId));

        // Assert
        Assert.Null(second.Task);
        Assert.Equal(first.Task!.Id, second.Conflict?.Id);
        Assert.Single(await _harness.Tasks.GetByOrganizationAsync(OrganizationId));
        Assert.Equal(1, (await _harness.Queue.GetQueueStatsAsync()).Enqueued);
    }

    [Theory]
    [InlineData(null, ProjectId)]
    [InlineData(ProjectId, null)]
    [InlineData(null, null)]
    public async Task CreateAsync_OrganizationWideAndProjectScopedDestructiveTasks_Conflict(string? activeProjectId, string? candidateProjectId)
    {
        // Arrange
        var active = await _service.CreateAsync(NewTask("test.destructive", activeProjectId));

        // Act
        var candidate = await _service.CreateAsync(NewTask("test.destructive", candidateProjectId));

        // Assert
        Assert.Null(candidate.Task);
        Assert.Equal(active.Task!.Id, candidate.Conflict?.Id);
    }

    [Fact]
    public async Task CreateAsync_DestructiveTaskActiveOnAnotherProjectOrOrganization_DoesNotConflict()
    {
        // Arrange
        await _service.CreateAsync(NewTask("test.destructive", ProjectId));

        // Act
        var otherProject = await _service.CreateAsync(NewTask("test.destructive", OtherProjectId));
        var otherOrganization = await _service.CreateAsync(NewTask("test.destructive", null, OtherOrganizationId));

        // Assert
        Assert.NotNull(otherProject.Task);
        Assert.NotNull(otherOrganization.Task);
    }

    [Fact]
    public async Task CreateAsync_NonDestructiveTasks_NeitherConflictNorBlockDestructiveOnes()
    {
        // Arrange
        var firstSafe = await _service.CreateAsync(NewTask("test.safe", ProjectId));

        // Act
        var secondSafe = await _service.CreateAsync(NewTask("test.safe", ProjectId));
        var destructive = await _service.CreateAsync(NewTask("test.destructive", ProjectId));
        var safeAfterDestructive = await _service.CreateAsync(NewTask("test.safe", ProjectId));

        // Assert
        Assert.NotNull(firstSafe.Task);
        Assert.NotNull(secondSafe.Task);
        Assert.NotNull(destructive.Task);
        Assert.NotNull(safeAfterDestructive.Task);
    }

    [Fact]
    public async Task CreateAsync_ActiveDestructiveTaskStoppedReportingProgress_IsRetiredAndReplaced()
    {
        // Arrange
        var stale = await _service.CreateAsync(NewTask("test.destructive", ProjectId));
        _time.Advance(BackgroundTaskService.StaleAfter + TimeSpan.FromMinutes(1));

        // Act
        var replacement = await _service.CreateAsync(NewTask("test.destructive", ProjectId));
        var retired = await _harness.Tasks.GetAsync(stale.Task!.Id);

        // Assert
        Assert.NotNull(replacement.Task);
        Assert.Equal(BackgroundTaskStatus.Failed, retired!.Status);
        Assert.NotNull(retired.CompletedUtc);
        Assert.Equal([replacement.Task.Id], (await _harness.Tasks.GetActiveIdsAsync(OrganizationId)).ToArray());
        Assert.Contains(_harness.Publisher.Updates, u => u.Id == stale.Task.Id && u.Status == BackgroundTaskStatus.Failed);
    }

    [Fact]
    public async Task CreateAsync_FinishedTasksStillInTheActiveIndex_AreDroppedFromIt()
    {
        // Arrange
        var finished = NewTask("test.destructive", ProjectId);
        var expired = NewTask("test.destructive", ProjectId);
        await _harness.Tasks.AddAsync(finished);
        await _harness.Tasks.AddAsync(expired);
        // Simulate an index that was not cleaned: the task finished without being removed, and another record expired.
        finished.Finish(BackgroundTaskStatus.Completed, "Done", _time.GetUtcNow().UtcDateTime);
        await _harness.Tasks.SaveAsync(finished);
        await _harness.Cache.RemoveAsync(BackgroundTaskService.GetTaskCacheKey(expired.Id));

        // Act
        var created = await _service.CreateAsync(NewTask("test.destructive", ProjectId));
        var active = await _harness.Tasks.GetActiveIdsAsync(OrganizationId);

        // Assert
        Assert.NotNull(created.Task);
        Assert.Equal([created.Task.Id], active.ToArray());
    }

    [Fact]
    public async Task CreateAsync_QueueRejectsTheWorkItem_FailsTheTaskSoItDoesNotBlockTheProject()
    {
        // Arrange
        var queue = new Mock<IQueue<WorkItemData>>();
        queue.SetupGet(q => q.Serializer).Returns(_harness.Queue.Serializer);
        queue.SetupSequence(q => q.EnqueueAsync(It.IsAny<WorkItemData>(), It.IsAny<QueueEntryOptions>()))
            .ThrowsAsync(new TimeoutException("Queue unavailable"))
            .ReturnsAsync("queued");
        using var failingHarness = new BackgroundTaskTestHarness(_time, Log, queue: queue.Object);
        var service = CreateService(failingHarness);
        var task = NewTask("test.destructive", ProjectId);

        // Act
        var thrown = await Record.ExceptionAsync(async () => await service.CreateAsync(task));
        var orphan = await failingHarness.Tasks.GetAsync(task.Id);
        var activeAfterFailure = await failingHarness.Tasks.GetActiveIdsAsync(OrganizationId);
        var retry = await service.CreateAsync(NewTask("test.destructive", ProjectId));

        // Assert
        Assert.IsType<TimeoutException>(thrown);
        Assert.Equal(BackgroundTaskStatus.Failed, orphan!.Status);
        Assert.Empty(activeAfterFailure);
        Assert.NotNull(retry.Task);
        Assert.Equal(BackgroundTaskStatus.Queued, retry.Task.Status);
    }

    [Theory]
    [InlineData(true, typeof(MaxEntrySizeExceededCacheException))]
    [InlineData(false, typeof(InvalidOperationException))]
    public async Task CreateAsync_SelectionCannotBeStored_FailsWithoutQueueingAndAllowsAnotherOperation(bool throwOnRejection, Type expectedExceptionType)
    {
        // Arrange
        using var selectionCache = new InMemoryCacheClient(new InMemoryCacheClientOptions
        {
            TimeProvider = _time,
            LoggerFactory = Log,
            MaxEntrySize = 1,
            SizeCalculator = _ => 2,
            ShouldThrowOnMaxEntrySizeExceeded = throwOnRejection
        });
        var resolver = new Mock<IBulkSelectionResolver>();
        resolver.SetupGet(r => r.Entity).Returns(BulkOperationEntity.Events);
        resolver.Setup(r => r.PrepareAsync(It.IsAny<BackgroundTask>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BulkSelectionPreparation(BulkOperationCheckResult.Ok, 1, "{}"));
        var service = new BulkOperationService(_harness.Tasks, selectionCache, _harness.LockProvider,
            [new StubHandler("test.destructive", isDestructive: true)], [resolver.Object], _time);
        var task = NewTask("test.destructive", ProjectId);
        task.Bulk!.Selection = new BulkOperationSelection { Kind = BulkOperationSelectionKind.Filter };

        // Act
        var thrown = await Record.ExceptionAsync(async () => await service.CreateAsync(task));
        var failed = await _harness.Tasks.GetAsync(task.Id);
        var activeAfterFailure = await _harness.Tasks.GetActiveIdsAsync(OrganizationId);
        long enqueuedAfterFailure = (await _harness.Queue.GetQueueStatsAsync()).Enqueued;
        var retry = await service.CreateAsync(NewTask("test.destructive", ProjectId));

        // Assert
        Assert.IsType(expectedExceptionType, thrown);
        Assert.NotNull(failed);
        Assert.Equal(BackgroundTaskStatus.Failed, failed.Status);
        Assert.NotNull(failed.CompletedUtc);
        Assert.Empty(activeAfterFailure);
        Assert.Equal(0, enqueuedAfterFailure);
        Assert.Contains(_harness.Publisher.Updates, update => update.Id == task.Id && update.Status == BackgroundTaskStatus.Failed);
        Assert.NotNull(retry.Task);
        Assert.Equal(BackgroundTaskStatus.Queued, retry.Task.Status);
    }

    [Fact]
    public async Task PublishLostAsync_RecordIsGone_PublishesAFinalFailedUpdateFromTheWorkItem()
    {
        // Arrange
        var workItem = new BulkOperationWorkItem { TaskId = Foundatio.Repositories.Utility.ObjectId.GenerateNewId().ToString(), OrganizationId = OrganizationId, Type = "test.destructive", Description = "Record 7" };

        // Act
        await _service.PublishLostAsync(workItem);

        // Assert
        var update = Assert.Single(_harness.Publisher.Updates);
        Assert.Equal(workItem.TaskId, update.Id);
        Assert.Equal(OrganizationId, update.OrganizationId);
        Assert.Equal("Record 7", update.Description);
        Assert.Equal(BackgroundTaskStatus.Failed, update.Status);
        Assert.False(String.IsNullOrEmpty(update.Message));
    }

    private BulkOperationService CreateService(BackgroundTaskTestHarness harness)
    {
        return new BulkOperationService(harness.Tasks, harness.Cache, harness.LockProvider,
            [new StubHandler("test.destructive", isDestructive: true), new StubHandler("test.safe", isDestructive: false)], [], _time);
    }

    private static BackgroundTask NewTask(string type, string? projectId, string organizationId = OrganizationId, int ids = 1)
    {
        return new BackgroundTask
        {
            OrganizationId = organizationId,
            ProjectId = projectId,
            Type = type,
            CreatedByUserId = "537650f3b77efe23a47914f8",
            Bulk = new BulkOperationDetails
            {
                Selection = new BulkOperationSelection
                {
                    Kind = BulkOperationSelectionKind.Ids,
                    Ids = Enumerable.Range(0, ids).Select(_ => Foundatio.Repositories.Utility.ObjectId.GenerateNewId().ToString()).ToList()
                }
            }
        };
    }

    private sealed class StubHandler(string type, bool isDestructive) : IBulkOperationHandler
    {
        public string Type => type;
        public BulkOperationEntity Entity => BulkOperationEntity.Events;
        public bool IsDestructive => isDestructive;

        public Task<BulkOperationCheckResult> AuthorizeAsync(BulkOperationRequest request) => Task.FromResult(BulkOperationCheckResult.Ok);
        public Task<BulkOperationCheckResult> ValidateAsync(BulkOperationRequest request) => Task.FromResult(BulkOperationCheckResult.Ok);
        public string Describe(BackgroundTask task) => $"Record {task.Total}";
        public Task<BulkBatchResult> ExecuteBatchAsync(BackgroundTask task, IReadOnlyCollection<string> ids, CancellationToken cancellationToken) => Task.FromResult(new BulkBatchResult(ids.Count, 0));
    }
}
