using System.Collections.Concurrent;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.BulkOperations;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Queries.Validation;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Base;
using Exceptionless.Core.Services.BulkOperations;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Extensions;
using Exceptionless.Web.Models;
using Foundatio.Jobs;
using Foundatio.Messaging;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;
using Foundatio.Repositories.Options;
using Foundatio.Repositories.Utility;
using Foundatio.Serializer;
using Moq;
using Xunit;

namespace Exceptionless.Tests.Api.Endpoints;

public partial class BulkOperationEndpointTests
{
    [Fact]
    public async Task GetPageAsync_Paging_DoesNotCountTotalHits()
    {
        // Arrange
        ICommandOptions? capturedOptions = null;
        var repository = new Mock<IEventRepository>();
        repository.Setup(r => r.FindAsync(It.IsAny<IRepositoryQuery>(), It.IsAny<ICommandOptions>()))
            .Callback<IRepositoryQuery, ICommandOptions?>((_, options) => capturedOptions = options)
            .ReturnsAsync(new FindResults<PersistentEvent>());
        var resolver = CreateResolver(repository.Object);

        // Act
        await resolver.GetPageAsync(NewRecordingOperation("test.record"), "{}", null, 10, TestCancellationToken);

        // Assert
        Assert.NotNull(capturedOptions);
        Assert.False(capturedOptions.ShouldTrackTotalHits());
    }

    [Fact]
    public async Task Create_FilterMatchingTooManyStacks_IsRejectedWithTheRealMessage()
    {
        // Arrange
        const string message = "Event filter resulted in too many matching stacks (30000 of 20000 max). Please limit your search criteria.";
        var repository = new Mock<IEventRepository>();
        repository.Setup(r => r.CountAsync(It.IsAny<IRepositoryQuery>(), It.IsAny<ICommandOptions>())).ThrowsAsync(new DocumentLimitExceededException(message));
        var service = CreateService([GetService<DeleteEventsBulkOperationHandler>()], [CreateResolver(repository.Object)]);

        // Act
        var result = await service.CreateAsync(NewRecordingOperation("events.delete"));

        // Assert
        Assert.Null(result.Operation);
        Assert.NotNull(result.Rejection);
        Assert.Equal(BulkOperationCheckStatus.Invalid, result.Rejection.Status);
        Assert.Equal("selection.filter", result.Rejection.Field);
        Assert.Equal(message, result.Rejection.Message);
        Assert.Empty(await service.GetByOrganizationAsync(SampleDataService.TEST_ORG_ID));
    }

    [Fact]
    public async Task Execute_StackStatusChangesAfterCreation_StillDeletesTheEventsThatWereConfirmed()
    {
        // Arrange
        var (stacks, events) = await CreateDataAsync(d =>
        {
            d.Event().TestProject().Message("open 1").Status(StackStatus.Open);
            d.Event().TestProject().Message("open 2").Status(StackStatus.Open);
            d.Event().TestProject().Message("fixed").Status(StackStatus.Fixed);
        });
        var created = await CreateOperationAsync(FilterRequest(SampleDataService.TEST_ORG_ID, null, "status:open"));
        var openStacks = stacks.Where(s => s.Status == StackStatus.Open).ToList();

        // Act
        foreach (var stack in openStacks)
            stack.Status = StackStatus.Fixed;

        await GetService<IStackRepository>().SaveAsync(openStacks, o => o.ImmediateConsistency());
        await _workItemJob.RunUntilEmptyAsync(TestCancellationToken);

        // Assert
        string? pinned = await _bulkOperationService.GetPinnedSelectionAsync(created.Id);
        Assert.Equal(2, created.Total);
        Assert.NotNull(pinned);
        Assert.All(openStacks, stack => Assert.Contains(stack.Id, pinned));
        Assert.Equal(["fixed"], (await GetRemainingMessagesAsync(events)).ToArray());
    }

    [Fact]
    public async Task Create_MoreThanFiveHundredOperations_StillSeesTheNewestActiveOneForTheConflictCheck()
    {
        // Arrange
        var service = CreateService(new RecordingHandler(), new RecordingHandler("test.destructive", true));
        for (int index = 0; index < 500; index++)
            Assert.NotNull((await service.CreateAsync(NewIdsOperation("test.record"))).Operation);

        // Act
        var newest = await service.CreateAsync(NewIdsOperation("test.destructive"));
        var blocked = await service.CreateAsync(NewIdsOperation("test.destructive"));
        var list = await service.GetByOrganizationAsync(SampleDataService.TEST_ORG_ID);

        // Assert
        Assert.NotNull(newest.Operation);
        Assert.Null(blocked.Operation);
        Assert.Equal(newest.Operation.Id, blocked.Conflict?.Id);
        Assert.Equal(BulkOperationService.ListLimit, list.Count);
        Assert.Equal(newest.Operation.Id, list[0].Id);
    }

    [Fact]
    public async Task Execute_RecordMissingWhenTheJobRuns_PublishesAFinalFailedUpdate()
    {
        // Arrange
        var messages = new ConcurrentQueue<BulkOperationUpdated>();
        await GetService<IMessageSubscriber>().SubscribeAsync<BulkOperationUpdated>(m => messages.Enqueue(m), TestCancellationToken);
        var workItem = new BulkOperationWorkItem
        {
            Description = "Delete 7 events matching filter",
            OperationId = ObjectId.GenerateNewId().ToString(),
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Type = "events.delete"
        };

        // Act
        await RunWorkItemAsync(_bulkOperationService, workItem, 500, TestCancellationToken);
        await WaitForAsync(() => !messages.IsEmpty);

        // Assert
        var update = Assert.Single(messages);
        Assert.Equal(workItem.OperationId, update.Id);
        Assert.Equal(SampleDataService.TEST_ORG_ID, update.OrganizationId);
        Assert.Equal("Delete 7 events matching filter", update.Description);
        Assert.Equal(BulkOperationStatus.Failed, update.Status);
        Assert.False(String.IsNullOrEmpty(update.Message));
    }

    [Fact]
    public async Task Execute_TemporaryErrorMidOperation_IsRetriedFromTheSavedCursor()
    {
        // Arrange
        var now = TimeProvider.GetUtcNow();
        var (_, events) = await CreateDataAsync(d =>
        {
            for (int i = 0; i < 5; i++)
                d.Event().TestProject().Date(now.AddMinutes(-i - 1));
        });
        var recorder = new RecordingHandler { Failure = call => call == 2 ? new TimeoutException("Elasticsearch timed out") : null };
        var service = CreateService(recorder);
        var created = await CreateRecordingOperationAsync(service);

        // Act
        var thrown = await Record.ExceptionAsync(() => RunOperationAsync(service, created.Id, 2, TestCancellationToken));
        var interrupted = await service.GetAsync(created.Id);
        await RunOperationAsync(service, created.Id, 2, TestCancellationToken);
        var finished = await service.GetAsync(created.Id);

        // Assert
        Assert.IsType<TimeoutException>(thrown);
        Assert.NotNull(interrupted);
        Assert.Equal(BulkOperationStatus.Running, interrupted.Status);
        Assert.Equal(2, interrupted.Processed);
        Assert.Equal(1, interrupted.RetryCount);
        Assert.NotNull(finished);
        Assert.Equal(BulkOperationStatus.Completed, finished.Status);
        Assert.Equal(0, finished.RetryCount);
        Assert.Equal(events.OrderByDescending(e => e.Date).Select(e => e.Id).ToArray(), recorder.Batches.SelectMany(b => b).ToArray());
    }

    [Fact]
    public async Task Execute_TemporaryErrorsKeepHappening_FailsAfterTheRetriesAreUsedUp()
    {
        // Arrange
        await CreateDataAsync(d => d.Event().TestProject());
        var recorder = new RecordingHandler { Failure = _ => new ApplicationException("Elasticsearch unavailable") };
        var service = CreateService(recorder);
        var created = await CreateRecordingOperationAsync(service);

        // Act
        var first = await Record.ExceptionAsync(() => RunOperationAsync(service, created.Id, 2, TestCancellationToken));
        var second = await Record.ExceptionAsync(() => RunOperationAsync(service, created.Id, 2, TestCancellationToken));
        var third = await Record.ExceptionAsync(() => RunOperationAsync(service, created.Id, 2, TestCancellationToken));
        var finished = await service.GetAsync(created.Id);

        // Assert
        Assert.IsType<ApplicationException>(first);
        Assert.IsType<ApplicationException>(second);
        Assert.Null(third);
        Assert.NotNull(finished);
        Assert.Equal(BulkOperationStatus.Failed, finished.Status);
        Assert.Equal(3, finished.RetryCount);
    }

    [Fact]
    public async Task Execute_ErrorThatRetryingCannotFix_FailsImmediately()
    {
        // Arrange
        await CreateDataAsync(d => d.Event().TestProject());
        var recorder = new RecordingHandler { Failure = _ => new InvalidOperationException("Bad request") };
        var service = CreateService(recorder);
        var created = await CreateRecordingOperationAsync(service);

        // Act
        var thrown = await Record.ExceptionAsync(() => RunOperationAsync(service, created.Id, 2, TestCancellationToken));
        var finished = await service.GetAsync(created.Id);

        // Assert
        Assert.Null(thrown);
        Assert.NotNull(finished);
        Assert.Equal(BulkOperationStatus.Failed, finished.Status);
        Assert.Equal(0, finished.RetryCount);
    }

    [Fact]
    public async Task Execute_JobRestartsMidDelete_ResumesAndDeletesEachEventExactlyOnce()
    {
        // Arrange
        var now = TimeProvider.GetUtcNow();
        var (_, events) = await CreateDataAsync(d =>
        {
            for (int i = 0; i < 5; i++)
                d.Event().TestProject().Message($"event {i}").Date(now.AddMinutes(-i - 1));
        });
        var deleteHandler = new InterceptingHandler(GetService<DeleteEventsBulkOperationHandler>());
        var service = CreateService(deleteHandler);
        var result = await service.CreateAsync(NewRecordingOperation("events.delete"));
        Assert.NotNull(result.Operation);
        using var shutdown = new CancellationTokenSource();
        deleteHandler.AfterBatch = () => shutdown.Cancel();

        // Act
        await RunOperationAsync(service, result.Operation.Id, 2, shutdown.Token);
        var interrupted = await service.GetAsync(result.Operation.Id);
        var remainingWhileInterrupted = await GetRemainingMessagesAsync(events);
        deleteHandler.AfterBatch = null;
        await RunOperationAsync(service, result.Operation.Id, 2, TestCancellationToken);
        var finished = await service.GetAsync(result.Operation.Id);

        // Assert
        Assert.NotNull(interrupted);
        Assert.Equal(BulkOperationStatus.Running, interrupted.Status);
        Assert.Equal(2, interrupted.Processed);
        Assert.Equal(3, remainingWhileInterrupted.Count);
        Assert.NotNull(finished);
        Assert.Equal(BulkOperationStatus.Completed, finished.Status);
        Assert.Equal(5, finished.Processed);
        Assert.Equal(5, finished.Succeeded);
        Assert.Equal(0, finished.Failed);
        Assert.Empty(await GetRemainingMessagesAsync(events));
    }

    [Fact]
    public async Task Cancel_RunningOperationThatStoppedReportingProgress_CancelsImmediately()
    {
        // Arrange
        var service = CreateService(new RecordingHandler());
        var created = await CreateRecordingOperationAsync(service);
        await service.UpdateAsync(created.Id, operation =>
        {
            operation.Status = BulkOperationStatus.Running;
            return true;
        });
        TimeProvider.Advance(BulkOperationService.StaleAfter + TimeSpan.FromMinutes(1));

        // Act
        var cancelled = await service.CancelAsync(created.Id);

        // Assert
        Assert.NotNull(cancelled);
        Assert.Equal(BulkOperationStatus.Cancelled, cancelled.Status);
        Assert.NotNull(cancelled.CompletedUtc);
    }

    [Fact]
    public async Task Cancel_FinishedOperation_ChangesNothing()
    {
        // Arrange
        var created = await CreateOperationAsync(FilterRequest(SampleDataService.TEST_ORG_ID, null, ""));
        await _workItemJob.RunUntilEmptyAsync(TestCancellationToken);

        // Act
        var response = await SendRequestAsAsync<ViewBulkOperation>(r => r.Post().AsTestOrganizationUser().AppendPaths("bulk-operations", created.Id, "cancel").StatusCodeShouldBeAccepted());

        // Assert
        Assert.Equal(BulkOperationStatus.Completed, response?.Status);
        Assert.Equal(BulkOperationStatus.Completed, (await GetOperationAsync(created.Id)).Status);
        Assert.False(await _bulkOperationService.IsCancelRequestedAsync(created.Id));
    }

    private EventBulkSelectionResolver CreateResolver(IEventRepository eventRepository)
    {
        return new EventBulkSelectionResolver(eventRepository, GetService<IOrganizationRepository>(), GetService<IProjectRepository>(),
            GetService<PersistentEventQueryValidator>(), GetService<ITextSerializer>());
    }

    private BulkOperation NewIdsOperation(string type)
    {
        var operation = NewRecordingOperation(type);
        operation.Selection = new BulkOperationSelection { Kind = BulkOperationSelectionKind.Ids, Ids = [SampleDataService.TEST_ORG_ID] };
        return operation;
    }

    /// <summary>Runs the real handler, with a hook after each batch to simulate the host stopping.</summary>
    private sealed class InterceptingHandler(IBulkOperationHandler inner) : IBulkOperationHandler
    {
        public Action? AfterBatch { get; set; }

        public string Type => inner.Type;
        public BulkOperationEntity Entity => inner.Entity;
        public bool IsDestructive => inner.IsDestructive;

        public Task<BulkOperationCheckResult> AuthorizeAsync(BulkOperationRequest request) => inner.AuthorizeAsync(request);
        public Task<BulkOperationCheckResult> ValidateAsync(BulkOperationRequest request) => inner.ValidateAsync(request);
        public string Describe(BulkOperation operation) => inner.Describe(operation);

        public async Task<BulkBatchResult> ExecuteBatchAsync(BulkOperation operation, IReadOnlyCollection<string> ids, CancellationToken cancellationToken)
        {
            var result = await inner.ExecuteBatchAsync(operation, ids, cancellationToken);
            AfterBatch?.Invoke();
            return result;
        }
    }
}
