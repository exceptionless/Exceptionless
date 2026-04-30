using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Extensions;
using Exceptionless.Tests.Utility;
using Exceptionless.Web.Models.Admin;
using Foundatio.Jobs;
using Foundatio.Queues;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;
using Foundatio.Repositories.Utility;
using Xunit;

namespace Exceptionless.Tests.Controllers;

public class AdminControllerTests : IntegrationTestsBase
{
    private readonly WorkItemJob _workItemJob;
    private readonly IQueue<WorkItemData> _workItemQueue;
    private readonly IStackRepository _stackRepository;
    private readonly IEventRepository _eventRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IUserRepository _userRepository;
    private readonly StackData _stackData;
    private readonly EventData _eventData;

    public AdminControllerTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _workItemJob = GetService<WorkItemJob>();
        _workItemQueue = GetService<IQueue<WorkItemData>>();
        _stackRepository = GetService<IStackRepository>();
        _eventRepository = GetService<IEventRepository>();
        _projectRepository = GetService<IProjectRepository>();
        _userRepository = GetService<IUserRepository>();
        _stackData = GetService<StackData>();
        _eventData = GetService<EventData>();
    }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        var service = GetService<SampleDataService>();
        await service.CreateDataAsync();
    }

    [Fact]
    public async Task RunJobAsync_WhenFixStackStatsWithExplicitUtcWindow_ShouldRepairStatsEndToEnd()
    {
        // Arrange
        TimeProvider.SetUtcNow(new DateTime(2026, 2, 15, 12, 0, 0, DateTimeKind.Utc));
        var stack = await CreateCorruptedStackWithEventAsync(new DateTimeOffset(2026, 2, 14, 0, 0, 0, TimeSpan.Zero));

        // Act
        await SendRequestAsync(r => r
            .AsGlobalAdminUser()
            .AppendPaths("admin", "maintenance", "fix-stack-stats")
            .QueryString("utcStart", "2026-02-10T00:00:00Z")
            .QueryString("utcEnd", "2026-02-23T00:00:00Z")
            .StatusCodeShouldBeOk());

        await _workItemJob.RunUntilEmptyAsync(TestCancellationToken);

        stack = await _stackRepository.GetByIdAsync(stack.Id);

        // Assert
        Assert.NotNull(stack);
        Assert.Equal(1, stack.TotalOccurrences);
        Assert.Equal(new DateTime(2026, 2, 14, 0, 0, 0, DateTimeKind.Utc), stack.FirstOccurrence);
        Assert.Equal(new DateTime(2026, 2, 14, 0, 0, 0, DateTimeKind.Utc), stack.LastOccurrence);
    }

    [Fact]
    public async Task RunJobAsync_WhenFixStackStatsWindowIsOmitted_ShouldUseDefaultStartAndCurrentUtcEnd()
    {
        // Arrange
        TimeProvider.SetUtcNow(new DateTime(2026, 2, 5, 12, 0, 0, DateTimeKind.Utc));
        var beforeWindow = await CreateCorruptedStackWithEventAsync(new DateTimeOffset(2025, 11, 1, 12, 0, 0, TimeSpan.Zero));

        TimeProvider.SetUtcNow(new DateTime(2026, 2, 15, 12, 0, 0, DateTimeKind.Utc));
        var inWindow = await CreateCorruptedStackWithEventAsync(new DateTimeOffset(2026, 2, 15, 12, 0, 0, TimeSpan.Zero));

        // Act
        await SendRequestAsync(r => r
            .AsGlobalAdminUser()
            .AppendPaths("admin", "maintenance", "fix-stack-stats")
            .StatusCodeShouldBeOk());

        await _workItemJob.RunUntilEmptyAsync(TestCancellationToken);

        beforeWindow = await _stackRepository.GetByIdAsync(beforeWindow.Id);
        inWindow = await _stackRepository.GetByIdAsync(inWindow.Id);

        // Assert
        Assert.NotNull(beforeWindow);
        Assert.NotNull(inWindow);
        Assert.Equal(0, beforeWindow.TotalOccurrences);
        Assert.Equal(1, inWindow.TotalOccurrences);
    }

    [Fact]
    public async Task RunJobAsync_WhenFixStackStatsUsesOffsetUtcTimestamp_ShouldAcceptModelBindingValue()
    {
        // Arrange
        TimeProvider.SetUtcNow(new DateTime(2026, 2, 15, 12, 0, 0, DateTimeKind.Utc));
        var stack = await CreateCorruptedStackWithEventAsync(new DateTimeOffset(2026, 2, 14, 0, 0, 0, TimeSpan.Zero));

        // Act
        await SendRequestAsync(r => r
            .AsGlobalAdminUser()
            .AppendPaths("admin", "maintenance", "fix-stack-stats")
            .QueryString("utcStart", "2026-02-10T00:00:00+00:00")
            .QueryString("utcEnd", "2026-02-23T00:00:00+00:00")
            .StatusCodeShouldBeOk());

        await _workItemJob.RunUntilEmptyAsync(TestCancellationToken);

        stack = await _stackRepository.GetByIdAsync(stack.Id);
        var stats = await _workItemQueue.GetQueueStatsAsync();

        // Assert
        Assert.NotNull(stack);
        Assert.Equal(1, stack.TotalOccurrences);
        Assert.Equal(1, stats.Enqueued);
    }

    [Fact]
    public async Task RunJobAsync_WhenFixStackStatsEndDateIsBeforeStartDate_ShouldReturnUnprocessableEntity()
    {
        // Arrange
        var response = await SendRequestAsAsync<Microsoft.AspNetCore.Mvc.ValidationProblemDetails>(r => r
            .AsGlobalAdminUser()
            .AppendPaths("admin", "maintenance", "fix-stack-stats")
            .QueryString("utcStart", "2026-02-20T00:00:00Z")
            .QueryString("utcEnd", "2026-02-10T00:00:00Z")
            .StatusCodeShouldBeUnprocessableEntity());

        // Act
        var stats = await _workItemQueue.GetQueueStatsAsync();

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Errors.ContainsKey("utc_end"));
        Assert.Equal(0, stats.Enqueued);
    }

    [Fact]
    public async Task RunJobAsync_WhenFixStackStatsStartDateIsInvalid_ShouldReturnBadRequestAndNotQueueWorkItem()
    {
        // Arrange
        await SendRequestAsync(r => r
            .AsGlobalAdminUser()
            .AppendPaths("admin", "maintenance", "fix-stack-stats")
            .QueryString("utcStart", "not-a-dateZ")
            .StatusCodeShouldBeBadRequest());

        // Act
        var stats = await _workItemQueue.GetQueueStatsAsync();

        // Assert
        Assert.Equal(0, stats.Enqueued);
    }

    private async Task<Stack> CreateCorruptedStackWithEventAsync(DateTimeOffset occurrenceDate)
    {
        var utcOccurrenceDate = occurrenceDate.UtcDateTime;
        var stack = _stackData.GenerateStack(generateId: true,
            organizationId: TestConstants.OrganizationId,
            projectId: TestConstants.ProjectId,
            totalOccurrences: 0,
            utcFirstOccurrence: utcOccurrenceDate.AddDays(1),
            utcLastOccurrence: utcOccurrenceDate.AddDays(-1));

        stack = await _stackRepository.AddAsync(stack, o => o.ImmediateConsistency());

        await _eventRepository.AddAsync(
            [_eventData.GenerateEvent(TestConstants.OrganizationId, TestConstants.ProjectId, stack.Id, occurrenceDate: occurrenceDate)],
            o => o.ImmediateConsistency());

        await RefreshDataAsync();
        return stack;
    }

    [Fact]
    public async Task RunJobAsync_WhenUpdateProjectNotificationSettingsWithOrphanedUser_ShouldRemoveOrphanedEntries()
    {
        // Arrange
        var project = await _projectRepository.GetByIdAsync(TestConstants.ProjectId);
        Assert.NotNull(project);

        string orphanedUserId = ObjectId.GenerateNewId().ToString();
        project.NotificationSettings[orphanedUserId] = new NotificationSettings { ReportNewErrors = true };
        await _projectRepository.SaveAsync(project, o => o.ImmediateConsistency());

        int settingsCountBefore = project.NotificationSettings.Count;

        // Act
        await SendRequestAsync(r => r
            .AsGlobalAdminUser()
            .AppendPaths("admin", "maintenance", "update-project-notification-settings")
            .StatusCodeShouldBeOk());

        await _workItemJob.RunUntilEmptyAsync(TestCancellationToken);

        // Assert
        project = await _projectRepository.GetByIdAsync(TestConstants.ProjectId);
        Assert.NotNull(project);
        Assert.DoesNotContain(orphanedUserId, project.NotificationSettings.Keys);
        Assert.Equal(settingsCountBefore - 1, project.NotificationSettings.Count);
    }

    [Fact]
    public async Task RunJobAsync_WhenUpdateProjectNotificationSettingsWithValidUser_ShouldPreserveSettings()
    {
        // Arrange
        var project = await _projectRepository.GetByIdAsync(TestConstants.ProjectId);
        Assert.NotNull(project);

        var globalAdmin = await _userRepository.GetByEmailAddressAsync(SampleDataService.TEST_USER_EMAIL);
        Assert.NotNull(globalAdmin);
        Assert.Contains(globalAdmin.Id, project.NotificationSettings.Keys);

        int settingsCountBefore = project.NotificationSettings.Count;

        // Act
        await SendRequestAsync(r => r
            .AsGlobalAdminUser()
            .AppendPaths("admin", "maintenance", "update-project-notification-settings")
            .StatusCodeShouldBeOk());

        await _workItemJob.RunUntilEmptyAsync(TestCancellationToken);

        // Assert
        project = await _projectRepository.GetByIdAsync(TestConstants.ProjectId);
        Assert.NotNull(project);
        Assert.Contains(globalAdmin.Id, project.NotificationSettings.Keys);
        Assert.Equal(settingsCountBefore, project.NotificationSettings.Count);
    }

    [Fact]
    public async Task RunJobAsync_WhenUpdateProjectNotificationSettingsWithIntegration_ShouldPreserveIntegrationKey()
    {
        // Arrange
        var project = await _projectRepository.GetByIdAsync(TestConstants.ProjectId);
        Assert.NotNull(project);

        project.NotificationSettings[Project.NotificationIntegrations.Slack] = new NotificationSettings { ReportNewErrors = true };
        string orphanedUserId = ObjectId.GenerateNewId().ToString();
        project.NotificationSettings[orphanedUserId] = new NotificationSettings { ReportNewErrors = true };
        await _projectRepository.SaveAsync(project, o => o.ImmediateConsistency());

        // Act
        await SendRequestAsync(r => r
            .AsGlobalAdminUser()
            .AppendPaths("admin", "maintenance", "update-project-notification-settings")
            .StatusCodeShouldBeOk());

        await _workItemJob.RunUntilEmptyAsync(TestCancellationToken);

        // Assert
        project = await _projectRepository.GetByIdAsync(TestConstants.ProjectId);
        Assert.NotNull(project);
        Assert.Contains(Project.NotificationIntegrations.Slack, project.NotificationSettings.Keys);
        Assert.DoesNotContain(orphanedUserId, project.NotificationSettings.Keys);
    }

    [Fact]
    public async Task RunJobAsync_WhenUpdateProjectNotificationSettingsWithDeletedUser_ShouldRemoveOrphanedEntries()
    {
        // Arrange
        var project = await _projectRepository.GetByIdAsync(TestConstants.ProjectId);
        Assert.NotNull(project);

        string deletedUserId = ObjectId.GenerateNewId().ToString();
        project.NotificationSettings[deletedUserId] = new NotificationSettings { ReportNewErrors = true };
        await _projectRepository.SaveAsync(project, o => o.ImmediateConsistency());

        Assert.Null(await _userRepository.GetByIdAsync(deletedUserId));

        // Act
        await SendRequestAsync(r => r
            .AsGlobalAdminUser()
            .AppendPaths("admin", "maintenance", "update-project-notification-settings")
            .StatusCodeShouldBeOk());

        await _workItemJob.RunUntilEmptyAsync(TestCancellationToken);

        // Assert
        project = await _projectRepository.GetByIdAsync(TestConstants.ProjectId);
        Assert.NotNull(project);
        Assert.DoesNotContain(deletedUserId, project.NotificationSettings.Keys);
    }

    [Fact]
    public async Task RunJobAsync_WhenUpdateProjectNotificationSettingsWithOrgFilter_ShouldOnlyProcessTargetOrg()
    {
        // Arrange
        var project = await _projectRepository.GetByIdAsync(TestConstants.ProjectId);
        Assert.NotNull(project);

        string orphanedUserId = ObjectId.GenerateNewId().ToString();
        project.NotificationSettings[orphanedUserId] = new NotificationSettings { ReportNewErrors = true };
        await _projectRepository.SaveAsync(project, o => o.ImmediateConsistency());

        // Act: run cleanup for a different org
        await SendRequestAsync(r => r
            .AsGlobalAdminUser()
            .AppendPaths("admin", "maintenance", "update-project-notification-settings")
            .QueryString("organizationId", TestConstants.OrganizationId2)
            .StatusCodeShouldBeOk());

        await _workItemJob.RunUntilEmptyAsync(TestCancellationToken);

        // Assert: orphaned user in the OTHER org should still be there
        project = await _projectRepository.GetByIdAsync(TestConstants.ProjectId);
        Assert.NotNull(project);
        Assert.Contains(orphanedUserId, project.NotificationSettings.Keys);

        // Act: now run for the correct org
        await SendRequestAsync(r => r
            .AsGlobalAdminUser()
            .AppendPaths("admin", "maintenance", "update-project-notification-settings")
            .QueryString("organizationId", TestConstants.OrganizationId)
            .StatusCodeShouldBeOk());

        await _workItemJob.RunUntilEmptyAsync(TestCancellationToken);

        // Assert
        project = await _projectRepository.GetByIdAsync(TestConstants.ProjectId);
        Assert.NotNull(project);
        Assert.DoesNotContain(orphanedUserId, project.NotificationSettings.Keys);
    }

    [Fact]
    public async Task GetStats_AsGlobalAdmin_ReturnsAllFieldsPopulated()
    {
        // Act
        var stats = await SendRequestAsAsync<AdminStatsResponse>(r => r
            .AsGlobalAdminUser()
            .AppendPaths("admin", "stats")
            .StatusCodeShouldBeOk());

        // Assert
        Assert.NotNull(stats);
        Assert.True(stats.Organizations.Total >= 0);
        Assert.True(stats.Users.Total >= 0);
        Assert.True(stats.Projects.Total >= 0);
        Assert.True(stats.Stacks.Total >= 0);
        Assert.True(stats.Events.Total >= 0);

        Assert.NotNull(stats.Organizations.Aggregations);
        Assert.NotNull(stats.Stacks.Aggregations);
        Assert.NotNull(stats.Events.Aggregations);
    }

    [Fact]
    public async Task GetStats_AsGlobalAdmin_BillingStatusBreakdownSumsToOrgCount()
    {
        // Act
        var stats = await SendRequestAsAsync<AdminStatsResponse>(r => r
            .AsGlobalAdminUser()
            .AppendPaths("admin", "stats")
            .StatusCodeShouldBeOk());

        // Assert
        Assert.NotNull(stats);
        var billingTerms = stats.Organizations.Aggregations.Terms<string>("terms_billing_status");
        Assert.NotNull(billingTerms);
        var billingBuckets = billingTerms.Buckets;
        Assert.NotNull(billingBuckets);
        long billingTotal = billingBuckets.Sum(b => b.Total ?? 0);
        Assert.Equal(stats.Organizations.Total, billingTotal);
    }

    [Fact]
    public async Task GetStats_AsGlobalAdmin_StacksByStatusSumsToStackCount()
    {
        // Act
        var stats = await SendRequestAsAsync<AdminStatsResponse>(r => r
            .AsGlobalAdminUser()
            .AppendPaths("admin", "stats")
            .StatusCodeShouldBeOk());

        // Assert
        Assert.NotNull(stats);
        var statusTerms = stats.Stacks.Aggregations.Terms<string>("terms_status");
        Assert.NotNull(statusTerms);
        var statusBuckets = statusTerms.Buckets;
        Assert.NotNull(statusBuckets);
        long statusTotal = statusBuckets.Sum(b => b.Total ?? 0);
        Assert.Equal(stats.Stacks.Total, statusTotal);
    }

    [Fact]
    public async Task GetStats_AsGlobalAdmin_StacksByTypeStatusHasValidStructure()
    {
        // Act
        var stats = await SendRequestAsAsync<AdminStatsResponse>(r => r
            .AsGlobalAdminUser()
            .AppendPaths("admin", "stats")
            .StatusCodeShouldBeOk());

        // Assert
        Assert.NotNull(stats);
        var typeTerms = stats.Stacks.Aggregations.Terms<string>("terms_type");
        Assert.NotNull(typeTerms);
        var typeBuckets = typeTerms.Buckets;
        Assert.NotNull(typeBuckets);
        foreach (var typeBucket in typeBuckets)
        {
            Assert.NotNull(typeBucket.Key);
            Assert.True(typeBucket.Total >= 0);
            var nestedStatusTerms = typeBucket.Aggregations.Terms<string>("terms_status");
            Assert.NotNull(nestedStatusTerms);
            var nestedStatusBuckets = nestedStatusTerms.Buckets;
            Assert.NotNull(nestedStatusBuckets);
            long subTotal = nestedStatusBuckets.Sum(b => b.Total ?? 0);
            Assert.Equal(typeBucket.Total, subTotal);
        }
    }

    [Fact]
    public Task GetStats_WithoutAuth_ReturnsUnauthorized()
    {
        return SendRequestAsync(r => r
            .AppendPaths("admin", "stats")
            .StatusCodeShouldBeUnauthorized());
    }

    [Fact]
    public Task RunJobAsync_AsAuthenticatedNonGlobalAdmin_ReturnsForbidden()
    {
        return SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .AppendPaths("admin", "maintenance", "fix-stack-stats")
            .StatusCodeShouldBeForbidden());
    }

    [Theory]
    [InlineData("admin/stats")]
    [InlineData("admin/migrations")]
    [InlineData("admin/elasticsearch")]
    [InlineData("admin/elasticsearch/snapshots")]
    public Task AdminReadEndpoints_AsAuthenticatedNonGlobalAdmin_ReturnForbidden(string path)
    {
        return SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .AppendPath(path)
            .StatusCodeShouldBeForbidden());
    }

    [Fact]
    public async Task GetMigrations_AsGlobalAdmin_ReturnsAllRegisteredMigrations()
    {
        // Act
        var response = await SendRequestAsAsync<MigrationsResponse>(r => r
            .AsGlobalAdminUser()
            .AppendPaths("admin", "migrations")
            .StatusCodeShouldBeOk());

        // Assert
        Assert.NotNull(response);

        if (response.States is not null)
        {
            foreach (var state in response.States)
            {
                Assert.NotNull(state.Id);
                Assert.True(Enum.IsDefined(state.MigrationType));
            }
        }
    }

    [Fact]
    public Task GetMigrations_WithoutAuth_ReturnsUnauthorized()
    {
        return SendRequestAsync(r => r
            .AppendPaths("admin", "migrations")
            .StatusCodeShouldBeUnauthorized());
    }

    [Fact]
    public async Task GetElasticsearch_AsGlobalAdmin_ReturnsClusterHealthAndIndices()
    {
        // Act
        var elasticsearch = await SendRequestAsAsync<ElasticsearchInfoResponse>(r => r
            .AsGlobalAdminUser()
            .AppendPaths("admin", "elasticsearch")
            .StatusCodeShouldBeOk());

        // Assert
        Assert.NotNull(elasticsearch);
        Assert.NotNull(elasticsearch.Health);
        Assert.NotNull(elasticsearch.Indices);
        Assert.NotNull(elasticsearch.IndexDetails);
    }

    [Fact]
    public async Task GetElasticsearch_AsGlobalAdmin_IndexDetailsContainExpectedFields()
    {
        // Act
        var elasticsearch = await SendRequestAsAsync<ElasticsearchInfoResponse>(r => r
            .AsGlobalAdminUser()
            .AppendPaths("admin", "elasticsearch")
            .StatusCodeShouldBeOk());

        // Assert
        Assert.NotNull(elasticsearch);
        if (elasticsearch.IndexDetails is not null)
        {
            Assert.All(elasticsearch.IndexDetails, indexDetail =>
            {
                Assert.True(indexDetail.DocsCount >= 0);
                Assert.True(indexDetail.StoreSizeInBytes >= 0);
                Assert.True(indexDetail.UnassignedShards >= 0);
            });
        }
    }

    [Fact]
    public Task GetElasticsearch_WithoutAuth_ReturnsUnauthorized()
    {
        return SendRequestAsync(r => r
            .AppendPaths("admin", "elasticsearch")
            .StatusCodeShouldBeUnauthorized());
    }

    [Fact]
    public async Task GetElasticsearchSnapshots_AsGlobalAdmin_ReturnsTypedResponse()
    {
        // Act
        var snapshots = await SendRequestAsAsync<ElasticsearchSnapshotsResponse>(r => r
            .AsGlobalAdminUser()
            .AppendPaths("admin", "elasticsearch", "snapshots")
            .StatusCodeShouldBeOk());

        // Assert
        Assert.NotNull(snapshots);
    }

    [Fact]
    public Task GetElasticsearchSnapshots_WithoutAuth_ReturnsUnauthorized()
    {
        return SendRequestAsync(r => r
            .AppendPaths("admin", "elasticsearch", "snapshots")
            .StatusCodeShouldBeUnauthorized());
    }
}
