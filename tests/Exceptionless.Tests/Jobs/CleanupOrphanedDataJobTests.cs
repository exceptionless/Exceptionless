using Exceptionless.Core;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Tests.Utility;
using Foundatio.Repositories;
using Foundatio.Repositories.Utility;
using Foundatio.Utility;
using Xunit;

namespace Exceptionless.Tests.Jobs;

public class CleanupOrphanedDataJobTests : IntegrationTestsBase
{
    private readonly CleanupOrphanedDataJob _job;
    private readonly OrganizationData _organizationData;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ProjectData _projectData;
    private readonly IProjectRepository _projectRepository;
    private readonly StackData _stackData;
    private readonly IStackRepository _stackRepository;
    private readonly EventData _eventData;
    private readonly IEventRepository _eventRepository;
    private readonly BillingManager _billingManager;
    private readonly BillingPlans _plans;
    private readonly ExceptionlessElasticConfiguration _configuration;

    public CleanupOrphanedDataJobTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _job = GetService<CleanupOrphanedDataJob>();
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
        _configuration = GetService<ExceptionlessElasticConfiguration>();
    }

    [Fact]
    public async Task DeleteOrphanedEventsByStack_WithValidStack_DoesNotDeleteEvents()
    {
        // Arrange
        var organization1 = _organizationData.GenerateOrganization(_billingManager, _plans, id: TestConstants.OrganizationId);
        var organization2 = _organizationData.GenerateOrganization(_billingManager, _plans, id: TestConstants.OrganizationId2);
        await _organizationRepository.AddAsync([organization1, organization2], o => o.ImmediateConsistency());

        var project1 = _projectData.GenerateProject(id: TestConstants.ProjectId, organizationId: organization1.Id);
        var project2 = _projectData.GenerateProject(generateId: true, organizationId: organization2.Id);
        await _projectRepository.AddAsync([project1, project2], o => o.ImmediateConsistency());

        var stack1 = _stackData.GenerateStack(id: TestConstants.StackId, organizationId: organization1.Id, projectId: project1.Id);
        var stack2 = _stackData.GenerateStack(generateId: true, organizationId: organization2.Id, projectId: project2.Id);
        await _stackRepository.AddAsync([stack1, stack2], o => o.ImmediateConsistency());

        var events1 = _eventData.GenerateEvents(100, organization1.Id, project1.Id, stack1.Id).ToList();
        var events2 = _eventData.GenerateEvents(100, organization2.Id, project2.Id, stack2.Id).ToList();
        await _eventRepository.AddAsync(events1.Concat(events2), o => o.ImmediateConsistency());

        // Act
        await _job.RunAsync(TestCancellationToken);

        // Assert
        var totalCount = await _eventRepository.CountAsync(o => o.IncludeSoftDeletes().ImmediateConsistency());
        Assert.Equal(200, totalCount);
    }

    [Fact]
    public async Task DeleteOrphanedEventsByStack_WithMixedOrphanedAndValid_OnlyDeletesOrphaned()
    {
        // Arrange
        var organization1 = _organizationData.GenerateOrganization(_billingManager, _plans, id: TestConstants.OrganizationId);
        var organization2 = _organizationData.GenerateOrganization(_billingManager, _plans, id: TestConstants.OrganizationId2);
        await _organizationRepository.AddAsync([organization1, organization2], o => o.ImmediateConsistency());

        var project1 = _projectData.GenerateProject(id: TestConstants.ProjectId, organizationId: organization1.Id);
        var project2 = _projectData.GenerateProject(generateId: true, organizationId: organization2.Id);
        await _projectRepository.AddAsync([project1, project2], o => o.ImmediateConsistency());

        var stack1 = _stackData.GenerateStack(id: TestConstants.StackId, organizationId: organization1.Id, projectId: project1.Id);
        var stack2 = _stackData.GenerateStack(generateId: true, organizationId: organization2.Id, projectId: project2.Id);
        await _stackRepository.AddAsync([stack1, stack2], o => o.ImmediateConsistency());

        var validEvents1 = _eventData.GenerateEvents(50, organization1.Id, project1.Id, stack1.Id).ToList();
        var validEvents2 = _eventData.GenerateEvents(50, organization2.Id, project2.Id, stack2.Id).ToList();
        var orphanedEvents1 = _eventData.GenerateEvents(30, organization1.Id, project1.Id, ObjectId.GenerateNewId().ToString()).ToList();
        var orphanedEvents2 = _eventData.GenerateEvents(20, organization2.Id, project2.Id, ObjectId.GenerateNewId().ToString()).ToList();

        await _eventRepository.AddAsync(validEvents1.Concat(validEvents2).Concat(orphanedEvents1).Concat(orphanedEvents2), o => o.ImmediateConsistency());

        var totalBefore = await _eventRepository.CountAsync(o => o.IncludeSoftDeletes().ImmediateConsistency());
        Assert.Equal(150, totalBefore);

        // Act
        await _job.RunAsync(TestCancellationToken);

        // Assert
        var totalAfter = await _eventRepository.CountAsync(o => o.IncludeSoftDeletes().ImmediateConsistency());
        Assert.Equal(100, totalAfter);
    }

    [Fact]
    public async Task DeleteOrphanedEventsByStack_LargeVolume_PreservesAllValidEvents()
    {
        // Arrange
        var organization = _organizationData.GenerateOrganization(_billingManager, _plans, id: TestConstants.OrganizationId);
        await _organizationRepository.AddAsync(organization, o => o.ImmediateConsistency());

        var project = _projectData.GenerateProject(id: TestConstants.ProjectId, organizationId: organization.Id);
        await _projectRepository.AddAsync(project, o => o.ImmediateConsistency());

        var stack = _stackData.GenerateStack(id: TestConstants.StackId, organizationId: organization.Id, projectId: project.Id);
        await _stackRepository.AddAsync(stack, o => o.ImmediateConsistency());

        await _eventRepository.AddAsync(_eventData.GenerateEvents(5000, organization.Id, project.Id, stack.Id), o => o.ImmediateConsistency());

        var orphanedEvents = _eventData.GenerateEvents(10000, organization.Id, project.Id).ToList();
        orphanedEvents.ForEach(e => e.StackId = ObjectId.GenerateNewId().ToString());
        await _eventRepository.AddAsync(orphanedEvents, o => o.ImmediateConsistency());

        var totalBefore = await _eventRepository.CountAsync(o => o.IncludeSoftDeletes().ImmediateConsistency());
        Assert.Equal(15000, totalBefore);

        // Act
        await _job.RunAsync(TestCancellationToken);

        // Assert
        var totalAfter = await _eventRepository.CountAsync(o => o.IncludeSoftDeletes().ImmediateConsistency());
        Assert.Equal(5000, totalAfter);
    }

    [Fact]
    public async Task DeleteOrphanedEventsByStack_WithManyUniqueOrphanedStacks_DeletesAllOrphanedEvents()
    {
        // Arrange
        var organization = _organizationData.GenerateSampleOrganization(_billingManager, _plans);
        await _organizationRepository.AddAsync(organization, o => o.ImmediateConsistency());
        var project = await _projectRepository.AddAsync(_projectData.GenerateSampleProject(), o => o.ImmediateConsistency());

        var stack = await _stackRepository.AddAsync(_stackData.GenerateSampleStack(), o => o.ImmediateConsistency());
        await _eventRepository.AddAsync(_eventData.GenerateEvents(50, organization.Id, project.Id, stack.Id), o => o.ImmediateConsistency());

        var orphanedEvents = _eventData.GenerateEvents(200, organization.Id, project.Id).ToList();
        var uniqueStackIds = Enumerable.Range(0, 200).Select(_ => ObjectId.GenerateNewId().ToString()).ToList();
        for (int i = 0; i < orphanedEvents.Count; i++)
            orphanedEvents[i].StackId = uniqueStackIds[i];

        await _eventRepository.AddAsync(orphanedEvents, o => o.ImmediateConsistency());

        Assert.Equal(250, await _eventRepository.CountAsync(o => o.IncludeSoftDeletes().ImmediateConsistency()));

        // Act
        await _job.RunAsync(TestCancellationToken);

        // Assert
        Assert.Equal(50, await _eventRepository.CountAsync(o => o.IncludeSoftDeletes().ImmediateConsistency()));
    }

    [Fact]
    public async Task DeleteOrphanedEventsByStack_MultipleValidStacks_PreservesAll()
    {
        // Arrange
        var organization1 = _organizationData.GenerateOrganization(_billingManager, _plans, id: TestConstants.OrganizationId);
        var organization2 = _organizationData.GenerateOrganization(_billingManager, _plans, id: TestConstants.OrganizationId2);
        await _organizationRepository.AddAsync([organization1, organization2], o => o.ImmediateConsistency());

        var project1 = _projectData.GenerateProject(id: TestConstants.ProjectId, organizationId: organization1.Id);
        var project2 = _projectData.GenerateProject(generateId: true, organizationId: organization2.Id);
        await _projectRepository.AddAsync([project1, project2], o => o.ImmediateConsistency());

        var stacks = new List<Stack>();
        for (int i = 0; i < 10; i++)
        {
            stacks.Add(_stackData.GenerateStack(generateId: true, organizationId: organization1.Id, projectId: project1.Id));
            stacks.Add(_stackData.GenerateStack(generateId: true, organizationId: organization2.Id, projectId: project2.Id));
        }
        await _stackRepository.AddAsync(stacks, o => o.ImmediateConsistency());

        var events = new List<PersistentEvent>();
        foreach (var stack in stacks)
            events.AddRange(_eventData.GenerateEvents(10, stack.OrganizationId, stack.ProjectId, stack.Id));
        await _eventRepository.AddAsync(events, o => o.ImmediateConsistency());

        // Act
        await _job.RunAsync(TestCancellationToken);

        // Assert
        var totalAfter = await _eventRepository.CountAsync(o => o.IncludeSoftDeletes().ImmediateConsistency());
        Assert.Equal(200, totalAfter);
    }

    [Fact]
    public async Task DeleteOrphanedEventsByStack_OnlyOrphanedEventsInOneTenant_OtherTenantUnaffected()
    {
        // Arrange
        var organization1 = _organizationData.GenerateOrganization(_billingManager, _plans, id: TestConstants.OrganizationId);
        var organization2 = _organizationData.GenerateOrganization(_billingManager, _plans, id: TestConstants.OrganizationId2);
        await _organizationRepository.AddAsync([organization1, organization2], o => o.ImmediateConsistency());

        var project1 = _projectData.GenerateProject(id: TestConstants.ProjectId, organizationId: organization1.Id);
        var project2 = _projectData.GenerateProject(generateId: true, organizationId: organization2.Id);
        await _projectRepository.AddAsync([project1, project2], o => o.ImmediateConsistency());

        var validStack = _stackData.GenerateStack(generateId: true, organizationId: organization2.Id, projectId: project2.Id);
        await _stackRepository.AddAsync(validStack, o => o.ImmediateConsistency());

        var orphanedEvents = _eventData.GenerateEvents(100, organization1.Id, project1.Id).ToList();
        orphanedEvents.ForEach(e => e.StackId = ObjectId.GenerateNewId().ToString());
        var validEvents = _eventData.GenerateEvents(100, organization2.Id, project2.Id, validStack.Id).ToList();

        await _eventRepository.AddAsync(orphanedEvents.Concat(validEvents), o => o.ImmediateConsistency());

        // Act
        await _job.RunAsync(TestCancellationToken);

        // Assert
        var totalAfter = await _eventRepository.CountAsync(o => o.IncludeSoftDeletes().ImmediateConsistency());
        Assert.Equal(100, totalAfter);
    }

    [Fact]
    public async Task DeleteOrphanedEventsByStack_WithSoftDeletedStack_DeletesOrphanedEvents()
    {
        // Arrange
        var organization = _organizationData.GenerateSampleOrganization(_billingManager, _plans);
        await _organizationRepository.AddAsync(organization, o => o.ImmediateConsistency());
        var project = await _projectRepository.AddAsync(_projectData.GenerateSampleProject(), o => o.ImmediateConsistency());

        var stack = await _stackRepository.AddAsync(_stackData.GenerateSampleStack(), o => o.ImmediateConsistency());
        await _eventRepository.AddAsync(_eventData.GenerateEvents(50, organization.Id, project.Id, stack.Id), o => o.ImmediateConsistency());

        var softDeletedStack = _stackData.GenerateStack(generateId: true, organizationId: organization.Id, projectId: project.Id);
        softDeletedStack.IsDeleted = true;
        softDeletedStack = await _stackRepository.AddAsync(softDeletedStack, o => o.ImmediateConsistency());
        await _eventRepository.AddAsync(_eventData.GenerateEvents(100, organization.Id, project.Id, softDeletedStack.Id), o => o.ImmediateConsistency());

        Assert.Equal(150, await _eventRepository.CountAsync(o => o.IncludeSoftDeletes().ImmediateConsistency()));

        // Act
        await _job.RunAsync(TestCancellationToken);

        // Assert
        Assert.Equal(50, await _eventRepository.CountAsync(o => o.IncludeSoftDeletes().ImmediateConsistency()));
    }

    [Fact]
    public async Task DeleteOrphanedEventsByProject_WithValidProjects_DoesNotDeleteEvents()
    {
        // Arrange
        var organization = _organizationData.GenerateOrganization(_billingManager, _plans, id: TestConstants.OrganizationId);
        await _organizationRepository.AddAsync(organization, o => o.ImmediateConsistency());

        var project1 = _projectData.GenerateProject(id: TestConstants.ProjectId, organizationId: organization.Id);
        var project2 = _projectData.GenerateProject(generateId: true, organizationId: organization.Id);
        await _projectRepository.AddAsync([project1, project2], o => o.ImmediateConsistency());

        var stack1 = _stackData.GenerateStack(id: TestConstants.StackId, organizationId: organization.Id, projectId: project1.Id);
        var stack2 = _stackData.GenerateStack(generateId: true, organizationId: organization.Id, projectId: project2.Id);
        await _stackRepository.AddAsync([stack1, stack2], o => o.ImmediateConsistency());

        var events = _eventData.GenerateEvents(50, organization.Id, project1.Id, stack1.Id)
            .Concat(_eventData.GenerateEvents(50, organization.Id, project2.Id, stack2.Id)).ToList();
        await _eventRepository.AddAsync(events, o => o.ImmediateConsistency());

        // Act
        await _job.RunAsync(TestCancellationToken);

        // Assert
        var totalAfter = await _eventRepository.CountAsync(o => o.IncludeSoftDeletes().ImmediateConsistency());
        Assert.Equal(100, totalAfter);
    }

    [Fact]
    public async Task DeleteOrphanedEventsByProject_WithOrphanedProject_DeletesEventsForMissingProject()
    {
        // Arrange
        var organization1 = _organizationData.GenerateOrganization(_billingManager, _plans, id: TestConstants.OrganizationId);
        var organization2 = _organizationData.GenerateOrganization(_billingManager, _plans, id: TestConstants.OrganizationId2);
        await _organizationRepository.AddAsync([organization1, organization2], o => o.ImmediateConsistency());

        var validProject = _projectData.GenerateProject(id: TestConstants.ProjectId, organizationId: organization1.Id);
        await _projectRepository.AddAsync(validProject, o => o.ImmediateConsistency());

        var validStack = _stackData.GenerateStack(id: TestConstants.StackId, organizationId: organization1.Id, projectId: validProject.Id);
        await _stackRepository.AddAsync(validStack, o => o.ImmediateConsistency());

        var validEvents = _eventData.GenerateEvents(75, organization1.Id, validProject.Id, validStack.Id).ToList();
        string fakeProjectId = ObjectId.GenerateNewId().ToString();
        string fakeStackId = ObjectId.GenerateNewId().ToString();
        var orphanedEvents = _eventData.GenerateEvents(50, organization2.Id, fakeProjectId, fakeStackId).ToList();

        await _eventRepository.AddAsync(validEvents.Concat(orphanedEvents), o => o.ImmediateConsistency());

        // Act
        await _job.RunAsync(TestCancellationToken);

        // Assert
        var totalAfter = await _eventRepository.CountAsync(o => o.IncludeSoftDeletes().ImmediateConsistency());
        Assert.Equal(75, totalAfter);
    }

    [Fact]
    public async Task DeleteOrphanedEventsByProject_WithMissingProjects_DeletesOrphanedEvents()
    {
        // Arrange
        var organization = _organizationData.GenerateSampleOrganization(_billingManager, _plans);
        await _organizationRepository.AddAsync(organization, o => o.ImmediateConsistency());
        var project = await _projectRepository.AddAsync(_projectData.GenerateSampleProject(), o => o.ImmediateConsistency());
        var stack = await _stackRepository.AddAsync(_stackData.GenerateSampleStack(), o => o.ImmediateConsistency());
        await _eventRepository.AddAsync(_eventData.GenerateEvents(50, organization.Id, project.Id, stack.Id), o => o.ImmediateConsistency());

        string fakeProjectId = ObjectId.GenerateNewId().ToString();
        var orphanedEvents = _eventData.GenerateEvents(100, organization.Id, fakeProjectId).ToList();
        orphanedEvents.ForEach(e => e.StackId = stack.Id);
        await _eventRepository.AddAsync(orphanedEvents, o => o.ImmediateConsistency());

        Assert.Equal(150, await _eventRepository.CountAsync(o => o.IncludeSoftDeletes().ImmediateConsistency()));

        // Act
        await _job.RunAsync(TestCancellationToken);

        // Assert
        Assert.Equal(50, await _eventRepository.CountAsync(o => o.IncludeSoftDeletes().ImmediateConsistency()));
    }

    [Fact]
    public async Task DeleteOrphanedEventsByProject_MultiTenant_EachTenantIndependent()
    {
        // Arrange
        var organization1 = _organizationData.GenerateOrganization(_billingManager, _plans, id: TestConstants.OrganizationId);
        var organization2 = _organizationData.GenerateOrganization(_billingManager, _plans, id: TestConstants.OrganizationId2);
        await _organizationRepository.AddAsync([organization1, organization2], o => o.ImmediateConsistency());

        var project1 = _projectData.GenerateProject(id: TestConstants.ProjectId, organizationId: organization1.Id);
        await _projectRepository.AddAsync(project1, o => o.ImmediateConsistency());

        var stack1 = _stackData.GenerateStack(id: TestConstants.StackId, organizationId: organization1.Id, projectId: project1.Id);
        await _stackRepository.AddAsync(stack1, o => o.ImmediateConsistency());

        var validEvents = _eventData.GenerateEvents(60, organization1.Id, project1.Id, stack1.Id).ToList();
        string nonExistentProjectId = ObjectId.GenerateNewId().ToString();
        string fakeStackId = ObjectId.GenerateNewId().ToString();
        var orphanedEvents = _eventData.GenerateEvents(40, organization2.Id, nonExistentProjectId, fakeStackId).ToList();

        await _eventRepository.AddAsync(validEvents.Concat(orphanedEvents), o => o.ImmediateConsistency());

        // Act
        await _job.RunAsync(TestCancellationToken);

        // Assert
        var totalAfter = await _eventRepository.CountAsync(o => o.IncludeSoftDeletes().ImmediateConsistency());
        Assert.Equal(60, totalAfter);
    }

    [Fact]
    public async Task DeleteOrphanedEventsByProject_WithSoftDeletedProject_DeletesOrphanedEvents()
    {
        // Arrange
        var organization = _organizationData.GenerateSampleOrganization(_billingManager, _plans);
        await _organizationRepository.AddAsync(organization, o => o.ImmediateConsistency());

        var project = await _projectRepository.AddAsync(_projectData.GenerateSampleProject(), o => o.ImmediateConsistency());
        var stack = await _stackRepository.AddAsync(_stackData.GenerateSampleStack(), o => o.ImmediateConsistency());
        await _eventRepository.AddAsync(_eventData.GenerateEvents(50, organization.Id, project.Id, stack.Id), o => o.ImmediateConsistency());

        var softDeletedProject = _projectData.GenerateProject(generateId: true, organizationId: organization.Id);
        softDeletedProject.IsDeleted = true;
        softDeletedProject = await _projectRepository.AddAsync(softDeletedProject, o => o.ImmediateConsistency());
        var orphanedEvents = _eventData.GenerateEvents(100, organization.Id, softDeletedProject.Id).ToList();
        orphanedEvents.ForEach(e => e.StackId = stack.Id);
        await _eventRepository.AddAsync(orphanedEvents, o => o.ImmediateConsistency());

        Assert.Equal(150, await _eventRepository.CountAsync(o => o.IncludeSoftDeletes().ImmediateConsistency()));

        // Act
        await _job.RunAsync(TestCancellationToken);

        // Assert
        Assert.Equal(50, await _eventRepository.CountAsync(o => o.IncludeSoftDeletes().ImmediateConsistency()));
    }

    [Fact]
    public async Task DeleteOrphanedEventsByOrganization_WithValidOrganizations_DoesNotDeleteEvents()
    {
        // Arrange
        var organization1 = _organizationData.GenerateOrganization(_billingManager, _plans, id: TestConstants.OrganizationId);
        var organization2 = _organizationData.GenerateOrganization(_billingManager, _plans, id: TestConstants.OrganizationId2);
        await _organizationRepository.AddAsync([organization1, organization2], o => o.ImmediateConsistency());

        var project1 = _projectData.GenerateProject(id: TestConstants.ProjectId, organizationId: organization1.Id);
        var project2 = _projectData.GenerateProject(generateId: true, organizationId: organization2.Id);
        await _projectRepository.AddAsync([project1, project2], o => o.ImmediateConsistency());

        var stack1 = _stackData.GenerateStack(id: TestConstants.StackId, organizationId: organization1.Id, projectId: project1.Id);
        var stack2 = _stackData.GenerateStack(generateId: true, organizationId: organization2.Id, projectId: project2.Id);
        await _stackRepository.AddAsync([stack1, stack2], o => o.ImmediateConsistency());

        var events = _eventData.GenerateEvents(80, organization1.Id, project1.Id, stack1.Id)
            .Concat(_eventData.GenerateEvents(80, organization2.Id, project2.Id, stack2.Id)).ToList();
        await _eventRepository.AddAsync(events, o => o.ImmediateConsistency());

        // Act
        await _job.RunAsync(TestCancellationToken);

        // Assert
        var totalAfter = await _eventRepository.CountAsync(o => o.IncludeSoftDeletes().ImmediateConsistency());
        Assert.Equal(160, totalAfter);
    }

    [Fact]
    public async Task DeleteOrphanedEventsByOrganization_WithOrphanedOrganization_DeletesEventsForMissingOrganization()
    {
        // Arrange
        var organization1 = _organizationData.GenerateOrganization(_billingManager, _plans, id: TestConstants.OrganizationId);
        await _organizationRepository.AddAsync(organization1, o => o.ImmediateConsistency());

        var project = _projectData.GenerateProject(id: TestConstants.ProjectId, organizationId: organization1.Id);
        await _projectRepository.AddAsync(project, o => o.ImmediateConsistency());

        var stack = _stackData.GenerateStack(id: TestConstants.StackId, organizationId: organization1.Id, projectId: project.Id);
        await _stackRepository.AddAsync(stack, o => o.ImmediateConsistency());

        var validEvents = _eventData.GenerateEvents(100, organization1.Id, project.Id, stack.Id).ToList();
        string fakeOrganizationId = ObjectId.GenerateNewId().ToString();
        string fakeProjectId = ObjectId.GenerateNewId().ToString();
        string fakeStackId = ObjectId.GenerateNewId().ToString();
        var orphanedEvents = _eventData.GenerateEvents(50, fakeOrganizationId, fakeProjectId, fakeStackId).ToList();

        await _eventRepository.AddAsync(validEvents.Concat(orphanedEvents), o => o.ImmediateConsistency());

        // Act
        await _job.RunAsync(TestCancellationToken);

        // Assert
        var totalAfter = await _eventRepository.CountAsync(o => o.IncludeSoftDeletes().ImmediateConsistency());
        Assert.Equal(100, totalAfter);
    }

    [Fact]
    public async Task DeleteOrphanedEventsByOrganization_WithMissingOrganizations_DeletesOrphanedEvents()
    {
        // Arrange
        var organization = _organizationData.GenerateSampleOrganization(_billingManager, _plans);
        await _organizationRepository.AddAsync(organization, o => o.ImmediateConsistency());
        var project = await _projectRepository.AddAsync(_projectData.GenerateSampleProject(), o => o.ImmediateConsistency());
        var stack = await _stackRepository.AddAsync(_stackData.GenerateSampleStack(), o => o.ImmediateConsistency());
        await _eventRepository.AddAsync(_eventData.GenerateEvents(50, organization.Id, project.Id, stack.Id), o => o.ImmediateConsistency());

        string fakeOrganizationId = ObjectId.GenerateNewId().ToString();
        var orphanedEvents = _eventData.GenerateEvents(100, fakeOrganizationId, project.Id).ToList();
        orphanedEvents.ForEach(e => e.StackId = stack.Id);
        await _eventRepository.AddAsync(orphanedEvents, o => o.ImmediateConsistency());

        Assert.Equal(150, await _eventRepository.CountAsync(o => o.IncludeSoftDeletes().ImmediateConsistency()));

        // Act
        await _job.RunAsync(TestCancellationToken);

        // Assert
        Assert.Equal(50, await _eventRepository.CountAsync(o => o.IncludeSoftDeletes().ImmediateConsistency()));
    }

    [Fact]
    public async Task DeleteOrphanedEventsByOrganization_TwoTenantsOneDeleted_OnlyDeletesOrphanedTenantEvents()
    {
        // Arrange
        var organization1 = _organizationData.GenerateOrganization(_billingManager, _plans, id: TestConstants.OrganizationId);
        await _organizationRepository.AddAsync(organization1, o => o.ImmediateConsistency());

        var project1 = _projectData.GenerateProject(id: TestConstants.ProjectId, organizationId: organization1.Id);
        await _projectRepository.AddAsync(project1, o => o.ImmediateConsistency());

        var stack1 = _stackData.GenerateStack(id: TestConstants.StackId, organizationId: organization1.Id, projectId: project1.Id);
        await _stackRepository.AddAsync(stack1, o => o.ImmediateConsistency());

        var validEvents = _eventData.GenerateEvents(120, organization1.Id, project1.Id, stack1.Id).ToList();
        string ghostOrganizationId = ObjectId.GenerateNewId().ToString();
        string ghostProjectId = ObjectId.GenerateNewId().ToString();
        string ghostStackId = ObjectId.GenerateNewId().ToString();
        var ghostEvents = _eventData.GenerateEvents(80, ghostOrganizationId, ghostProjectId, ghostStackId).ToList();

        await _eventRepository.AddAsync(validEvents.Concat(ghostEvents), o => o.ImmediateConsistency());

        // Act
        await _job.RunAsync(TestCancellationToken);

        // Assert
        var totalAfter = await _eventRepository.CountAsync(o => o.IncludeSoftDeletes().ImmediateConsistency());
        Assert.Equal(120, totalAfter);
    }

    [Fact]
    public async Task DeleteOrphanedEventsByOrganization_WithSoftDeletedOrganization_DeletesOrphanedEvents()
    {
        // Arrange
        var validOrganization = _organizationData.GenerateSampleOrganization(_billingManager, _plans);
        await _organizationRepository.AddAsync(validOrganization, o => o.ImmediateConsistency());
        var project = await _projectRepository.AddAsync(_projectData.GenerateSampleProject(), o => o.ImmediateConsistency());
        var stack = await _stackRepository.AddAsync(_stackData.GenerateSampleStack(), o => o.ImmediateConsistency());
        await _eventRepository.AddAsync(_eventData.GenerateEvents(50, validOrganization.Id, project.Id, stack.Id), o => o.ImmediateConsistency());

        var softDeletedOrganization = _organizationData.GenerateOrganization(_billingManager, _plans, generateId: true);
        softDeletedOrganization.IsDeleted = true;
        softDeletedOrganization = await _organizationRepository.AddAsync(softDeletedOrganization, o => o.ImmediateConsistency());
        var orphanedEvents = _eventData.GenerateEvents(100, softDeletedOrganization.Id, project.Id).ToList();
        orphanedEvents.ForEach(e => e.StackId = stack.Id);
        await _eventRepository.AddAsync(orphanedEvents, o => o.ImmediateConsistency());

        Assert.Equal(150, await _eventRepository.CountAsync(o => o.IncludeSoftDeletes().ImmediateConsistency()));

        // Act
        await _job.RunAsync(TestCancellationToken);

        // Assert
        Assert.Equal(50, await _eventRepository.CountAsync(o => o.IncludeSoftDeletes().ImmediateConsistency()));
    }

    [Fact]
    public async Task FixDuplicateStacks_WithDuplicatesAcrossTenants_MergesCorrectly()
    {
        // Arrange
        var organization = _organizationData.GenerateOrganization(_billingManager, _plans, id: TestConstants.OrganizationId);
        await _organizationRepository.AddAsync(organization, o => o.ImmediateConsistency());

        var project = _projectData.GenerateProject(id: TestConstants.ProjectId, organizationId: organization.Id);
        await _projectRepository.AddAsync(project, o => o.ImmediateConsistency());

        const string signatureHash = "abc123def456";
        var stack1 = _stackData.GenerateStack(generateId: true, organizationId: organization.Id, projectId: project.Id, signatureHash: signatureHash);
        stack1.CreatedUtc = DateTime.UtcNow.AddDays(-10);
        stack1.TotalOccurrences = 5;
        var stack2 = _stackData.GenerateStack(generateId: true, organizationId: organization.Id, projectId: project.Id, signatureHash: signatureHash);
        stack2.CreatedUtc = DateTime.UtcNow.AddDays(-5);
        stack2.TotalOccurrences = 10;
        await _stackRepository.AddAsync([stack1, stack2], o => o.ImmediateConsistency());

        var events1 = _eventData.GenerateEvents(3, organization.Id, project.Id, stack1.Id).ToList();
        var events2 = _eventData.GenerateEvents(7, organization.Id, project.Id, stack2.Id).ToList();
        await _eventRepository.AddAsync(events1.Concat(events2), o => o.ImmediateConsistency());

        // Act
        await _job.RunAsync(TestCancellationToken);

        // Assert
        await RefreshDataAsync();
        var allStacks = await _stackRepository.GetAllAsync(o => o.IncludeSoftDeletes());
        var activeStacks = allStacks.Documents.Where(s => !s.IsDeleted).ToList();
        var deletedStacks = allStacks.Documents.Where(s => s.IsDeleted).ToList();
        Assert.Single(activeStacks);
        Assert.Single(deletedStacks);

        var allEvents = await _eventRepository.GetAllAsync();
        Assert.Equal(10, allEvents.Total);
        Assert.All(allEvents.Documents, e => Assert.Equal(activeStacks[0].Id, e.StackId));
    }

    [Fact]
    public async Task FixDuplicateStacks_WithDuplicateSignatures_MergesIntoMostPopularStack()
    {
        // Arrange
        var organization = _organizationData.GenerateSampleOrganization(_billingManager, _plans);
        await _organizationRepository.AddAsync(organization, o => o.ImmediateConsistency());
        var project = await _projectRepository.AddAsync(_projectData.GenerateSampleProject(), o => o.ImmediateConsistency());

        var originalStack = _stackData.GenerateStack(generateId: true, organizationId: organization.Id, projectId: project.Id);
        originalStack.TotalOccurrences = 100;
        var duplicateStack = originalStack.DeepClone();
        duplicateStack.Id = ObjectId.GenerateNewId().ToString();
        duplicateStack.TotalOccurrences = 10;

        originalStack = await _stackRepository.AddAsync(originalStack, o => o.ImmediateConsistency());
        duplicateStack = await _stackRepository.AddAsync(duplicateStack, o => o.ImmediateConsistency());
        await _eventRepository.AddAsync(_eventData.GenerateEvents(100, organization.Id, project.Id, originalStack.Id), o => o.ImmediateConsistency());
        await _eventRepository.AddAsync(_eventData.GenerateEvents(10, organization.Id, project.Id, duplicateStack.Id), o => o.ImmediateConsistency());

        // Act
        await _job.RunAsync(TestCancellationToken);
        await RefreshDataAsync();

        // Assert
        var updatedOriginal = await _stackRepository.GetByIdAsync(originalStack.Id, o => o.IncludeSoftDeletes());
        var updatedDuplicate = await _stackRepository.GetByIdAsync(duplicateStack.Id, o => o.IncludeSoftDeletes());

        Assert.NotNull(updatedOriginal);
        Assert.NotNull(updatedDuplicate);
        Assert.False(updatedOriginal.IsDeleted);
        Assert.True(updatedDuplicate.IsDeleted);
        Assert.Equal(110, updatedOriginal.TotalOccurrences);

        Assert.Equal(110, await _eventRepository.CountAsync(o => o.IncludeSoftDeletes().ImmediateConsistency()));
        Assert.Equal(110, await _eventRepository.CountAsync(q => q.Stack(originalStack.Id)));
        Assert.Equal(0, await _eventRepository.CountAsync(q => q.Stack(duplicateStack.Id)));
    }

    [Fact]
    public async Task FixDuplicateStacks_AfterPartialTargetMerge_DoesNotDoubleApplyMetadata()
    {
        // Arrange
        var organization = _organizationData.GenerateSampleOrganization(_billingManager, _plans);
        await _organizationRepository.AddAsync(organization, o => o.ImmediateConsistency());
        var project = await _projectRepository.AddAsync(_projectData.GenerateSampleProject(), o => o.ImmediateConsistency());

        var targetStack = _stackData.GenerateStack(generateId: true, organizationId: organization.Id, projectId: project.Id);
        targetStack.TotalOccurrences = 100;
        var sourceStack = targetStack.DeepClone();
        sourceStack.Id = ObjectId.GenerateNewId().ToString();
        sourceStack.TotalOccurrences = 10;
        await _stackRepository.AddAsync([targetStack, sourceStack], o => o.ImmediateConsistency());
        await _eventRepository.AddAsync(_eventData.GenerateEvents(100, organization.Id, project.Id, targetStack.Id), o => o.ImmediateConsistency());
        await _eventRepository.AddAsync(_eventData.GenerateEvents(10, organization.Id, project.Id, sourceStack.Id), o => o.ImmediateConsistency());

        // Simulate a prior run that merged the target but failed before hiding the source.
        await _stackRepository.MergeDuplicateStackAsync(targetStack.Id, sourceStack);

        // Act
        await _job.RunAsync(TestCancellationToken);
        await RefreshDataAsync();

        // Assert
        var updatedTarget = await _stackRepository.GetByIdAsync(targetStack.Id, o => o.IncludeSoftDeletes());
        var updatedSource = await _stackRepository.GetByIdAsync(sourceStack.Id, o => o.IncludeSoftDeletes());
        Assert.NotNull(updatedTarget);
        Assert.NotNull(updatedSource);
        Assert.Equal(110, updatedTarget.TotalOccurrences);
        Assert.False(updatedTarget.IsDeleted);
        Assert.True(updatedSource.IsDeleted);
        Assert.Equal(110, await _eventRepository.CountAsync(q => q.Stack(targetStack.Id), o => o.ImmediateConsistency()));
        Assert.Equal(0, await _eventRepository.CountAsync(q => q.Stack(sourceStack.Id)));
    }

    [Fact]
    public async Task DeleteOrphanedEventsByStack_WithRedirectedSource_ReassignsLateEvents()
    {
        var organization = _organizationData.GenerateSampleOrganization(_billingManager, _plans);
        await _organizationRepository.AddAsync(organization, o => o.ImmediateConsistency());
        var project = await _projectRepository.AddAsync(_projectData.GenerateSampleProject(), o => o.ImmediateConsistency());

        var targetStack = _stackData.GenerateStack(generateId: true, organizationId: organization.Id, projectId: project.Id);
        targetStack.TotalOccurrences = 100;
        var sourceStack = targetStack.DeepClone();
        sourceStack.Id = ObjectId.GenerateNewId().ToString();
        sourceStack.TotalOccurrences = 10;
        await _stackRepository.AddAsync([targetStack, sourceStack], o => o.ImmediateConsistency());
        await _stackRepository.SetDuplicateStackRedirectAsync(sourceStack, targetStack.Id, isDeleted: true);

        await _eventRepository.AddAsync(
            _eventData.GenerateEvents(10, organization.Id, project.Id, sourceStack.Id),
            o => o.ImmediateConsistency());

        await RefreshDataAsync();
        await _job.RunAsync(TestCancellationToken);
        await RefreshDataAsync();

        Assert.Equal(10, await _eventRepository.CountAsync(q => q.Stack(targetStack.Id), o => o.ImmediateConsistency()));
        Assert.Equal(0, await _eventRepository.CountAsync(q => q.Stack(sourceStack.Id), o => o.ImmediateConsistency()));

        var updatedTarget = await _stackRepository.GetByIdAsync(targetStack.Id, o => o.ImmediateConsistency());
        Assert.NotNull(updatedTarget);
        Assert.Equal(110, updatedTarget.TotalOccurrences);

        var redirectTombstone = await _stackRepository.GetByIdAsync(sourceStack.Id, o => o.IncludeSoftDeletes());
        Assert.NotNull(redirectTombstone);
        Assert.True(redirectTombstone.IsDeleted);
        Assert.Equal(targetStack.Id, redirectTombstone.RedirectToStackId);
        Assert.False(redirectTombstone.NeedsRedirectReconciliation);
    }

    [Fact]
    public async Task DeleteOrphanedEventsByStack_WithLateCounterAndNoSourceEvents_ReconcilesTombstone()
    {
        var organization = _organizationData.GenerateSampleOrganization(_billingManager, _plans);
        await _organizationRepository.AddAsync(organization, o => o.ImmediateConsistency());
        var project = await _projectRepository.AddAsync(_projectData.GenerateSampleProject(), o => o.ImmediateConsistency());

        var targetStack = _stackData.GenerateStack(generateId: true, organizationId: organization.Id, projectId: project.Id);
        targetStack.TotalOccurrences = 100;
        var sourceStack = targetStack.DeepClone();
        sourceStack.Id = ObjectId.GenerateNewId().ToString();
        sourceStack.TotalOccurrences = 10;
        await _stackRepository.AddAsync([targetStack, sourceStack], o => o.ImmediateConsistency());
        await _stackRepository.MergeDuplicateStackAsync(targetStack.Id, sourceStack);
        await _stackRepository.SetDuplicateStackRedirectAsync(sourceStack, targetStack.Id, isDeleted: true);

        await _stackRepository.IncrementEventCounterAsync(
            sourceStack.OrganizationId,
            sourceStack.ProjectId,
            sourceStack.Id,
            sourceStack.FirstOccurrence,
            sourceStack.LastOccurrence.AddMinutes(1),
            5,
            sendNotifications: false);

        var incrementedSource = await _stackRepository.GetByIdAsync(sourceStack.Id, o => o.IncludeSoftDeletes().ImmediateConsistency());
        Assert.NotNull(incrementedSource);
        Assert.Equal(15, incrementedSource.TotalOccurrences);

        Assert.Equal(0, await _eventRepository.CountAsync(q => q.Stack(sourceStack.Id), o => o.ImmediateConsistency()));

        await RefreshDataAsync();
        await _job.RunAsync(TestCancellationToken);

        var updatedTarget = await _stackRepository.GetByIdAsync(targetStack.Id, o => o.ImmediateConsistency());
        Assert.NotNull(updatedTarget);
        Assert.Equal(115, updatedTarget.TotalOccurrences);

        var reconciledSource = await _stackRepository.GetByIdAsync(sourceStack.Id, o => o.IncludeSoftDeletes().ImmediateConsistency());
        Assert.NotNull(reconciledSource);
        Assert.False(reconciledSource.NeedsRedirectReconciliation);
        Assert.Empty((await _stackRepository.GetRedirectedStacksNeedingReconciliationAsync()).Documents);

        DateTime targetUpdatedUtc = updatedTarget.UpdatedUtc;
        await _job.RunAsync(TestCancellationToken);
        updatedTarget = await _stackRepository.GetByIdAsync(targetStack.Id, o => o.ImmediateConsistency());
        Assert.NotNull(updatedTarget);
        Assert.Equal(targetUpdatedUtc, updatedTarget.UpdatedUtc);
    }

    [Fact]
    public async Task FixDuplicateStacks_NoDuplicates_DoesNotModifyAnything()
    {
        // Arrange
        var organization1 = _organizationData.GenerateOrganization(_billingManager, _plans, id: TestConstants.OrganizationId);
        var organization2 = _organizationData.GenerateOrganization(_billingManager, _plans, id: TestConstants.OrganizationId2);
        await _organizationRepository.AddAsync([organization1, organization2], o => o.ImmediateConsistency());

        var project1 = _projectData.GenerateProject(id: TestConstants.ProjectId, organizationId: organization1.Id);
        var project2 = _projectData.GenerateProject(generateId: true, organizationId: organization2.Id);
        await _projectRepository.AddAsync([project1, project2], o => o.ImmediateConsistency());

        var stack1 = _stackData.GenerateStack(generateId: true, organizationId: organization1.Id, projectId: project1.Id, signatureHash: "unique_hash_1");
        var stack2 = _stackData.GenerateStack(generateId: true, organizationId: organization2.Id, projectId: project2.Id, signatureHash: "unique_hash_2");
        await _stackRepository.AddAsync([stack1, stack2], o => o.ImmediateConsistency());

        var events1 = _eventData.GenerateEvents(20, organization1.Id, project1.Id, stack1.Id).ToList();
        var events2 = _eventData.GenerateEvents(20, organization2.Id, project2.Id, stack2.Id).ToList();
        await _eventRepository.AddAsync(events1.Concat(events2), o => o.ImmediateConsistency());

        // Act
        await _job.RunAsync(TestCancellationToken);

        // Assert
        var allStacks = await _stackRepository.GetAllAsync(o => o.IncludeSoftDeletes());
        Assert.Equal(2, allStacks.Total);
        Assert.All(allStacks.Documents, s => Assert.False(s.IsDeleted));

        var totalEvents = await _eventRepository.CountAsync(o => o.IncludeSoftDeletes().ImmediateConsistency());
        Assert.Equal(40, totalEvents);
    }

    [Fact]
    public async Task FixDuplicateStacks_WithNoEvents_KeepsOldestStack()
    {
        // Arrange
        var organization = _organizationData.GenerateSampleOrganization(_billingManager, _plans);
        await _organizationRepository.AddAsync(organization, o => o.ImmediateConsistency());
        var project = await _projectRepository.AddAsync(_projectData.GenerateSampleProject(), o => o.ImmediateConsistency());

        var originalStack = _stackData.GenerateStack(generateId: true, organizationId: organization.Id, projectId: project.Id);
        originalStack.CreatedUtc = DateTime.UtcNow.AddMinutes(-10);
        var duplicateStack = originalStack.DeepClone();
        duplicateStack.Id = ObjectId.GenerateNewId().ToString();
        duplicateStack.CreatedUtc = originalStack.CreatedUtc.AddMinutes(1);

        originalStack = await _stackRepository.AddAsync(originalStack, o => o.ImmediateConsistency());
        duplicateStack = await _stackRepository.AddAsync(duplicateStack, o => o.ImmediateConsistency());

        // Act
        await _job.RunAsync(TestCancellationToken);
        await RefreshDataAsync();

        // Assert
        var updatedOriginal = await _stackRepository.GetByIdAsync(originalStack.Id, o => o.IncludeSoftDeletes());
        var updatedDuplicate = await _stackRepository.GetByIdAsync(duplicateStack.Id, o => o.IncludeSoftDeletes());

        Assert.NotNull(updatedOriginal);
        Assert.NotNull(updatedDuplicate);
        Assert.False(updatedOriginal.IsDeleted);
        Assert.True(updatedDuplicate.IsDeleted);
    }

    [Fact]
    public async Task FixDuplicateStacks_WithClosedEventIndex_KeepsAllStacksActive()
    {
        // Arrange
        var organization = _organizationData.GenerateSampleOrganization(_billingManager, _plans);
        await _organizationRepository.AddAsync(organization, o => o.ImmediateConsistency());
        var project = await _projectRepository.AddAsync(_projectData.GenerateSampleProject(), o => o.ImmediateConsistency());

        var occurrenceDate = _configuration.TimeProvider.GetUtcNow().AddDays(-1);
        var originalStack = _stackData.GenerateStack(generateId: true, organizationId: organization.Id, projectId: project.Id);
        var duplicateStack = originalStack.DeepClone();
        duplicateStack.Id = ObjectId.GenerateNewId().ToString();
        await _stackRepository.AddAsync([originalStack, duplicateStack], o => o.ImmediateConsistency());
        await _eventRepository.AddAsync(
            Enumerable.Range(0, 10).Select(_ => _eventData.GenerateEvent(
                organization.Id, project.Id, duplicateStack.Id, occurrenceDate: occurrenceDate)),
            o => o.ImmediateConsistency());

        string eventIndex = _configuration.Events.GetVersionedIndex(occurrenceDate.UtcDateTime);
        var closeResponse = await _configuration.Client.Indices.CloseAsync(eventIndex, TestContext.Current.CancellationToken);
        Assert.True(closeResponse.IsValidResponse, closeResponse.DebugInformation);

        try
        {
            // Act
            await _job.RunAsync(TestCancellationToken);
        }
        finally
        {
            var openResponse = await _configuration.Client.Indices.OpenAsync(eventIndex, TestContext.Current.CancellationToken);
            Assert.True(openResponse.IsValidResponse, openResponse.DebugInformation);
        }

        // Assert
        var stacks = await _stackRepository.GetByIdsAsync(
            [originalStack.Id, duplicateStack.Id],
            o => o.IncludeSoftDeletes().ImmediateConsistency());
        Assert.Equal(2, stacks.Count);
        Assert.All(stacks, stack => Assert.False(stack.IsDeleted));
        Assert.Equal(10, await _eventRepository.CountAsync(q => q.Stack(duplicateStack.Id), o => o.ImmediateConsistency()));
    }

    [Fact]
    public async Task RunAsync_AllOrphanTypes_CleansUpCorrectly()
    {
        // Arrange
        var organization1 = _organizationData.GenerateOrganization(_billingManager, _plans, id: TestConstants.OrganizationId);
        await _organizationRepository.AddAsync(organization1, o => o.ImmediateConsistency());

        var project1 = _projectData.GenerateProject(id: TestConstants.ProjectId, organizationId: organization1.Id);
        await _projectRepository.AddAsync(project1, o => o.ImmediateConsistency());

        var validStack = _stackData.GenerateStack(id: TestConstants.StackId, organizationId: organization1.Id, projectId: project1.Id);
        await _stackRepository.AddAsync(validStack, o => o.ImmediateConsistency());

        var validEvents = _eventData.GenerateEvents(100, organization1.Id, project1.Id, validStack.Id).ToList();
        var orphanedByStack = _eventData.GenerateEvents(25, organization1.Id, project1.Id, ObjectId.GenerateNewId().ToString()).ToList();
        var orphanedByProject = _eventData.GenerateEvents(25, organization1.Id, ObjectId.GenerateNewId().ToString(), ObjectId.GenerateNewId().ToString()).ToList();
        var orphanedByOrganization = _eventData.GenerateEvents(25, ObjectId.GenerateNewId().ToString(), ObjectId.GenerateNewId().ToString(), ObjectId.GenerateNewId().ToString()).ToList();

        await _eventRepository.AddAsync(validEvents.Concat(orphanedByStack).Concat(orphanedByProject).Concat(orphanedByOrganization), o => o.ImmediateConsistency());

        var totalBefore = await _eventRepository.CountAsync(o => o.IncludeSoftDeletes().ImmediateConsistency());
        Assert.Equal(175, totalBefore);

        // Act
        await _job.RunAsync(TestCancellationToken);

        // Assert
        var totalAfter = await _eventRepository.CountAsync(o => o.IncludeSoftDeletes().ImmediateConsistency());
        Assert.Equal(100, totalAfter);
    }

    [Fact]
    public async Task RunAsync_NoOrphans_PreservesEverything()
    {
        // Arrange
        var organization1 = _organizationData.GenerateOrganization(_billingManager, _plans, id: TestConstants.OrganizationId);
        var organization2 = _organizationData.GenerateOrganization(_billingManager, _plans, id: TestConstants.OrganizationId2);
        await _organizationRepository.AddAsync([organization1, organization2], o => o.ImmediateConsistency());

        var project1 = _projectData.GenerateProject(id: TestConstants.ProjectId, organizationId: organization1.Id);
        var project2 = _projectData.GenerateProject(generateId: true, organizationId: organization2.Id);
        await _projectRepository.AddAsync([project1, project2], o => o.ImmediateConsistency());

        var stack1 = _stackData.GenerateStack(id: TestConstants.StackId, organizationId: organization1.Id, projectId: project1.Id);
        var stack2 = _stackData.GenerateStack(generateId: true, organizationId: organization2.Id, projectId: project2.Id);
        await _stackRepository.AddAsync([stack1, stack2], o => o.ImmediateConsistency());

        var events1 = _eventData.GenerateEvents(200, organization1.Id, project1.Id, stack1.Id).ToList();
        var events2 = _eventData.GenerateEvents(200, organization2.Id, project2.Id, stack2.Id).ToList();
        await _eventRepository.AddAsync(events1.Concat(events2), o => o.ImmediateConsistency());

        // Act
        await _job.RunAsync(TestCancellationToken);

        // Assert
        var totalAfter = await _eventRepository.CountAsync(o => o.IncludeSoftDeletes().ImmediateConsistency());
        Assert.Equal(400, totalAfter);
    }

    [Fact]
    public async Task RunAsync_EmptyDatabase_CompletesWithoutError()
    {
        // Arrange

        // Act
        await _job.RunAsync(TestCancellationToken);

        // Assert
        var totalAfter = await _eventRepository.CountAsync(o => o.IncludeSoftDeletes().ImmediateConsistency());
        Assert.Equal(0, totalAfter);
    }
}
