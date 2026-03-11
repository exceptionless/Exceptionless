using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Extensions;
using Exceptionless.Tests.Utility;
using Foundatio.Jobs;
using Foundatio.Queues;
using Foundatio.Repositories;
using Foundatio.Repositories.Utility;
using Xunit;

namespace Exceptionless.Tests.Jobs.WorkItemHandlers;

public class UpdateProjectNotificationSettingsWorkItemHandlerTests : IntegrationTestsBase
{
    private readonly WorkItemJob _workItemJob;
    private readonly IQueue<WorkItemData> _workItemQueue;
    private readonly IProjectRepository _projectRepository;
    private readonly IUserRepository _userRepository;
    private readonly UserData _userData;
    private readonly ProjectData _projectData;

    public UpdateProjectNotificationSettingsWorkItemHandlerTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _workItemJob = GetService<WorkItemJob>();
        _workItemQueue = GetService<IQueue<WorkItemData>>();
        _projectRepository = GetService<IProjectRepository>();
        _userRepository = GetService<IUserRepository>();
        _userData = GetService<UserData>();
        _projectData = GetService<ProjectData>();
    }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        var service = GetService<SampleDataService>();
        await service.CreateDataAsync();
    }

    [Fact]
    public async Task RunUntilEmptyAsync_WithOrphanedUserSettings_RemovesOrphanedEntries()
    {
        // Arrange
        var project = await _projectRepository.GetByIdAsync(TestConstants.ProjectId);
        Assert.NotNull(project);

        string orphanedUserId = ObjectId.GenerateNewId().ToString();
        project.NotificationSettings[orphanedUserId] = new NotificationSettings { ReportNewErrors = true };
        await _projectRepository.SaveAsync(project, o => o.ImmediateConsistency());

        int settingsCountBefore = project.NotificationSettings.Count;

        // Act
        await _workItemQueue.EnqueueAsync(new UpdateProjectNotificationSettingsWorkItem());
        await _workItemJob.RunUntilEmptyAsync(TestCancellationToken);

        // Assert
        project = await _projectRepository.GetByIdAsync(TestConstants.ProjectId);
        Assert.NotNull(project);
        Assert.DoesNotContain(orphanedUserId, project.NotificationSettings.Keys);
        Assert.Equal(settingsCountBefore - 1, project.NotificationSettings.Count);
    }

    [Fact]
    public async Task RunUntilEmptyAsync_WithValidOrgMemberSettings_PreservesSettings()
    {
        // Arrange
        var globalAdmin = await _userRepository.GetByEmailAddressAsync(SampleDataService.TEST_USER_EMAIL);
        Assert.NotNull(globalAdmin);

        var project = await _projectRepository.GetByIdAsync(TestConstants.ProjectId);
        Assert.NotNull(project);
        Assert.Contains(globalAdmin.Id, project.NotificationSettings.Keys);

        int settingsCountBefore = project.NotificationSettings.Count;

        // Act
        await _workItemQueue.EnqueueAsync(new UpdateProjectNotificationSettingsWorkItem());
        await _workItemJob.RunUntilEmptyAsync(TestCancellationToken);

        // Assert
        project = await _projectRepository.GetByIdAsync(TestConstants.ProjectId);
        Assert.NotNull(project);
        Assert.Contains(globalAdmin.Id, project.NotificationSettings.Keys);
        Assert.Equal(settingsCountBefore, project.NotificationSettings.Count);
    }

    [Fact]
    public async Task RunUntilEmptyAsync_WithSlackIntegrationKey_PreservesIntegrationSettings()
    {
        // Arrange
        var project = await _projectRepository.GetByIdAsync(TestConstants.ProjectId);
        Assert.NotNull(project);

        project.NotificationSettings[Project.NotificationIntegrations.Slack] = new NotificationSettings { ReportNewErrors = true };
        string orphanedUserId = ObjectId.GenerateNewId().ToString();
        project.NotificationSettings[orphanedUserId] = new NotificationSettings { ReportNewErrors = true };
        await _projectRepository.SaveAsync(project, o => o.ImmediateConsistency());

        // Act
        await _workItemQueue.EnqueueAsync(new UpdateProjectNotificationSettingsWorkItem());
        await _workItemJob.RunUntilEmptyAsync(TestCancellationToken);

        // Assert
        project = await _projectRepository.GetByIdAsync(TestConstants.ProjectId);
        Assert.NotNull(project);
        Assert.Contains(Project.NotificationIntegrations.Slack, project.NotificationSettings.Keys);
        Assert.DoesNotContain(orphanedUserId, project.NotificationSettings.Keys);
    }

    [Fact]
    public async Task RunUntilEmptyAsync_WithDeletedUserAccount_RemovesOrphanedEntry()
    {
        // Arrange
        var project = await _projectRepository.GetByIdAsync(TestConstants.ProjectId);
        Assert.NotNull(project);

        string deletedUserId = ObjectId.GenerateNewId().ToString();
        project.NotificationSettings[deletedUserId] = new NotificationSettings { ReportNewErrors = true };
        await _projectRepository.SaveAsync(project, o => o.ImmediateConsistency());

        Assert.Null(await _userRepository.GetByIdAsync(deletedUserId));

        // Act
        await _workItemQueue.EnqueueAsync(new UpdateProjectNotificationSettingsWorkItem());
        await _workItemJob.RunUntilEmptyAsync(TestCancellationToken);

        // Assert
        project = await _projectRepository.GetByIdAsync(TestConstants.ProjectId);
        Assert.NotNull(project);
        Assert.DoesNotContain(deletedUserId, project.NotificationSettings.Keys);
    }

    [Fact]
    public async Task RunUntilEmptyAsync_WithOrganizationIdFilter_OnlyProcessesTargetOrg()
    {
        // Arrange
        var project = await _projectRepository.GetByIdAsync(TestConstants.ProjectId);
        Assert.NotNull(project);

        string orphanedUserId = ObjectId.GenerateNewId().ToString();
        project.NotificationSettings[orphanedUserId] = new NotificationSettings { ReportNewErrors = true };
        await _projectRepository.SaveAsync(project, o => o.ImmediateConsistency());

        // Act: cleanup a different org — should NOT affect our project
        await _workItemQueue.EnqueueAsync(new UpdateProjectNotificationSettingsWorkItem { OrganizationId = TestConstants.OrganizationId2 });
        await _workItemJob.RunUntilEmptyAsync(TestCancellationToken);

        // Assert: orphaned entry should still exist
        project = await _projectRepository.GetByIdAsync(TestConstants.ProjectId);
        Assert.NotNull(project);
        Assert.Contains(orphanedUserId, project.NotificationSettings.Keys);

        // Act: now cleanup the correct org
        await _workItemQueue.EnqueueAsync(new UpdateProjectNotificationSettingsWorkItem { OrganizationId = TestConstants.OrganizationId });
        await _workItemJob.RunUntilEmptyAsync(TestCancellationToken);

        // Assert
        project = await _projectRepository.GetByIdAsync(TestConstants.ProjectId);
        Assert.NotNull(project);
        Assert.DoesNotContain(orphanedUserId, project.NotificationSettings.Keys);
    }

    [Fact]
    public async Task RunUntilEmptyAsync_WithMultipleProjectsContainingOrphans_CleansAllProjects()
    {
        // Arrange: create a second project in the same org
        var project1 = await _projectRepository.GetByIdAsync(TestConstants.ProjectId);
        Assert.NotNull(project1);

        var project2 = _projectData.GenerateProject(generateId: true, organizationId: TestConstants.OrganizationId);
        project2 = await _projectRepository.AddAsync(project2, o => o.ImmediateConsistency());

        string orphanedUserId = ObjectId.GenerateNewId().ToString();
        project1.NotificationSettings[orphanedUserId] = new NotificationSettings { ReportNewErrors = true };
        project2.NotificationSettings[orphanedUserId] = new NotificationSettings { ReportCriticalErrors = true };
        await _projectRepository.SaveAsync([project1, project2], o => o.ImmediateConsistency());

        // Act
        await _workItemQueue.EnqueueAsync(new UpdateProjectNotificationSettingsWorkItem { OrganizationId = TestConstants.OrganizationId });
        await _workItemJob.RunUntilEmptyAsync(TestCancellationToken);

        // Assert: both projects should be cleaned
        project1 = await _projectRepository.GetByIdAsync(project1.Id);
        project2 = await _projectRepository.GetByIdAsync(project2.Id);
        Assert.NotNull(project1);
        Assert.NotNull(project2);
        Assert.DoesNotContain(orphanedUserId, project1.NotificationSettings.Keys);
        Assert.DoesNotContain(orphanedUserId, project2.NotificationSettings.Keys);
    }

    [Fact]
    public async Task RunUntilEmptyAsync_WithMoreThanOnePageOfProjects_CleansEveryProjectPage()
    {
        // Arrange
        var firstProject = await _projectRepository.GetByIdAsync(TestConstants.ProjectId);
        Assert.NotNull(firstProject);

        string orphanedUserId = ObjectId.GenerateNewId().ToString();
        firstProject.NotificationSettings[orphanedUserId] = new NotificationSettings { ReportNewErrors = true };
        await _projectRepository.SaveAsync(firstProject, o => o.ImmediateConsistency());

        var additionalProjects = Enumerable.Range(0, 55)
            .Select(_ =>
            {
                var project = _projectData.GenerateProject(generateId: true, organizationId: TestConstants.OrganizationId);
                project.NotificationSettings[orphanedUserId] = new NotificationSettings { ReportCriticalErrors = true };
                return project;
            })
            .ToList();

        await _projectRepository.AddAsync(additionalProjects, o => o.ImmediateConsistency());

        // Act
        await _workItemQueue.EnqueueAsync(new UpdateProjectNotificationSettingsWorkItem { OrganizationId = TestConstants.OrganizationId });
        await _workItemJob.RunUntilEmptyAsync(TestCancellationToken);

        // Assert
        var refreshedProjects = await _projectRepository.GetByIdsAsync([firstProject.Id, ..additionalProjects.Select(project => project.Id)]);
        Assert.Equal(56, refreshedProjects.Count);
        Assert.All(refreshedProjects, project => Assert.DoesNotContain(orphanedUserId, project.NotificationSettings.Keys));
    }

    [Fact]
    public async Task RunUntilEmptyAsync_WithNoOrphans_MakesNoChanges()
    {
        // Arrange: project only has valid user settings (global admin from sample data)
        var project = await _projectRepository.GetByIdAsync(TestConstants.ProjectId);
        Assert.NotNull(project);

        var settingsBefore = new Dictionary<string, NotificationSettings>(project.NotificationSettings);

        // Act
        await _workItemQueue.EnqueueAsync(new UpdateProjectNotificationSettingsWorkItem());
        await _workItemJob.RunUntilEmptyAsync(TestCancellationToken);

        // Assert
        project = await _projectRepository.GetByIdAsync(TestConstants.ProjectId);
        Assert.NotNull(project);
        Assert.Equal(settingsBefore.Count, project.NotificationSettings.Count);
        foreach (var key in settingsBefore.Keys)
            Assert.Contains(key, project.NotificationSettings.Keys);
    }

    [Fact]
    public async Task RunUntilEmptyAsync_WithMixedOrphanTypes_RemovesAllOrphansAndPreservesValid()
    {
        // Arrange
        var globalAdmin = await _userRepository.GetByEmailAddressAsync(SampleDataService.TEST_USER_EMAIL);
        Assert.NotNull(globalAdmin);

        var project = await _projectRepository.GetByIdAsync(TestConstants.ProjectId);
        Assert.NotNull(project);

        string deletedUserId = ObjectId.GenerateNewId().ToString();
        string removedFromOrgUserId = ObjectId.GenerateNewId().ToString();

        var removedUser = _userData.GenerateUser(generateId: true, organizationId: TestConstants.OrganizationId2);
        removedUser.Id = removedFromOrgUserId;
        removedUser.ResetVerifyEmailAddressTokenAndExpiration(TimeProvider);
        await _userRepository.AddAsync(removedUser, o => o.ImmediateConsistency());

        project.NotificationSettings[deletedUserId] = new NotificationSettings { ReportNewErrors = true };
        project.NotificationSettings[removedFromOrgUserId] = new NotificationSettings { ReportCriticalErrors = true };
        project.NotificationSettings[Project.NotificationIntegrations.Slack] = new NotificationSettings { ReportNewErrors = true };
        await _projectRepository.SaveAsync(project, o => o.ImmediateConsistency());

        // Act
        await _workItemQueue.EnqueueAsync(new UpdateProjectNotificationSettingsWorkItem { OrganizationId = TestConstants.OrganizationId });
        await _workItemJob.RunUntilEmptyAsync(TestCancellationToken);

        // Assert
        project = await _projectRepository.GetByIdAsync(TestConstants.ProjectId);
        Assert.NotNull(project);
        Assert.DoesNotContain(deletedUserId, project.NotificationSettings.Keys);
        Assert.DoesNotContain(removedFromOrgUserId, project.NotificationSettings.Keys);
        Assert.Contains(globalAdmin.Id, project.NotificationSettings.Keys);
        Assert.Contains(Project.NotificationIntegrations.Slack, project.NotificationSettings.Keys);
    }

    [Fact]
    public async Task RunUntilEmptyAsync_WithUserInDifferentOrganization_RemovesOrphanedEntry()
    {
        // Arrange: user exists but belongs to a different org, not this one
        var otherOrgUser = _userData.GenerateUser(generateId: true, organizationId: TestConstants.OrganizationId2);
        otherOrgUser.ResetVerifyEmailAddressTokenAndExpiration(TimeProvider);
        otherOrgUser = await _userRepository.AddAsync(otherOrgUser, o => o.ImmediateConsistency());

        var project = await _projectRepository.GetByIdAsync(TestConstants.ProjectId);
        Assert.NotNull(project);
        project.NotificationSettings[otherOrgUser.Id] = new NotificationSettings { ReportNewErrors = true };
        await _projectRepository.SaveAsync(project, o => o.ImmediateConsistency());

        // Act
        await _workItemQueue.EnqueueAsync(new UpdateProjectNotificationSettingsWorkItem { OrganizationId = TestConstants.OrganizationId });
        await _workItemJob.RunUntilEmptyAsync(TestCancellationToken);

        // Assert
        project = await _projectRepository.GetByIdAsync(TestConstants.ProjectId);
        Assert.NotNull(project);
        Assert.DoesNotContain(otherOrgUser.Id, project.NotificationSettings.Keys);
    }
}
