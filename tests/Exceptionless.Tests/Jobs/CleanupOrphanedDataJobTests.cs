using Exceptionless.Core.Billing;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
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
    }

    [Fact]
    public async Task DeleteOrphanedEventsByStack_WithMissingStacks_DeletesOrphanedEvents()
    {
        var organization = _organizationData.GenerateSampleOrganization(_billingManager, _plans);
        await _organizationRepository.AddAsync(organization, o => o.ImmediateConsistency());
        var project = await _projectRepository.AddAsync(_projectData.GenerateSampleProject(), o => o.ImmediateConsistency());

        var stack = await _stackRepository.AddAsync(_stackData.GenerateSampleStack(), o => o.ImmediateConsistency());
        await _eventRepository.AddAsync(_eventData.GenerateEvents(50, organization.Id, project.Id, stack.Id), o => o.ImmediateConsistency());

        // Create orphaned events pointing to non-existent stacks
        var orphanedEvents = _eventData.GenerateEvents(100, organization.Id, project.Id).ToList();
        orphanedEvents.ForEach(e => e.StackId = ObjectId.GenerateNewId().ToString());
        await _eventRepository.AddAsync(orphanedEvents, o => o.ImmediateConsistency());

        var eventCount = await _eventRepository.CountAsync(o => o.IncludeSoftDeletes());
        Assert.Equal(150, eventCount);

        await _job.RunAsync(TestCancellationToken);
        await RefreshDataAsync();

        eventCount = await _eventRepository.CountAsync(o => o.IncludeSoftDeletes());
        Assert.Equal(50, eventCount);
    }

    [Fact]
    public async Task DeleteOrphanedEventsByStack_WithManyUniqueOrphanedStacks_DeletesAllOrphanedEvents()
    {
        var organization = _organizationData.GenerateSampleOrganization(_billingManager, _plans);
        await _organizationRepository.AddAsync(organization, o => o.ImmediateConsistency());
        var project = await _projectRepository.AddAsync(_projectData.GenerateSampleProject(), o => o.ImmediateConsistency());

        var stack = await _stackRepository.AddAsync(_stackData.GenerateSampleStack(), o => o.ImmediateConsistency());
        await _eventRepository.AddAsync(_eventData.GenerateEvents(50, organization.Id, project.Id, stack.Id), o => o.ImmediateConsistency());

        // Create orphaned events each pointing to different non-existent stacks (tests partition logic)
        var orphanedEvents = _eventData.GenerateEvents(200, organization.Id, project.Id).ToList();
        var uniqueStackIds = Enumerable.Range(0, 200).Select(_ => ObjectId.GenerateNewId().ToString()).ToList();
        for (int i = 0; i < orphanedEvents.Count; i++)
            orphanedEvents[i].StackId = uniqueStackIds[i];
        await _eventRepository.AddAsync(orphanedEvents, o => o.ImmediateConsistency());

        var eventCount = await _eventRepository.CountAsync(o => o.IncludeSoftDeletes());
        Assert.Equal(250, eventCount);

        await _job.RunAsync(TestCancellationToken);
        await RefreshDataAsync();

        eventCount = await _eventRepository.CountAsync(o => o.IncludeSoftDeletes());
        Assert.Equal(50, eventCount);
    }

    [Fact]
    public async Task DeleteOrphanedEventsByProject_WithMissingProjects_DeletesOrphanedEvents()
    {
        var organization = _organizationData.GenerateSampleOrganization(_billingManager, _plans);
        await _organizationRepository.AddAsync(organization, o => o.ImmediateConsistency());
        var project = await _projectRepository.AddAsync(_projectData.GenerateSampleProject(), o => o.ImmediateConsistency());
        var stack = await _stackRepository.AddAsync(_stackData.GenerateSampleStack(), o => o.ImmediateConsistency());
        await _eventRepository.AddAsync(_eventData.GenerateEvents(50, organization.Id, project.Id, stack.Id), o => o.ImmediateConsistency());

        // Create orphaned events pointing to a non-existent project
        string fakeProjectId = ObjectId.GenerateNewId().ToString();
        var orphanedEvents = _eventData.GenerateEvents(100, organization.Id, fakeProjectId).ToList();
        orphanedEvents.ForEach(e => e.StackId = ObjectId.GenerateNewId().ToString());
        await _eventRepository.AddAsync(orphanedEvents, o => o.ImmediateConsistency());

        var eventCount = await _eventRepository.CountAsync(o => o.IncludeSoftDeletes());
        Assert.Equal(150, eventCount);

        await _job.RunAsync(TestCancellationToken);
        await RefreshDataAsync();

        eventCount = await _eventRepository.CountAsync(o => o.IncludeSoftDeletes());
        Assert.Equal(50, eventCount);
    }

    [Fact]
    public async Task DeleteOrphanedEventsByOrganization_WithMissingOrganizations_DeletesOrphanedEvents()
    {
        var organization = _organizationData.GenerateSampleOrganization(_billingManager, _plans);
        await _organizationRepository.AddAsync(organization, o => o.ImmediateConsistency());
        var project = await _projectRepository.AddAsync(_projectData.GenerateSampleProject(), o => o.ImmediateConsistency());
        var stack = await _stackRepository.AddAsync(_stackData.GenerateSampleStack(), o => o.ImmediateConsistency());
        await _eventRepository.AddAsync(_eventData.GenerateEvents(50, organization.Id, project.Id, stack.Id), o => o.ImmediateConsistency());

        // Create orphaned events pointing to a non-existent organization
        string fakeOrgId = ObjectId.GenerateNewId().ToString();
        var orphanedEvents = _eventData.GenerateEvents(100, fakeOrgId, project.Id).ToList();
        orphanedEvents.ForEach(e => e.StackId = ObjectId.GenerateNewId().ToString());
        await _eventRepository.AddAsync(orphanedEvents, o => o.ImmediateConsistency());

        var eventCount = await _eventRepository.CountAsync(o => o.IncludeSoftDeletes());
        Assert.Equal(150, eventCount);

        await _job.RunAsync(TestCancellationToken);
        await RefreshDataAsync();

        eventCount = await _eventRepository.CountAsync(o => o.IncludeSoftDeletes());
        Assert.Equal(50, eventCount);
    }

    [Fact]
    public async Task DeleteOrphanedEvents_WithAllValidParents_DeletesNothing()
    {
        var organization = _organizationData.GenerateSampleOrganization(_billingManager, _plans);
        await _organizationRepository.AddAsync(organization, o => o.ImmediateConsistency());
        var project = await _projectRepository.AddAsync(_projectData.GenerateSampleProject(), o => o.ImmediateConsistency());
        var stack = await _stackRepository.AddAsync(_stackData.GenerateSampleStack(), o => o.ImmediateConsistency());
        await _eventRepository.AddAsync(_eventData.GenerateEvents(100, organization.Id, project.Id, stack.Id), o => o.ImmediateConsistency());

        var eventCount = await _eventRepository.CountAsync(o => o.IncludeSoftDeletes());
        Assert.Equal(100, eventCount);

        await _job.RunAsync(TestCancellationToken);
        await RefreshDataAsync();

        // No events should be deleted since all parents exist
        eventCount = await _eventRepository.CountAsync(o => o.IncludeSoftDeletes());
        Assert.Equal(100, eventCount);
    }

    [Fact]
    public async Task DeleteOrphanedEventsByStack_WithSoftDeletedStack_DeletesOrphanedEvents()
    {
        var organization = _organizationData.GenerateSampleOrganization(_billingManager, _plans);
        await _organizationRepository.AddAsync(organization, o => o.ImmediateConsistency());
        var project = await _projectRepository.AddAsync(_projectData.GenerateSampleProject(), o => o.ImmediateConsistency());

        // Create a valid stack with events
        var stack = await _stackRepository.AddAsync(_stackData.GenerateSampleStack(), o => o.ImmediateConsistency());
        await _eventRepository.AddAsync(_eventData.GenerateEvents(50, organization.Id, project.Id, stack.Id), o => o.ImmediateConsistency());

        // Create a soft-deleted stack with events still pointing to it
        var softDeletedStack = _stackData.GenerateStack(generateId: true, organizationId: organization.Id, projectId: project.Id);
        softDeletedStack.IsDeleted = true;
        softDeletedStack = await _stackRepository.AddAsync(softDeletedStack, o => o.ImmediateConsistency());
        var orphanedEvents = _eventData.GenerateEvents(100, organization.Id, project.Id, softDeletedStack.Id).ToList();
        await _eventRepository.AddAsync(orphanedEvents, o => o.ImmediateConsistency());

        var eventCount = await _eventRepository.CountAsync(o => o.IncludeSoftDeletes());
        Assert.Equal(150, eventCount);

        await _job.RunAsync(TestCancellationToken);
        await RefreshDataAsync();

        // Events pointing to the soft-deleted stack should be cleaned up
        eventCount = await _eventRepository.CountAsync(o => o.IncludeSoftDeletes());
        Assert.Equal(50, eventCount);
    }

    [Fact]
    public async Task DeleteOrphanedEventsByProject_WithSoftDeletedProject_DeletesOrphanedEvents()
    {
        var organization = _organizationData.GenerateSampleOrganization(_billingManager, _plans);
        await _organizationRepository.AddAsync(organization, o => o.ImmediateConsistency());

        // Create a valid project with events
        var project = await _projectRepository.AddAsync(_projectData.GenerateSampleProject(), o => o.ImmediateConsistency());
        var stack = await _stackRepository.AddAsync(_stackData.GenerateSampleStack(), o => o.ImmediateConsistency());
        await _eventRepository.AddAsync(_eventData.GenerateEvents(50, organization.Id, project.Id, stack.Id), o => o.ImmediateConsistency());

        // Create a soft-deleted project with events still pointing to it
        var softDeletedProject = _projectData.GenerateProject(generateId: true, organizationId: organization.Id);
        softDeletedProject.IsDeleted = true;
        softDeletedProject = await _projectRepository.AddAsync(softDeletedProject, o => o.ImmediateConsistency());
        var orphanedEvents = _eventData.GenerateEvents(100, organization.Id, softDeletedProject.Id).ToList();
        orphanedEvents.ForEach(e => e.StackId = ObjectId.GenerateNewId().ToString());
        await _eventRepository.AddAsync(orphanedEvents, o => o.ImmediateConsistency());

        var eventCount = await _eventRepository.CountAsync(o => o.IncludeSoftDeletes());
        Assert.Equal(150, eventCount);

        await _job.RunAsync(TestCancellationToken);
        await RefreshDataAsync();

        // Events pointing to the soft-deleted project should be cleaned up
        eventCount = await _eventRepository.CountAsync(o => o.IncludeSoftDeletes());
        Assert.Equal(50, eventCount);
    }

    [Fact]
    public async Task DeleteOrphanedEventsByOrganization_WithSoftDeletedOrganization_DeletesOrphanedEvents()
    {
        // Create a valid organization with events
        var validOrganization = _organizationData.GenerateSampleOrganization(_billingManager, _plans);
        await _organizationRepository.AddAsync(validOrganization, o => o.ImmediateConsistency());
        var project = await _projectRepository.AddAsync(_projectData.GenerateSampleProject(), o => o.ImmediateConsistency());
        var stack = await _stackRepository.AddAsync(_stackData.GenerateSampleStack(), o => o.ImmediateConsistency());
        await _eventRepository.AddAsync(_eventData.GenerateEvents(50, validOrganization.Id, project.Id, stack.Id), o => o.ImmediateConsistency());

        // Create a soft-deleted organization with events still pointing to it
        var softDeletedOrg = _organizationData.GenerateOrganization(_billingManager, _plans, generateId: true);
        softDeletedOrg.IsDeleted = true;
        softDeletedOrg = await _organizationRepository.AddAsync(softDeletedOrg, o => o.ImmediateConsistency());
        var orphanedEvents = _eventData.GenerateEvents(100, softDeletedOrg.Id, project.Id).ToList();
        orphanedEvents.ForEach(e => e.StackId = ObjectId.GenerateNewId().ToString());
        await _eventRepository.AddAsync(orphanedEvents, o => o.ImmediateConsistency());

        var eventCount = await _eventRepository.CountAsync(o => o.IncludeSoftDeletes());
        Assert.Equal(150, eventCount);

        await _job.RunAsync(TestCancellationToken);
        await RefreshDataAsync();

        // Events pointing to the soft-deleted organization should be cleaned up
        eventCount = await _eventRepository.CountAsync(o => o.IncludeSoftDeletes());
        Assert.Equal(50, eventCount);
    }

    [Fact]
    public async Task FixDuplicateStacks_WithDuplicateSignatures_MergesIntoMostPopularStack()
    {
        var organization = _organizationData.GenerateSampleOrganization(_billingManager, _plans);
        await _organizationRepository.AddAsync(organization, o => o.ImmediateConsistency());
        var project = await _projectRepository.AddAsync(_projectData.GenerateSampleProject(), o => o.ImmediateConsistency());

        // Create two stacks with the same duplicate signature
        var originalStack = _stackData.GenerateStack(generateId: true, organizationId: organization.Id, projectId: project.Id);
        originalStack.TotalOccurrences = 100;
        var duplicateStack = originalStack.DeepClone();
        duplicateStack.Id = ObjectId.GenerateNewId().ToString();
        duplicateStack.TotalOccurrences = 10;

        originalStack = await _stackRepository.AddAsync(originalStack, o => o.ImmediateConsistency());
        duplicateStack = await _stackRepository.AddAsync(duplicateStack, o => o.ImmediateConsistency());

        // Add events to both stacks
        await _eventRepository.AddAsync(_eventData.GenerateEvents(100, organization.Id, project.Id, originalStack.Id), o => o.ImmediateConsistency());
        await _eventRepository.AddAsync(_eventData.GenerateEvents(10, organization.Id, project.Id, duplicateStack.Id), o => o.ImmediateConsistency());

        await _job.RunAsync(TestCancellationToken);
        await RefreshDataAsync();

        // The stack with more events should be the target
        var updatedOriginal = await _stackRepository.GetByIdAsync(originalStack.Id, o => o.IncludeSoftDeletes());
        var updatedDuplicate = await _stackRepository.GetByIdAsync(duplicateStack.Id, o => o.IncludeSoftDeletes());

        Assert.NotNull(updatedOriginal);
        Assert.NotNull(updatedDuplicate);
        Assert.False(updatedOriginal.IsDeleted);
        Assert.True(updatedDuplicate.IsDeleted);
        Assert.Equal(110, updatedOriginal.TotalOccurrences);

        // All events should be on the target stack
        var eventCount = await _eventRepository.CountAsync(o => o.IncludeSoftDeletes());
        Assert.Equal(110, eventCount);
    }

    [Fact]
    public async Task FixDuplicateStacks_WithNoEvents_KeepsOldestStack()
    {
        var organization = _organizationData.GenerateSampleOrganization(_billingManager, _plans);
        await _organizationRepository.AddAsync(organization, o => o.ImmediateConsistency());
        var project = await _projectRepository.AddAsync(_projectData.GenerateSampleProject(), o => o.ImmediateConsistency());

        // Create two stacks with the same duplicate signature but no events
        var originalStack = _stackData.GenerateStack(generateId: true, organizationId: organization.Id, projectId: project.Id);
        originalStack.CreatedUtc = DateTime.UtcNow.AddMinutes(-10);
        var duplicateStack = originalStack.DeepClone();
        duplicateStack.Id = ObjectId.GenerateNewId().ToString();
        duplicateStack.CreatedUtc = originalStack.CreatedUtc.AddMinutes(1);

        originalStack = await _stackRepository.AddAsync(originalStack, o => o.ImmediateConsistency());
        duplicateStack = await _stackRepository.AddAsync(duplicateStack, o => o.ImmediateConsistency());

        await _job.RunAsync(TestCancellationToken);
        await RefreshDataAsync();

        // Oldest stack should be kept when there are no events
        var updatedOriginal = await _stackRepository.GetByIdAsync(originalStack.Id, o => o.IncludeSoftDeletes());
        var updatedDuplicate = await _stackRepository.GetByIdAsync(duplicateStack.Id, o => o.IncludeSoftDeletes());

        Assert.NotNull(updatedOriginal);
        Assert.NotNull(updatedDuplicate);
        Assert.False(updatedOriginal.IsDeleted);
        Assert.True(updatedDuplicate.IsDeleted);
    }
}
