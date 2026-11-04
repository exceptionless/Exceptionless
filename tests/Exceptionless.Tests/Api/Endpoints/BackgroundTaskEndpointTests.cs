using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models.BackgroundTasks;
using Exceptionless.Core.Models.BulkOperations;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services.BackgroundTasks;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Extensions;
using Exceptionless.Tests.Hubs;
using Exceptionless.Tests.Utility;
using Exceptionless.Web.Api.Results;
using Exceptionless.Web.Hubs;
using Exceptionless.Web.Models;
using Foundatio.Jobs;
using Foundatio.Repositories;
using Foundatio.Repositories.Utility;
using Xunit;

namespace Exceptionless.Tests.Api.Endpoints;

public class BackgroundTaskEndpointTests : IntegrationTestsBase
{
    private readonly BackgroundTaskService _taskService;
    private readonly WorkItemJob _workItemJob;

    public BackgroundTaskEndpointTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _taskService = GetService<BackgroundTaskService>();
        _workItemJob = GetService<WorkItemJob>();
    }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        await GetService<SampleDataService>().CreateDataAsync();
    }

    [Fact]
    public async Task Get_Task_ReturnsItsProgressAndOnlyBulkTasksHaveASelection()
    {
        // Arrange
        var plain = await AddTaskAsync(canCancel: false, progress: 40);
        var bulk = await AddTaskAsync(canCancel: true, bulk: true);

        // Act
        var plainView = await SendRequestAsAsync<ViewBackgroundTask>(r => r.AsTestOrganizationUser().AppendPaths("tasks", plain.Id).StatusCodeShouldBeOk());
        var bulkView = await SendRequestAsAsync<ViewBackgroundTask>(r => r.AsTestOrganizationUser().AppendPaths("tasks", bulk.Id).StatusCodeShouldBeOk());

        // Assert
        Assert.Equal(plain.Id, plainView!.Id);
        Assert.Equal(BackgroundTaskTypes.ResetProjectData, plainView.Type);
        Assert.Equal(BackgroundTaskStatus.Queued, plainView.Status);
        Assert.Equal(40, plainView.Progress);
        Assert.False(plainView.CanCancel);
        Assert.Null(plainView.Selection);
        Assert.True(bulkView!.CanCancel);
        Assert.Equal(BulkOperationSelectionKind.Ids, bulkView.Selection!.Kind);
        Assert.Equal(2, bulkView.Selection.IdCount);
    }

    [Fact]
    public async Task GetByOrganization_MixedTaskTypes_AreListedNewestFirst()
    {
        // Arrange
        var first = await AddTaskAsync(canCancel: false);
        TimeProvider.Advance(TimeSpan.FromSeconds(5));
        var second = await AddTaskAsync(canCancel: true, bulk: true);
        await AddTaskAsync(canCancel: false, organizationId: SampleDataService.FREE_ORG_ID);

        // Act
        var list = await SendRequestAsAsync<IReadOnlyCollection<ViewBackgroundTask>>(r => r.AsTestOrganizationUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "tasks").StatusCodeShouldBeOk());

        // Assert
        Assert.Equal([second.Id, first.Id], list!.Select(t => t.Id).ToArray());
    }

    [Fact]
    public async Task Get_TaskOfAnotherOrganization_IsNotFoundForGetListAndCancelButVisibleToGlobalAdmins()
    {
        // Arrange
        var task = await AddTaskAsync(canCancel: true);

        // Act
        await SendRequestAsync(r => r.AsFreeOrganizationUser().AppendPaths("tasks", task.Id).StatusCodeShouldBeNotFound());
        await SendRequestAsync(r => r.Post().AsFreeOrganizationUser().AppendPaths("tasks", task.Id, "cancel").StatusCodeShouldBeNotFound());
        await SendRequestAsync(r => r.AsFreeOrganizationUser().AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "tasks").StatusCodeShouldBeNotFound());
        await SendRequestAsync(r => r.AsTestOrganizationUser().AppendPaths("tasks", ObjectId.GenerateNewId().ToString()).StatusCodeShouldBeNotFound());
        await SendRequestAsync(r => r.Post().AsTestOrganizationUser().AppendPaths("tasks", ObjectId.GenerateNewId().ToString(), "cancel").StatusCodeShouldBeNotFound());
        var asAdmin = await SendRequestAsAsync<ViewBackgroundTask>(r => r.AsGlobalAdminUser().AppendPaths("tasks", task.Id).StatusCodeShouldBeOk());

        // Assert
        Assert.Equal(task.Id, asAdmin!.Id);
        Assert.Equal(BackgroundTaskStatus.Queued, (await _taskService.GetAsync(task.Id))!.Status);
    }

    [Fact]
    public async Task Tasks_WithoutAUserSession_AreRejected()
    {
        // Arrange
        var task = await AddTaskAsync(canCancel: true);

        // Act
        await SendRequestAsync(r => r.AppendPaths("tasks", task.Id).StatusCodeShouldBeUnauthorized());
        await SendRequestAsync(r => r.AsTestOrganizationClientUser().AppendPaths("tasks", task.Id).StatusCodeShouldBeForbidden());
        await SendRequestAsync(r => r.Post().AsTestOrganizationClientUser().AppendPaths("tasks", task.Id, "cancel").StatusCodeShouldBeForbidden());

        // Assert
        Assert.Equal(BackgroundTaskStatus.Queued, (await _taskService.GetAsync(task.Id))!.Status);
    }

    [Fact]
    public async Task Cancel_QueuedCancellableTask_IsAcceptedAndCancelledImmediately()
    {
        // Arrange
        var task = await AddTaskAsync(canCancel: true);

        // Act
        var response = await SendRequestAsAsync<ViewBackgroundTask>(r => r.Post().AsTestOrganizationUser().AppendPaths("tasks", task.Id, "cancel").StatusCodeShouldBeAccepted());

        // Assert
        Assert.Equal(BackgroundTaskStatus.Cancelled, response!.Status);
        Assert.Equal(BackgroundTaskStatus.Cancelled, (await _taskService.GetAsync(task.Id))!.Status);
    }

    [Fact]
    public async Task Cancel_ActiveTaskThatCannotBeCancelled_IsUnprocessableAndKeepsRunning()
    {
        // Arrange
        var task = await AddTaskAsync(canCancel: false);
        await _taskService.UpdateAsync(task.Id, current =>
        {
            current.Start(TimeProvider.GetUtcNow().UtcDateTime);
            return true;
        });

        // Act
        await SendRequestAsync(r => r.Post().AsTestOrganizationUser().AppendPaths("tasks", task.Id, "cancel").StatusCodeShouldBeUnprocessableEntity());

        // Assert
        Assert.Equal(BackgroundTaskStatus.Running, (await _taskService.GetAsync(task.Id))!.Status);
        Assert.False(await _taskService.IsCancelRequestedAsync(task.Id));
    }

    [Fact]
    public async Task Cancel_FinishedTaskThatCannotBeCancelled_IsAcceptedAndChangesNothing()
    {
        // Arrange
        var task = await AddTaskAsync(canCancel: false);
        await _taskService.UpdateAsync(task.Id, current =>
        {
            current.Finish(BackgroundTaskStatus.Completed, "Done", TimeProvider.GetUtcNow().UtcDateTime);
            return true;
        });

        // Act
        var response = await SendRequestAsAsync<ViewBackgroundTask>(r => r.Post().AsTestOrganizationUser().AppendPaths("tasks", task.Id, "cancel").StatusCodeShouldBeAccepted());

        // Assert
        Assert.Equal(BackgroundTaskStatus.Completed, response!.Status);
    }

    [Fact]
    public async Task ResetData_ReturnsATaskIdThatTracksTheJobUntilItCompletes()
    {
        // Arrange
        var now = TimeProvider.GetUtcNow();
        var (_, events) = await CreateDataAsync(d => d.Event().TestProject().Date(now.AddHours(-1)));

        // Act
        var accepted = await SendRequestAsAsync<WorkInProgressResult>(r => r.Post().AsTestOrganizationUser()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, "reset-data").StatusCodeShouldBeAccepted());
        string taskId = Assert.Single(accepted!.Workers);
        var queued = await GetTaskAsync(taskId);
        await _workItemJob.RunUntilEmptyAsync(TestCancellationToken);
        var completed = await GetTaskAsync(taskId);

        // Assert
        Assert.Equal(BackgroundTaskTypes.ResetProjectData, queued.Type);
        Assert.Equal(BackgroundTaskStatus.Queued, queued.Status);
        Assert.False(queued.CanCancel);
        Assert.Equal(SampleDataService.TEST_PROJECT_ID, queued.ProjectId);
        Assert.StartsWith("Reset data for ", queued.Description);
        Assert.Equal(BackgroundTaskStatus.Completed, completed.Status);
        Assert.Equal(100, completed.Progress);
        Assert.NotNull(completed.StartedUtc);
        Assert.NotNull(completed.CompletedUtc);
        await RefreshDataAsync();
        Assert.Empty(await GetService<IEventRepository>().GetByIdsAsync(events.Select(e => e.Id).ToArray(), o => o.Cache(false)));
    }

    [Fact]
    public async Task SampleData_ReturnsATaskIdThatTracksTheJobUntilItCompletes()
    {
        // Act
        var accepted = await SendRequestAsAsync<WorkInProgressResult>(r => r.Post().AsTestOrganizationUser()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, "sample-data").StatusCodeShouldBeAccepted());
        string taskId = Assert.Single(accepted!.Workers);
        var queued = await GetTaskAsync(taskId);
        await _workItemJob.RunUntilEmptyAsync(TestCancellationToken);
        var completed = await GetTaskAsync(taskId);

        // Assert
        Assert.Equal(BackgroundTaskTypes.GenerateProjectSampleData, queued.Type);
        Assert.Equal(BackgroundTaskStatus.Queued, queued.Status);
        Assert.False(queued.CanCancel);
        Assert.StartsWith("Generate sample data for ", queued.Description);
        Assert.Equal(BackgroundTaskStatus.Completed, completed.Status);
        Assert.Equal(100, completed.Progress);
    }

    [Fact]
    public async Task ResetAndSampleData_ProjectOfAnotherOrganization_IsNotFoundAndTracksNothing()
    {
        // Act
        await SendRequestAsync(r => r.Post().AsFreeOrganizationUser().AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, "reset-data").StatusCodeShouldBeNotFound());
        await SendRequestAsync(r => r.Post().AsFreeOrganizationUser().AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, "sample-data").StatusCodeShouldBeNotFound());

        // Assert
        Assert.Empty(await _taskService.GetByOrganizationAsync(SampleDataService.TEST_ORG_ID));
        Assert.Empty(await _taskService.GetByOrganizationAsync(SampleDataService.FREE_ORG_ID));
    }

    [Fact]
    public async Task OnBackgroundTaskUpdatedAsync_SendsOnlyToTheTasksOrganization()
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
            await broker.OnBackgroundTaskUpdatedAsync(new BackgroundTaskUpdated
            {
                Id = "537650f3b77efe23a47914f9",
                OrganizationId = SampleDataService.TEST_ORG_ID,
                Type = BackgroundTaskTypes.ResetProjectData,
                Description = "Reset data for Test",
                Status = BackgroundTaskStatus.Running,
                Progress = 40,
                CanCancel = false
            }, TestCancellationToken);

            await WaitForAsync(() => ownSocket.SentMessages.Count > 0);

            // Assert
            string sent = Assert.Single(ownSocket.SentMessages);
            Assert.Contains("\"type\":\"BackgroundTaskUpdated\"", sent);
            Assert.Contains("\"progress\":40", sent);
            Assert.Contains("\"can_cancel\":false", sent);
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

    private async Task<BackgroundTask> AddTaskAsync(bool canCancel, int progress = 0, bool bulk = false, string organizationId = SampleDataService.TEST_ORG_ID)
    {
        var task = new BackgroundTask
        {
            OrganizationId = organizationId,
            ProjectId = organizationId == SampleDataService.TEST_ORG_ID ? SampleDataService.TEST_PROJECT_ID : SampleDataService.FREE_PROJECT_ID,
            Type = bulk ? "events.delete" : BackgroundTaskTypes.ResetProjectData,
            Description = bulk ? "Delete 2 selected events" : "Reset data for Test",
            Progress = progress,
            CanCancel = canCancel,
            CreatedByUserId = "537650f3b77efe23a47914f8",
            Bulk = bulk ? new BulkOperationDetails
            {
                Selection = new BulkOperationSelection { Kind = BulkOperationSelectionKind.Ids, Ids = [ObjectId.GenerateNewId().ToString(), ObjectId.GenerateNewId().ToString()] }
            } : null
        };

        await _taskService.AddAsync(task);
        return task;
    }

    private async Task<ViewBackgroundTask> GetTaskAsync(string id)
    {
        var task = await SendRequestAsAsync<ViewBackgroundTask>(r => r.AsTestOrganizationUser().AppendPaths("tasks", id).StatusCodeShouldBeOk());
        Assert.NotNull(task);
        return task;
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(25);
    }
}
