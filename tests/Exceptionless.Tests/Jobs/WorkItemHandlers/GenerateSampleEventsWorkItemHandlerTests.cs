using Exceptionless.Core;
using Exceptionless.Core.Jobs.WorkItemHandlers;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.BackgroundTasks;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Plugins.EventProcessor;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Utility;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;
using Foundatio.Repositories.Options;
using Foundatio.Utility;
using Foundatio.Xunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace Exceptionless.Tests.Jobs.WorkItemHandlers;

public class GenerateSampleEventsWorkItemHandlerTests(ITestOutputHelper output) : TestWithLoggingBase(output)
{
    [Theory]
    [InlineData(true, 101, 1, 100, false, BackgroundTaskStatus.Running)]
    [InlineData(true, 100, 1, 100, true, BackgroundTaskStatus.Completed)]
    [InlineData(false, 101, 1, 100, false, BackgroundTaskStatus.Queued)]
    [InlineData(false, 100, 1, 100, true, BackgroundTaskStatus.Queued)]
    [InlineData(false, 200, 2, 100, false, BackgroundTaskStatus.Queued)]
    [InlineData(false, 1, 2, 1, true, BackgroundTaskStatus.Queued)]
    public async Task HandleItemAsync_HostStopsAfterBatch_OnlyCompletesWhenAllWorkFinished(
        bool projectScoped, int eventCount, int projectCount, int expectedProcessed, bool expectSuccess, BackgroundTaskStatus expectedTaskStatus)
    {
        // Arrange
        var time = new FakeTimeProvider();
        using var harness = new BackgroundTaskTestHarness(time, Log);
        using var services = new ServiceCollection().BuildServiceProvider();
        using var shutdown = CancellationTokenSource.CreateLinkedTokenSource(TestCancellationToken);
        var organization = new Organization { Id = SampleDataService.TEST_ORG_ID };
        var project = new Project { Id = SampleDataService.TEST_PROJECT_ID, OrganizationId = organization.Id };
        var organizations = new Mock<IOrganizationRepository>();
        organizations.Setup(repository => repository.GetByIdAsync(organization.Id, It.IsAny<ICommandOptions>()))
            .ReturnsAsync(organization);
        var projects = new Mock<IProjectRepository>();
        projects.Setup(repository => repository.GetByIdAsync(project.Id, It.IsAny<ICommandOptions>()))
            .ReturnsAsync(project);
        var hits = Enumerable.Range(0, projectCount)
            .Select(index => new Project { Id = index == 0 ? project.Id : SampleDataService.TEST_ROCKET_SHIP_PROJECT_ID, OrganizationId = organization.Id })
            .Select(item => new FindHit<Project>(item.Id, item, 0));
        projects.Setup(repository => repository.GetByOrganizationIdAsync(organization.Id, It.IsAny<CommandOptionsDescriptor<Project>>()))
            .ReturnsAsync(new FindResults<Project>(hits, projectCount));
        var pipeline = new RecordingEventPipeline(services, Log, shutdown.Cancel);
        var inner = new GenerateSampleEventsWorkItemHandler(pipeline, organizations.Object, projects.Object, harness.LockProvider, time, Log);
        var handler = new BackgroundTaskTrackingWorkItemHandler(inner, harness.Tasks, time);
        var task = new BackgroundTask
        {
            OrganizationId = organization.Id,
            ProjectId = project.Id,
            Type = BackgroundTaskTypes.GenerateProjectSampleData,
            Description = "Generate sample data",
            CreatedByUserId = "537650f3b77efe23a47914f8"
        };
        await harness.Tasks.AddAsync(task);
        var workItem = new GenerateSampleEventsWorkItem
        {
            OrganizationId = projectScoped ? organization.Id : null,
            ProjectId = projectScoped ? project.Id : null,
            TaskId = projectScoped ? task.Id : null,
            EventCount = eventCount
        };
        var reported = new List<int>();
        var context = new WorkItemContext(workItem, "test-job", EmptyLock.Empty, shutdown.Token, (progress, _) =>
        {
            reported.Add(progress);
            return Task.CompletedTask;
        });

        // Act
        await handler.HandleItemAsync(context);
        var stored = await harness.Tasks.GetAsync(task.Id);

        // Assert
        Assert.Equal(expectedProcessed, pipeline.Processed);
        Assert.Equal(expectSuccess, context.Result.IsSuccess);
        Assert.Equal(expectSuccess, reported.Contains(100));
        Assert.Equal(expectedTaskStatus, stored!.Status);
        Assert.Equal(expectedTaskStatus is BackgroundTaskStatus.Completed, stored.CompletedUtc.HasValue);
    }

    private sealed class RecordingEventPipeline(IServiceProvider services, ILoggerFactory loggerFactory, Action afterBatch)
        : EventPipeline(services, new AppOptions(), loggerFactory)
    {
        public int Processed { get; private set; }

        public override Task<ICollection<EventContext>> RunAsync(ICollection<EventContext> contexts)
        {
            Processed += contexts.Count;
            afterBatch();
            return Task.FromResult(contexts);
        }

        protected override IList<Type> GetActionTypes() => [];
    }
}
