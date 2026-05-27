using Exceptionless.Core.Billing;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services;
using Exceptionless.Tests.Utility;
using Foundatio.Jobs;
using Foundatio.Queues;
using Foundatio.Repositories;
using Xunit;

namespace Exceptionless.Tests.Jobs.WorkItemHandlers;

public class ResetProjectDataWorkItemHandlerTests : IntegrationTestsBase
{
    private readonly WorkItemJob _workItemJob;
    private readonly IQueue<WorkItemData> _workItemQueue;
    private readonly UsageService _usageService;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IStackRepository _stackRepository;
    private readonly IEventRepository _eventRepository;
    private readonly OrganizationData _organizationData;
    private readonly ProjectData _projectData;
    private readonly StackData _stackData;
    private readonly EventData _eventData;
    private readonly BillingManager _billingManager;
    private readonly BillingPlans _plans;

    public ResetProjectDataWorkItemHandlerTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _workItemJob = GetService<WorkItemJob>();
        _workItemQueue = GetService<IQueue<WorkItemData>>();
        _usageService = GetService<UsageService>();
        _organizationData = GetService<OrganizationData>();
        _organizationRepository = GetService<IOrganizationRepository>();
        _projectData = GetService<ProjectData>();
        _projectRepository = GetService<IProjectRepository>();
        _stackData = GetService<StackData>();
        _stackRepository = GetService<IStackRepository>();
        _eventData = GetService<EventData>();
        _eventRepository = GetService<IEventRepository>();
        _billingManager = GetService<BillingManager>();
        _plans = GetService<BillingPlans>();
    }

    [Fact]
    public async Task ResetProjectData_WithEvents_IncrementsDeletedUsageAndPersists()
    {
        // Arrange
        var organization = await _organizationRepository.AddAsync(_organizationData.GenerateSampleOrganization(_billingManager, _plans), o => o.ImmediateConsistency());
        var project = await _projectRepository.AddAsync(_projectData.GenerateSampleProject(), o => o.ImmediateConsistency());
        var stack = await _stackRepository.AddAsync(_stackData.GenerateSampleStack(), o => o.ImmediateConsistency());

        var events = _eventData.GenerateEvents(5, organization.Id, project.Id, stack.Id).ToList();
        await _eventRepository.AddAsync(events, o => o.ImmediateConsistency());

        // Act
        await _workItemQueue.EnqueueAsync(new ResetProjectDataWorkItem { OrganizationId = organization.Id, ProjectId = project.Id });
        await _workItemJob.RunUntilEmptyAsync(TestCancellationToken);
        await RefreshDataAsync();

        // Assert
        var remaining = await _eventRepository.GetAllAsync();
        Assert.DoesNotContain(remaining.Documents, e => String.Equals(e.ProjectId, project.Id, StringComparison.Ordinal));
        Assert.Null(await _stackRepository.GetByIdAsync(stack.Id, o => o.IncludeSoftDeletes()));

        var usageResponse = await _usageService.GetUsageAsync(organization.Id, project.Id);
        Assert.Equal(5, usageResponse.CurrentUsage.Deleted);
        Assert.Equal(5, usageResponse.CurrentHourUsage.Deleted);

        TimeProvider.Advance(TimeSpan.FromMinutes(10));
        await _usageService.SavePendingUsageAsync();

        var savedOrg = await _organizationRepository.GetByIdAsync(organization.Id);
        Assert.NotNull(savedOrg);
        Assert.Equal(5, savedOrg.Usage.Sum(u => u.Deleted));

        var savedProject = await _projectRepository.GetByIdAsync(project.Id);
        Assert.NotNull(savedProject);
        Assert.Equal(5, savedProject.Usage.Sum(u => u.Deleted));
    }

    [Fact]
    public async Task ResetProjectData_EmptyProject_DoesNotIncrementDeletedUsage()
    {
        // Arrange
        var organization = await _organizationRepository.AddAsync(_organizationData.GenerateSampleOrganization(_billingManager, _plans), o => o.ImmediateConsistency());
        var project = await _projectRepository.AddAsync(_projectData.GenerateSampleProject(), o => o.ImmediateConsistency());

        // Act
        await _workItemQueue.EnqueueAsync(new ResetProjectDataWorkItem { OrganizationId = organization.Id, ProjectId = project.Id });
        await _workItemJob.RunUntilEmptyAsync(TestCancellationToken);

        // Assert
        var usageResponse = await _usageService.GetUsageAsync(organization.Id, project.Id);
        Assert.Equal(0, usageResponse.CurrentUsage.Deleted);
        Assert.Equal(0, usageResponse.CurrentHourUsage.Deleted);
    }

    [Fact]
    public async Task ResetProjectData_MultipleStacks_IncrementsDeletedUsageForAllEvents()
    {
        // Arrange
        var organization = await _organizationRepository.AddAsync(_organizationData.GenerateSampleOrganization(_billingManager, _plans), o => o.ImmediateConsistency());
        var project = await _projectRepository.AddAsync(_projectData.GenerateSampleProject(), o => o.ImmediateConsistency());

        var stack1 = await _stackRepository.AddAsync(_stackData.GenerateSampleStack(), o => o.ImmediateConsistency());
        var stack2 = await _stackRepository.AddAsync(_stackData.GenerateStack(generateId: true, organizationId: organization.Id, projectId: project.Id), o => o.ImmediateConsistency());

        await _eventRepository.AddAsync(_eventData.GenerateEvents(3, organization.Id, project.Id, stack1.Id), o => o.ImmediateConsistency());
        await _eventRepository.AddAsync(_eventData.GenerateEvents(4, organization.Id, project.Id, stack2.Id), o => o.ImmediateConsistency());

        // Act
        await _workItemQueue.EnqueueAsync(new ResetProjectDataWorkItem { OrganizationId = organization.Id, ProjectId = project.Id });
        await _workItemJob.RunUntilEmptyAsync(TestCancellationToken);

        // Assert
        var usageResponse = await _usageService.GetUsageAsync(organization.Id, project.Id);
        Assert.Equal(7, usageResponse.CurrentUsage.Deleted);
    }

    [Fact]
    public async Task ResetProjectData_MultipleProjects_OnlyIncrementsTargetProject()
    {
        // Arrange
        var organization = await _organizationRepository.AddAsync(_organizationData.GenerateSampleOrganization(_billingManager, _plans), o => o.ImmediateConsistency());
        var project1 = await _projectRepository.AddAsync(_projectData.GenerateSampleProject(), o => o.ImmediateConsistency());
        var project2 = await _projectRepository.AddAsync(_projectData.GenerateProject(generateId: true, organizationId: organization.Id), o => o.ImmediateConsistency());

        var stack1 = await _stackRepository.AddAsync(_stackData.GenerateStack(generateId: true, organizationId: organization.Id, projectId: project1.Id), o => o.ImmediateConsistency());
        var stack2 = await _stackRepository.AddAsync(_stackData.GenerateStack(generateId: true, organizationId: organization.Id, projectId: project2.Id), o => o.ImmediateConsistency());

        await _eventRepository.AddAsync(_eventData.GenerateEvents(5, organization.Id, project1.Id, stack1.Id), o => o.ImmediateConsistency());
        await _eventRepository.AddAsync(_eventData.GenerateEvents(3, organization.Id, project2.Id, stack2.Id), o => o.ImmediateConsistency());

        // Act
        await _workItemQueue.EnqueueAsync(new ResetProjectDataWorkItem { OrganizationId = organization.Id, ProjectId = project1.Id });
        await _workItemJob.RunUntilEmptyAsync(TestCancellationToken);

        // Assert
        var usage1 = await _usageService.GetUsageAsync(organization.Id, project1.Id);
        var usage2 = await _usageService.GetUsageAsync(organization.Id, project2.Id);

        Assert.Equal(5, usage1.CurrentUsage.Deleted);
        Assert.Equal(0, usage2.CurrentUsage.Deleted);

        var project2Events = await _eventRepository.GetAllAsync();
        Assert.Equal(3, project2Events.Documents.Count(e => String.Equals(e.ProjectId, project2.Id, StringComparison.Ordinal)));
    }

    [Fact]
    public async Task ResetProjectData_DeletedUsage_DoesNotReduceEventsLeft()
    {
        // Arrange
        var organization = await _organizationRepository.AddAsync(_organizationData.GenerateSampleOrganization(_billingManager, _plans), o => o.ImmediateConsistency());
        var project = await _projectRepository.AddAsync(_projectData.GenerateSampleProject(), o => o.ImmediateConsistency());
        var stack = await _stackRepository.AddAsync(_stackData.GenerateSampleStack(), o => o.ImmediateConsistency());

        await _eventRepository.AddAsync(_eventData.GenerateEvents(10, organization.Id, project.Id, stack.Id), o => o.ImmediateConsistency());

        int eventsLeftBefore = await _usageService.GetEventsLeftAsync(organization.Id);

        // Act
        await _workItemQueue.EnqueueAsync(new ResetProjectDataWorkItem { OrganizationId = organization.Id, ProjectId = project.Id });
        await _workItemJob.RunUntilEmptyAsync(TestCancellationToken);

        // Assert
        int eventsLeftAfter = await _usageService.GetEventsLeftAsync(organization.Id);
        Assert.Equal(eventsLeftBefore, eventsLeftAfter);
    }
}
