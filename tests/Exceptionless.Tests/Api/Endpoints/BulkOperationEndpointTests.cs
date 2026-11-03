using System.Net;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.BulkOperations;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services;
using Exceptionless.Core.Services.BulkOperations;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Extensions;
using Exceptionless.Tests.Hubs;
using Exceptionless.Tests.Utility;
using Exceptionless.Web.Hubs;
using Exceptionless.Web.Models;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Messaging;
using Foundatio.Queues;
using Foundatio.Repositories;
using Foundatio.Repositories.Utility;
using Foundatio.Utility;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Exceptionless.Tests.Api.Endpoints;

public partial class BulkOperationEndpointTests : IntegrationTestsBase
{
    private readonly IEventRepository _eventRepository;
    private readonly BulkOperationService _bulkOperationService;
    private readonly WorkItemJob _workItemJob;

    public BulkOperationEndpointTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _eventRepository = GetService<IEventRepository>();
        _bulkOperationService = GetService<BulkOperationService>();
        _workItemJob = GetService<WorkItemJob>();
    }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        await GetService<SampleDataService>().CreateDataAsync();
    }

    [Fact]
    public async Task Create_FilterSelection_DeletesEverythingMatchingThroughTheQueuedJob()
    {
        // Arrange
        var now = TimeProvider.GetUtcNow();
        var (_, events) = await CreateDataAsync(d =>
        {
            d.Event().TestProject().Type(Event.KnownTypes.Log).Date(now.AddHours(-1));
            d.Event().TestProject().Type(Event.KnownTypes.Log).Date(now.AddDays(-2));
            d.Event().TestProject().Project(SampleDataService.TEST_ROCKET_SHIP_PROJECT_ID).Type(Event.KnownTypes.Log).Date(now.AddHours(-2));
        });

        // Act
        var created = await CreateOperationAsync(FilterRequest(SampleDataService.TEST_ORG_ID, null, "type:log"));
        await _workItemJob.RunUntilEmptyAsync(TestCancellationToken);
        var finished = await GetOperationAsync(created.Id);

        // Assert
        Assert.Equal(BulkOperationStatus.Queued, created.Status);
        Assert.Equal("events.delete", created.Type);
        Assert.Equal(3, created.Total);
        Assert.Equal("Delete 3 events matching filter", created.Description);
        Assert.Equal(BulkOperationStatus.Completed, finished.Status);
        Assert.Equal(3, finished.Processed);
        Assert.Equal(3, finished.Succeeded);
        Assert.Equal(0, finished.Failed);
        Assert.Empty(await GetRemainingMessagesAsync(events));
    }

    [Fact]
    public async Task Create_FilterSelection_OnlyDeletesMatchesInsideScopeFilterAndWindow()
    {
        // Arrange
        var now = TimeProvider.GetUtcNow();
        var (_, events) = await CreateDataAsync(d =>
        {
            d.Event().TestProject().Type(Event.KnownTypes.Log).Message("match").Date(now.AddHours(-1));
            d.Event().TestProject().Type(Event.KnownTypes.Error).Message("other type").Date(now.AddHours(-1));
            d.Event().TestProject().Type(Event.KnownTypes.Log).Message("outside window").Date(now.AddDays(-2));
            d.Event().TestProject().Project(SampleDataService.TEST_ROCKET_SHIP_PROJECT_ID).Type(Event.KnownTypes.Log).Message("other project").Date(now.AddHours(-1));
            d.Event().FreeProject().Type(Event.KnownTypes.Log).Message("other organization").Date(now.AddHours(-1));
        });

        // Act
        var created = await CreateOperationAsync(FilterRequest(SampleDataService.TEST_ORG_ID, SampleDataService.TEST_PROJECT_ID, "type:log", "[now-1d TO now]"));
        await _workItemJob.RunUntilEmptyAsync(TestCancellationToken);

        // Assert
        Assert.Equal(1, created.Total);
        var remaining = await GetRemainingMessagesAsync(events);
        Assert.DoesNotContain("match", remaining);
        Assert.Contains("other type", remaining);
        Assert.Contains("outside window", remaining);
        Assert.Contains("other project", remaining);
        Assert.Contains("other organization", remaining);
    }

    [Fact]
    public async Task Create_FilterNamingAnotherOrganization_CannotReachItsEvents()
    {
        // Arrange
        var (_, events) = await CreateDataAsync(d => d.Event().FreeProject());

        // Act
        var created = await CreateOperationAsync(FilterRequest(SampleDataService.TEST_ORG_ID, null, $"organization:{SampleDataService.FREE_ORG_ID}"));
        await _workItemJob.RunUntilEmptyAsync(TestCancellationToken);

        // Assert
        Assert.Equal(0, created.Total);
        Assert.NotNull(await _eventRepository.GetByIdAsync(events.Single().Id));
    }

    [Fact]
    public async Task Create_FilterSelection_ResolvesTimeWindowToAbsoluteDatesCappedAtCreation()
    {
        // Arrange
        var now = TimeProvider.GetUtcNow();
        var (_, events) = await CreateDataAsync(d =>
        {
            d.Event().TestProject().Message("past").Date(now.AddHours(-1));
            d.Event().TestProject().Message("future dated").Date(now.AddMinutes(10));
        });

        // Act
        var created = await CreateOperationAsync(FilterRequest(SampleDataService.TEST_ORG_ID, null, "", "[now-7d TO now+7d]"));
        TimeProvider.Advance(TimeSpan.FromHours(3));
        var reloaded = await GetOperationAsync(created.Id);
        await _workItemJob.RunUntilEmptyAsync(TestCancellationToken);

        // Assert
        Assert.NotNull(created.Selection.UtcEnd);
        Assert.True(created.Selection.UtcEnd <= created.CreatedUtc, "The window end must not be later than the moment the operation was created.");
        Assert.True(created.Selection.UtcStart < created.Selection.UtcEnd);
        Assert.Equal(created.Selection.UtcStart, reloaded.Selection.UtcStart);
        Assert.Equal(created.Selection.UtcEnd, reloaded.Selection.UtcEnd);
        Assert.Equal(["future dated"], (await GetRemainingMessagesAsync(events)).ToArray());
    }

    [Fact]
    public async Task Create_FilterSelection_DoesNotIncludeEventsStoredAfterCreation()
    {
        // Arrange
        var now = TimeProvider.GetUtcNow();
        await CreateDataAsync(d => d.Event().TestProject().Message("before").Date(now.AddHours(-1)));
        var created = await CreateOperationAsync(FilterRequest(SampleDataService.TEST_ORG_ID, null, ""));

        // Act
        TimeProvider.Advance(TimeSpan.FromMinutes(2));
        var (_, lateEvents) = await CreateDataAsync(d => d.Event().TestProject().Message("late arrival").Date(now.AddHours(-1)).CreatedDate(TimeProvider.GetUtcNow().UtcDateTime));
        await _workItemJob.RunUntilEmptyAsync(TestCancellationToken);

        // Assert
        var finished = await GetOperationAsync(created.Id);
        Assert.Equal(BulkOperationStatus.Completed, finished.Status);
        Assert.Equal(1, finished.Succeeded);
        Assert.NotNull(await _eventRepository.GetByIdAsync(lateEvents.Single().Id));
    }

    [Fact]
    public async Task Create_IdSelection_DeletesTheIdsThroughTheSameSideEffectsAsTheSingleAction()
    {
        // Arrange
        var (_, events) = await CreateDataAsync(d =>
        {
            d.Event().TestProject();
            d.Event().TestProject();
            d.Event().TestProject();
        });

        // Act
        var created = await CreateOperationAsync(new NewBulkOperation
        {
            Type = "events.delete",
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Selection = new NewBulkOperationSelection { Kind = BulkOperationSelectionKind.Ids, Ids = events.Take(2).Select(e => e.Id).ToList() }
        });
        await _workItemJob.RunUntilEmptyAsync(TestCancellationToken);

        // Assert
        var finished = await GetOperationAsync(created.Id);
        Assert.Equal(BulkOperationStatus.Completed, finished.Status);
        Assert.Equal(2, finished.Succeeded);
        Assert.Null(await _eventRepository.GetByIdAsync(events[0].Id));
        Assert.Null(await _eventRepository.GetByIdAsync(events[1].Id));
        Assert.NotNull(await _eventRepository.GetByIdAsync(events[2].Id));

        var usage = await GetService<UsageService>().GetUsageAsync(SampleDataService.TEST_ORG_ID, SampleDataService.TEST_PROJECT_ID);
        Assert.Equal(2, usage.CurrentUsage.Deleted);
    }

    [Fact]
    public async Task Create_IdSelectionWithAnotherOrganizationsEvent_LeavesThatEventAndCountsItFailed()
    {
        // Arrange
        var (_, events) = await CreateDataAsync(d =>
        {
            d.Event().TestProject();
            d.Event().FreeProject();
        });
        var own = events.Single(e => e.OrganizationId == SampleDataService.TEST_ORG_ID);
        var foreign = events.Single(e => e.OrganizationId == SampleDataService.FREE_ORG_ID);

        // Act
        var created = await CreateOperationAsync(new NewBulkOperation
        {
            Type = "events.delete",
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Selection = new NewBulkOperationSelection { Kind = BulkOperationSelectionKind.Ids, Ids = [own.Id, foreign.Id] }
        });
        await _workItemJob.RunUntilEmptyAsync(TestCancellationToken);

        // Assert
        var finished = await GetOperationAsync(created.Id);
        Assert.Equal(1, finished.Succeeded);
        Assert.Equal(1, finished.Failed);
        Assert.Null(await _eventRepository.GetByIdAsync(own.Id));
        Assert.NotNull(await _eventRepository.GetByIdAsync(foreign.Id));
    }

    [Fact]
    public async Task Create_ProjectScopedIdSelectionWithAnotherProjectsEvent_LeavesThatEvent()
    {
        // Arrange
        var (_, events) = await CreateDataAsync(d =>
        {
            d.Event().TestProject();
            d.Event().TestProject().Project(SampleDataService.TEST_ROCKET_SHIP_PROJECT_ID);
        });
        var otherProject = events.Single(e => e.ProjectId == SampleDataService.TEST_ROCKET_SHIP_PROJECT_ID);

        // Act
        var created = await CreateOperationAsync(new NewBulkOperation
        {
            Type = "events.delete",
            OrganizationId = SampleDataService.TEST_ORG_ID,
            ProjectId = SampleDataService.TEST_PROJECT_ID,
            Selection = new NewBulkOperationSelection { Kind = BulkOperationSelectionKind.Ids, Ids = events.Select(e => e.Id).ToList() }
        });
        await _workItemJob.RunUntilEmptyAsync(TestCancellationToken);

        // Assert
        var finished = await GetOperationAsync(created.Id);
        Assert.Equal(1, finished.Failed);
        Assert.NotNull(await _eventRepository.GetByIdAsync(otherProject.Id));
    }

    [Fact]
    public async Task Create_OtherOrganizationOrProject_IsNotFoundAndStartsNothing()
    {
        // Arrange
        var freeProjectInTestOrganization = FilterRequest(SampleDataService.TEST_ORG_ID, SampleDataService.FREE_PROJECT_ID, "");

        // Act
        await SendRequestAsync(r => r.Post().AsTestOrganizationUser().AppendPath("bulk-operations")
            .Content(FilterRequest(SampleDataService.FREE_ORG_ID, null, "")).StatusCodeShouldBeNotFound());
        await SendRequestAsync(r => r.Post().AsTestOrganizationUser().AppendPath("bulk-operations")
            .Content(freeProjectInTestOrganization).StatusCodeShouldBeNotFound());
        await SendRequestAsync(r => r.Post().AsFreeOrganizationUser().AppendPath("bulk-operations")
            .Content(FilterRequest(SampleDataService.TEST_ORG_ID, SampleDataService.TEST_PROJECT_ID, "")).StatusCodeShouldBeNotFound());

        // Assert
        Assert.Empty(await _bulkOperationService.GetByOrganizationAsync(SampleDataService.TEST_ORG_ID));
        Assert.Empty(await _bulkOperationService.GetByOrganizationAsync(SampleDataService.FREE_ORG_ID));
    }

    [Fact]
    public async Task Create_WithoutAUserSession_IsRejected()
    {
        // Arrange
        var request = FilterRequest(SampleDataService.TEST_ORG_ID, null, "");

        // Act
        await SendRequestAsync(r => r.Post().AppendPath("bulk-operations").Content(request).StatusCodeShouldBeUnauthorized());
        await SendRequestAsync(r => r.Post().AsTestOrganizationClientUser().AppendPath("bulk-operations").Content(request).StatusCodeShouldBeForbidden());

        // Assert
        Assert.Empty(await _bulkOperationService.GetByOrganizationAsync(SampleDataService.TEST_ORG_ID));
    }

    [Fact]
    public async Task Create_GlobalAdmin_CanStartOperationForAnyOrganization()
    {
        // Arrange
        var request = FilterRequest(SampleDataService.FREE_ORG_ID, null, "");

        // Act
        var created = await SendRequestAsAsync<ViewBulkOperation>(r => r.Post().AsGlobalAdminUser().AppendPath("bulk-operations").Content(request).StatusCodeShouldBeAccepted());

        // Assert
        Assert.NotNull(created);
        Assert.Equal(SampleDataService.FREE_ORG_ID, created.OrganizationId);
    }

    [Fact]
    public async Task Get_OperationOfAnotherOrganization_IsNotFoundForListGetAndCancel()
    {
        // Arrange
        var created = await CreateOperationAsync(FilterRequest(SampleDataService.TEST_ORG_ID, null, ""));

        // Act
        await SendRequestAsync(r => r.AsFreeOrganizationUser().AppendPaths("bulk-operations", created.Id).StatusCodeShouldBeNotFound());
        await SendRequestAsync(r => r.Post().AsFreeOrganizationUser().AppendPaths("bulk-operations", created.Id, "cancel").StatusCodeShouldBeNotFound());
        await SendRequestAsync(r => r.AsFreeOrganizationUser().AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "bulk-operations").StatusCodeShouldBeNotFound());
        var asAdmin = await SendRequestAsAsync<ViewBulkOperation>(r => r.AsGlobalAdminUser().AppendPaths("bulk-operations", created.Id).StatusCodeShouldBeOk());

        // Assert
        Assert.Equal(created.Id, asAdmin?.Id);
        Assert.Equal(BulkOperationStatus.Queued, (await GetOperationAsync(created.Id)).Status);
    }

    [Fact]
    public async Task GetByOrganization_ReturnsActiveAndRecentOperationsNewestFirst()
    {
        // Arrange
        var first = await CreateOperationAsync(FilterRequest(SampleDataService.TEST_ORG_ID, SampleDataService.TEST_PROJECT_ID, ""));
        await _workItemJob.RunUntilEmptyAsync(TestCancellationToken);
        TimeProvider.Advance(TimeSpan.FromSeconds(5));
        var second = await CreateOperationAsync(FilterRequest(SampleDataService.TEST_ORG_ID, SampleDataService.TEST_PROJECT_ID, ""));

        // Act
        var list = await SendRequestAsAsync<IReadOnlyCollection<ViewBulkOperation>>(r => r.AsTestOrganizationUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "bulk-operations").StatusCodeShouldBeOk());

        // Assert
        Assert.NotNull(list);
        Assert.Equal([second.Id, first.Id], list.Select(o => o.Id).ToArray());
        Assert.Equal(BulkOperationStatus.Queued, list.First().Status);
        Assert.Equal(BulkOperationStatus.Completed, list.Last().Status);
    }

    [Fact]
    public async Task Create_WhileADestructiveOperationIsActiveForTheProject_ReturnsConflict()
    {
        // Arrange
        var active = await CreateOperationAsync(FilterRequest(SampleDataService.TEST_ORG_ID, SampleDataService.TEST_PROJECT_ID, ""));

        // Act
        await SendRequestAsync(r => r.Post().AsTestOrganizationUser().AppendPath("bulk-operations")
            .Content(FilterRequest(SampleDataService.TEST_ORG_ID, SampleDataService.TEST_PROJECT_ID, "type:log")).StatusCodeShouldBeConflict());
        await SendRequestAsync(r => r.Post().AsTestOrganizationUser().AppendPath("bulk-operations")
            .Content(FilterRequest(SampleDataService.TEST_ORG_ID, null, "")).StatusCodeShouldBeConflict());
        var otherProject = await CreateOperationAsync(FilterRequest(SampleDataService.TEST_ORG_ID, SampleDataService.TEST_ROCKET_SHIP_PROJECT_ID, ""));
        var otherOrganization = await CreateOperationAsync(FilterRequest(SampleDataService.FREE_ORG_ID, SampleDataService.FREE_PROJECT_ID, ""), r => r.AsFreeOrganizationUser());

        // Assert
        Assert.NotEqual(active.Id, otherProject.Id);
        Assert.Equal(SampleDataService.FREE_ORG_ID, otherOrganization.OrganizationId);
    }

    [Fact]
    public async Task Create_AfterTheActiveOperationFinishesOrIsCancelled_IsAllowedAgain()
    {
        // Arrange
        var first = await CreateOperationAsync(FilterRequest(SampleDataService.TEST_ORG_ID, SampleDataService.TEST_PROJECT_ID, ""));
        await _workItemJob.RunUntilEmptyAsync(TestCancellationToken);
        var second = await CreateOperationAsync(FilterRequest(SampleDataService.TEST_ORG_ID, SampleDataService.TEST_PROJECT_ID, ""));

        // Act
        var cancelled = await SendRequestAsAsync<ViewBulkOperation>(r => r.Post().AsTestOrganizationUser().AppendPaths("bulk-operations", second.Id, "cancel").StatusCodeShouldBeAccepted());
        var third = await CreateOperationAsync(FilterRequest(SampleDataService.TEST_ORG_ID, SampleDataService.TEST_PROJECT_ID, ""));

        // Assert
        Assert.Equal(BulkOperationStatus.Completed, (await GetOperationAsync(first.Id)).Status);
        Assert.Equal(BulkOperationStatus.Cancelled, cancelled?.Status);
        Assert.NotEqual(second.Id, third.Id);
    }

    [Fact]
    public async Task Create_WhileTheActiveOperationHasStoppedReportingProgress_IsAllowed()
    {
        // Arrange
        await CreateOperationAsync(FilterRequest(SampleDataService.TEST_ORG_ID, SampleDataService.TEST_PROJECT_ID, ""));

        // Act
        TimeProvider.Advance(BulkOperationService.StaleAfter + TimeSpan.FromMinutes(1));
        var next = await CreateOperationAsync(FilterRequest(SampleDataService.TEST_ORG_ID, SampleDataService.TEST_PROJECT_ID, ""));

        // Assert
        Assert.Equal(BulkOperationStatus.Queued, next.Status);
    }

    [Fact]
    public async Task Create_InvalidRequests_AreRejectedWithoutStartingAnything()
    {
        // Arrange
        var ids = Enumerable.Range(0, 1001).Select(_ => ObjectId.GenerateNewId().ToString()).ToList();
        var requests = new[]
        {
            FilterRequest(SampleDataService.TEST_ORG_ID, null, "") with { Type = "events.explode" },
            FilterRequest(SampleDataService.TEST_ORG_ID, null, "") with { Parameters = new Dictionary<string, string> { ["x"] = "y" } },
            FilterRequest(SampleDataService.TEST_ORG_ID, null, "type:(log"),
            FilterRequest(SampleDataService.TEST_ORG_ID, null, "", "[now+1d TO now+2d]"),
            FilterRequest(SampleDataService.TEST_ORG_ID, null, "") with { Selection = new NewBulkOperationSelection { Kind = BulkOperationSelectionKind.Filter, Ids = ["537650f3b77efe23a47914f3"] } },
            FilterRequest(SampleDataService.TEST_ORG_ID, null, "") with { Selection = new NewBulkOperationSelection { Kind = BulkOperationSelectionKind.Ids, Ids = [] } },
            FilterRequest(SampleDataService.TEST_ORG_ID, null, "") with { Selection = new NewBulkOperationSelection { Kind = BulkOperationSelectionKind.Ids, Ids = ["not-an-id"] } },
            FilterRequest(SampleDataService.TEST_ORG_ID, null, "") with { Selection = new NewBulkOperationSelection { Kind = BulkOperationSelectionKind.Ids, Filter = "type:log", Ids = ["537650f3b77efe23a47914f3"] } },
            FilterRequest(SampleDataService.TEST_ORG_ID, null, "") with { Selection = new NewBulkOperationSelection { Kind = BulkOperationSelectionKind.Ids, Ids = ids } }
        };

        // Act
        foreach (var request in requests)
            await SendRequestAsync(r => r.Post().AsTestOrganizationUser().AppendPath("bulk-operations").Content(request).StatusCodeShouldBeUnprocessableEntity());

        // Assert
        Assert.Empty(await _bulkOperationService.GetByOrganizationAsync(SampleDataService.TEST_ORG_ID));
    }

    [Fact]
    public async Task Create_PremiumFilterOnFreePlan_RequiresUpgrade()
    {
        // Arrange
        var request = FilterRequest(SampleDataService.FREE_ORG_ID, null, "tag:premium-tag");

        // Act
        await SendRequestAsync(r => r.Post().AsFreeOrganizationUser().AppendPath("bulk-operations").Content(request).StatusCodeShouldBeUpgradeRequired());

        // Assert
        Assert.Empty(await _bulkOperationService.GetByOrganizationAsync(SampleDataService.FREE_ORG_ID));
    }

    [Fact]
    public async Task Cancel_QueuedOperation_CancelsImmediatelyAndTheJobDoesNothing()
    {
        // Arrange
        var (_, events) = await CreateDataAsync(d => d.Event().TestProject());
        var created = await CreateOperationAsync(FilterRequest(SampleDataService.TEST_ORG_ID, null, ""));

        // Act
        var response = await SendRequestAsAsync<ViewBulkOperation>(r => r.Post().AsTestOrganizationUser().AppendPaths("bulk-operations", created.Id, "cancel").StatusCodeShouldBeAccepted());
        await _workItemJob.RunUntilEmptyAsync(TestCancellationToken);

        // Assert
        Assert.Equal(BulkOperationStatus.Cancelled, response?.Status);
        Assert.Equal(BulkOperationStatus.Cancelled, (await GetOperationAsync(created.Id)).Status);
        Assert.NotNull(await _eventRepository.GetByIdAsync(events.Single().Id));
    }

    [Fact]
    public async Task Execute_MoreMatchesThanOneBatch_ProcessesEveryBatchAndPublishesProgress()
    {
        // Arrange
        var now = TimeProvider.GetUtcNow();
        var (_, events) = await CreateDataAsync(d =>
        {
            for (int i = 0; i < 5; i++)
                d.Event().TestProject().Date(now.AddMinutes(-i - 1));
        });
        var messages = new System.Collections.Concurrent.ConcurrentQueue<BulkOperationUpdated>();
        await GetService<IMessageSubscriber>().SubscribeAsync<BulkOperationUpdated>(m => messages.Enqueue(m), TestCancellationToken);
        var created = await CreateOperationAsync(FilterRequest(SampleDataService.TEST_ORG_ID, null, ""));

        // Act
        await RunOperationAsync(_bulkOperationService, created.Id, batchSize: 2, TestCancellationToken);
        await WaitForAsync(() => messages.Any(m => m.Status == BulkOperationStatus.Completed));

        // Assert
        var finished = await GetOperationAsync(created.Id);
        Assert.Equal(BulkOperationStatus.Completed, finished.Status);
        Assert.Equal(5, finished.Processed);
        Assert.Equal(5, finished.Succeeded);
        Assert.Empty(await GetRemainingMessagesAsync(events));

        var forOperation = messages.Where(m => m.Id == created.Id).ToList();
        Assert.Contains(forOperation, m => m.Status == BulkOperationStatus.Running && m.Processed == 2);
        Assert.Contains(forOperation, m => m.Status == BulkOperationStatus.Running && m.Processed == 4);
        Assert.Contains(forOperation, m => m.Status == BulkOperationStatus.Running && m.Processed == 5);
        var last = forOperation.OrderBy(m => m.UpdatedUtc).ThenBy(m => m.Processed).Last();
        Assert.Equal(BulkOperationStatus.Completed, last.Status);
        Assert.All(forOperation, m => Assert.Equal(SampleDataService.TEST_ORG_ID, m.OrganizationId));
    }

    [Fact]
    public async Task Execute_JobRestartsMidOperation_ResumesAfterTheSavedCursorWithoutRepeatingBatches()
    {
        // Arrange
        var now = TimeProvider.GetUtcNow();
        var (_, events) = await CreateDataAsync(d =>
        {
            for (int i = 0; i < 5; i++)
                d.Event().TestProject().Date(now.AddMinutes(-i - 1));
        });
        var recorder = new RecordingHandler();
        var service = CreateService(recorder);
        var created = await CreateRecordingOperationAsync(service);
        using var shutdown = new CancellationTokenSource();
        recorder.AfterBatch = () => shutdown.Cancel();

        // Act
        await RunOperationAsync(service, created.Id, batchSize: 2, shutdown.Token);
        var interrupted = await service.GetAsync(created.Id);
        recorder.AfterBatch = null;
        await RunOperationAsync(service, created.Id, batchSize: 2, TestCancellationToken);
        var finished = await service.GetAsync(created.Id);

        // Assert
        Assert.NotNull(interrupted);
        Assert.Equal(BulkOperationStatus.Running, interrupted.Status);
        Assert.Equal(2, interrupted.Processed);
        Assert.NotNull(interrupted.Cursor);
        Assert.NotNull(finished);
        Assert.Equal(BulkOperationStatus.Completed, finished.Status);
        Assert.Equal(5, finished.Processed);
        Assert.Equal(events.OrderByDescending(e => e.Date).Select(e => e.Id).ToArray(), recorder.Batches.SelectMany(b => b).ToArray());
        Assert.Equal([2, 2, 1], recorder.Batches.Select(b => b.Count).ToArray());
    }

    [Fact]
    public async Task Execute_CancelRequestedDuringABatch_StopsBeforeTheNextBatch()
    {
        // Arrange
        var now = TimeProvider.GetUtcNow();
        await CreateDataAsync(d =>
        {
            for (int i = 0; i < 5; i++)
                d.Event().TestProject().Date(now.AddMinutes(-i - 1));
        });
        var recorder = new RecordingHandler();
        var service = CreateService(recorder);
        var created = await CreateRecordingOperationAsync(service);
        recorder.AfterBatch = () => service.CancelAsync(created.Id).GetAwaiter().GetResult();

        // Act
        await RunOperationAsync(service, created.Id, batchSize: 2, TestCancellationToken);

        // Assert
        var finished = await service.GetAsync(created.Id);
        Assert.NotNull(finished);
        Assert.Equal(BulkOperationStatus.Cancelled, finished.Status);
        Assert.Equal(2, finished.Processed);
        Assert.NotNull(finished.CompletedUtc);
        Assert.Single(recorder.Batches);
    }

    [Fact]
    public async Task Execute_NonDestructiveOperationsDoNotBlockEachOtherOrDestructiveOnes()
    {
        // Arrange
        var recorder = new RecordingHandler();
        var service = CreateService(recorder, new RecordingHandler("test.destructive", true));

        // Act
        var first = await service.CreateAsync(NewRecordingOperation("test.record"));
        var second = await service.CreateAsync(NewRecordingOperation("test.record"));
        var destructive = await service.CreateAsync(NewRecordingOperation("test.destructive"));
        var blocked = await service.CreateAsync(NewRecordingOperation("test.destructive"));

        // Assert
        Assert.NotNull(first.Operation);
        Assert.NotNull(second.Operation);
        Assert.NotNull(destructive.Operation);
        Assert.Null(blocked.Operation);
        Assert.Equal(destructive.Operation.Id, blocked.Conflict?.Id);
    }

    [Fact]
    public async Task OnBulkOperationUpdatedAsync_SendsOnlyToTheOperationsOrganization()
    {
        // Arrange
        var broker = GetService<MessageBusBroker>();
        var connectionMapping = GetService<IConnectionMapping>();
        var connectionManager = GetService<WebSocketConnectionManager>();
        var ownSocket = new TestWebSocket();
        var otherSocket = new TestWebSocket();
        string ownConnection = connectionManager.AddWebSocket(ownSocket);
        string otherConnection = connectionManager.AddWebSocket(otherSocket);
        await connectionMapping.GroupAddAsync(SampleDataService.TEST_ORG_ID, ownConnection);
        await connectionMapping.GroupAddAsync(SampleDataService.FREE_ORG_ID, otherConnection);

        try
        {
            // Act
            await broker.OnBulkOperationUpdatedAsync(new BulkOperationUpdated
            {
                Id = "537650f3b77efe23a47914f9",
                OrganizationId = SampleDataService.TEST_ORG_ID,
                Type = "events.delete",
                Description = "Delete events matching filter",
                Status = BulkOperationStatus.Running,
                Total = 10,
                Processed = 4
            }, TestCancellationToken);

            await WaitForAsync(() => ownSocket.SentMessages.Count > 0);

            // Assert
            string sent = Assert.Single(ownSocket.SentMessages);
            Assert.Contains("\"type\":\"BulkOperationUpdated\"", sent);
            Assert.Contains("\"processed\":4", sent);
            Assert.Empty(otherSocket.SentMessages);
        }
        finally
        {
            await connectionMapping.GroupRemoveAsync(SampleDataService.TEST_ORG_ID, ownConnection);
            await connectionMapping.GroupRemoveAsync(SampleDataService.FREE_ORG_ID, otherConnection);
            await connectionManager.RemoveWebSocketAsync(ownConnection);
            await connectionManager.RemoveWebSocketAsync(otherConnection);
        }
    }

    private static NewBulkOperation FilterRequest(string organizationId, string? projectId, string filter, string time = "[now-7d TO now]")
    {
        return new NewBulkOperation
        {
            Type = "events.delete",
            OrganizationId = organizationId,
            ProjectId = projectId,
            Selection = new NewBulkOperationSelection { Kind = BulkOperationSelectionKind.Filter, Filter = filter, Time = time }
        };
    }

    private async Task<ViewBulkOperation> CreateOperationAsync(NewBulkOperation request, Action<AppSendBuilder>? authorize = null)
    {
        var created = await SendRequestAsAsync<ViewBulkOperation>(r =>
        {
            r.Post().AppendPath("bulk-operations").Content(request).StatusCodeShouldBeAccepted();
            if (authorize is null)
                r.AsTestOrganizationUser();
            else
                authorize(r);
        });

        Assert.NotNull(created);
        return created;
    }

    private async Task<ViewBulkOperation> GetOperationAsync(string id)
    {
        var operation = await SendRequestAsAsync<ViewBulkOperation>(r => r.AsTestOrganizationUser().AppendPaths("bulk-operations", id).StatusCodeShouldBeOk());
        Assert.NotNull(operation);
        return operation;
    }

    private async Task<List<string>> GetRemainingMessagesAsync(IEnumerable<PersistentEvent> events)
    {
        await RefreshDataAsync();
        var remaining = await _eventRepository.GetByIdsAsync(events.Select(e => e.Id).ToArray(), o => o.Cache(false));
        return remaining.Select(e => e.Message ?? String.Empty).OrderBy(m => m, StringComparer.Ordinal).ToList();
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(25);
    }

    private BulkOperationService CreateService(params IBulkOperationHandler[] handlers)
    {
        return CreateService(handlers, [GetService<EventBulkSelectionResolver>()]);
    }

    private BulkOperationService CreateService(IBulkOperationHandler[] handlers, IBulkSelectionResolver[] resolvers)
    {
        return new BulkOperationService(GetService<ICacheClient>(), GetService<ILockProvider>(), GetService<IMessagePublisher>(), GetService<IQueue<WorkItemData>>(),
            handlers, resolvers, TimeProvider, GetService<ILogger<BulkOperationService>>());
    }

    private async Task<BulkOperation> CreateRecordingOperationAsync(BulkOperationService service)
    {
        var result = await service.CreateAsync(NewRecordingOperation("test.record"));
        Assert.NotNull(result.Operation);
        return result.Operation;
    }

    private BulkOperation NewRecordingOperation(string type)
    {
        var now = TimeProvider.GetUtcNow().UtcDateTime;
        return new BulkOperation
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            ProjectId = SampleDataService.TEST_PROJECT_ID,
            Type = type,
            Description = "Record events",
            CreatedByUserId = "537650f3b77efe23a47914f8",
            Selection = new BulkOperationSelection
            {
                Kind = BulkOperationSelectionKind.Filter,
                Filter = String.Empty,
                UtcStart = now.AddDays(-7),
                UtcEnd = now,
                UtcCreatedBefore = now
            }
        };
    }

    private Task RunOperationAsync(BulkOperationService service, string operationId, int batchSize, CancellationToken shutdownToken)
    {
        return RunWorkItemAsync(service, new BulkOperationWorkItem { OperationId = operationId, OrganizationId = SampleDataService.TEST_ORG_ID }, batchSize, shutdownToken);
    }

    private Task RunWorkItemAsync(BulkOperationService service, BulkOperationWorkItem workItem, int batchSize, CancellationToken shutdownToken)
    {
        var handler = new Exceptionless.Core.Jobs.WorkItemHandlers.BulkOperationWorkItemHandler(service, [GetService<EventBulkSelectionResolver>()],
            new BulkOperationOptions { BatchSize = batchSize, RetryDelay = TimeSpan.Zero }, GetService<ILockProvider>(), TimeProvider, Log);
        var context = new WorkItemContext(workItem, "test-job", EmptyLock.Empty, shutdownToken, static (_, _) => Task.CompletedTask);

        return handler.HandleItemAsync(context);
    }

    private sealed class RecordingHandler(string type = "test.record", bool isDestructive = false) : IBulkOperationHandler
    {
        private int _calls;

        public List<List<string>> Batches { get; } = [];
        public Action? AfterBatch { get; set; }

        /// <summary>Called with the 1 based batch number before the batch is recorded; return an exception to make it fail.</summary>
        public Func<int, Exception?>? Failure { get; set; }

        public string Type => type;
        public BulkOperationEntity Entity => BulkOperationEntity.Events;
        public bool IsDestructive => isDestructive;

        public Task<BulkOperationCheckResult> AuthorizeAsync(BulkOperationRequest request) => Task.FromResult(BulkOperationCheckResult.Ok);
        public Task<BulkOperationCheckResult> ValidateAsync(BulkOperationRequest request) => Task.FromResult(BulkOperationCheckResult.Ok);
        public string Describe(BulkOperation operation) => "Record events";

        public Task<BulkBatchResult> ExecuteBatchAsync(BulkOperation operation, IReadOnlyCollection<string> ids, CancellationToken cancellationToken)
        {
            var failure = Failure?.Invoke(++_calls);
            if (failure is not null)
                throw failure;

            Batches.Add(ids.ToList());
            AfterBatch?.Invoke();
            return Task.FromResult(new BulkBatchResult(ids.Count, 0));
        }
    }
}
