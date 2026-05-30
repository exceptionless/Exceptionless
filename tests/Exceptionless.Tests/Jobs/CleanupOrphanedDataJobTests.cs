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
        // Arrange
        var organization = _organizationData.GenerateSampleOrganization(_billingManager, _plans);
        await _organizationRepository.AddAsync(organization, o => o.ImmediateConsistency());
        var project = await _projectRepository.AddAsync(_projectData.GenerateSampleProject(), o => o.ImmediateConsistency());

        var stack = await _stackRepository.AddAsync(_stackData.GenerateSampleStack(), o => o.ImmediateConsistency());
        await _eventRepository.AddAsync(_eventData.GenerateEvents(50, organization.Id, project.Id, stack.Id), o => o.ImmediateConsistency());

        var orphanedEvents = _eventData.GenerateEvents(100, organization.Id, project.Id).ToList();
        orphanedEvents.ForEach(e => e.StackId = ObjectId.GenerateNewId().ToString());
        await _eventRepository.AddAsync(orphanedEvents, o => o.ImmediateConsistency());

        Assert.Equal(150, await _eventRepository.CountAsync(o => o.IncludeSoftDeletes()));

        // Act
        await _job.RunAsync(TestCancellationToken);
        await RefreshDataAsync();

        // Assert
        Assert.Equal(50, await _eventRepository.CountAsync(o => o.IncludeSoftDeletes()));
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

        // Each orphaned event points to a different non-existent stack (exercises batched partition logic)
        var orphanedEvents = _eventData.GenerateEvents(200, organization.Id, project.Id).ToList();
        var uniqueStackIds = Enumerable.Range(0, 200).Select(_ => ObjectId.GenerateNewId().ToString()).ToList();
        for (int i = 0; i < orphanedEvents.Count; i++)
            orphanedEvents[i].StackId = uniqueStackIds[i];
        await _eventRepository.AddAsync(orphanedEvents, o => o.ImmediateConsistency());

        Assert.Equal(250, await _eventRepository.CountAsync(o => o.IncludeSoftDeletes()));

        // Act
        await _job.RunAsync(TestCancellationToken);
        await RefreshDataAsync();

        // Assert
        Assert.Equal(50, await _eventRepository.CountAsync(o => o.IncludeSoftDeletes()));
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

        // Use a valid stack ID so the stack-cleanup phase doesn't delete these events
        // before the project-cleanup phase has a chance to process them.
        string fakeProjectId = ObjectId.GenerateNewId().ToString();
        var orphanedEvents = _eventData.GenerateEvents(100, organization.Id, fakeProjectId).ToList();
        orphanedEvents.ForEach(e => e.StackId = stack.Id);
        await _eventRepository.AddAsync(orphanedEvents, o => o.ImmediateConsistency());

        Assert.Equal(150, await _eventRepository.CountAsync(o => o.IncludeSoftDeletes()));

        // Act
        await _job.RunAsync(TestCancellationToken);
        await RefreshDataAsync();

        // Assert
        Assert.Equal(50, await _eventRepository.CountAsync(o => o.IncludeSoftDeletes()));
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

        // Use a valid stack ID so the stack-cleanup phase doesn't delete these events
        // before the organization-cleanup phase has a chance to process them.
        string fakeOrgId = ObjectId.GenerateNewId().ToString();
        var orphanedEvents = _eventData.GenerateEvents(100, fakeOrgId, project.Id).ToList();
        orphanedEvents.ForEach(e => e.StackId = stack.Id);
        await _eventRepository.AddAsync(orphanedEvents, o => o.ImmediateConsistency());

        Assert.Equal(150, await _eventRepository.CountAsync(o => o.IncludeSoftDeletes()));

        // Act
        await _job.RunAsync(TestCancellationToken);
        await RefreshDataAsync();

        // Assert
        Assert.Equal(50, await _eventRepository.CountAsync(o => o.IncludeSoftDeletes()));
    }

    [Fact]
    public async Task DeleteOrphanedEvents_WithAllValidParents_DeletesNothing()
    {
        // Arrange
        var organization = _organizationData.GenerateSampleOrganization(_billingManager, _plans);
        await _organizationRepository.AddAsync(organization, o => o.ImmediateConsistency());
        var project = await _projectRepository.AddAsync(_projectData.GenerateSampleProject(), o => o.ImmediateConsistency());
        var stack = await _stackRepository.AddAsync(_stackData.GenerateSampleStack(), o => o.ImmediateConsistency());
        await _eventRepository.AddAsync(_eventData.GenerateEvents(100, organization.Id, project.Id, stack.Id), o => o.ImmediateConsistency());

        Assert.Equal(100, await _eventRepository.CountAsync(o => o.IncludeSoftDeletes()));

        // Act
        await _job.RunAsync(TestCancellationToken);
        await RefreshDataAsync();

        // Assert - no events should be deleted since all parents exist
        Assert.Equal(100, await _eventRepository.CountAsync(o => o.IncludeSoftDeletes()));
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
        var orphanedEvents = _eventData.GenerateEvents(100, organization.Id, project.Id, softDeletedStack.Id).ToList();
        await _eventRepository.AddAsync(orphanedEvents, o => o.ImmediateConsistency());

        Assert.Equal(150, await _eventRepository.CountAsync(o => o.IncludeSoftDeletes()));

        // Act
        await _job.RunAsync(TestCancellationToken);
        await RefreshDataAsync();

        // Assert - events pointing to the soft-deleted stack should be cleaned up
        Assert.Equal(50, await _eventRepository.CountAsync(o => o.IncludeSoftDeletes()));
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
        // Use the valid stack ID so that the stack-cleanup phase doesn't eat these events
        // before the project-cleanup phase has a chance to process them.
        orphanedEvents.ForEach(e => e.StackId = stack.Id);
        await _eventRepository.AddAsync(orphanedEvents, o => o.ImmediateConsistency());

        Assert.Equal(150, await _eventRepository.CountAsync(o => o.IncludeSoftDeletes()));

        // Act
        await _job.RunAsync(TestCancellationToken);
        await RefreshDataAsync();

        // Assert - events pointing to the soft-deleted project should be cleaned up
        Assert.Equal(50, await _eventRepository.CountAsync(o => o.IncludeSoftDeletes()));
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

        var softDeletedOrg = _organizationData.GenerateOrganization(_billingManager, _plans, generateId: true);
        softDeletedOrg.IsDeleted = true;
        softDeletedOrg = await _organizationRepository.AddAsync(softDeletedOrg, o => o.ImmediateConsistency());
        var orphanedEvents = _eventData.GenerateEvents(100, softDeletedOrg.Id, project.Id).ToList();
        // Use the valid stack ID so that the stack-cleanup phase doesn't eat these events
        // before the organization-cleanup phase has a chance to process them.
        orphanedEvents.ForEach(e => e.StackId = stack.Id);
        await _eventRepository.AddAsync(orphanedEvents, o => o.ImmediateConsistency());

        Assert.Equal(150, await _eventRepository.CountAsync(o => o.IncludeSoftDeletes()));

        // Act
        await _job.RunAsync(TestCancellationToken);
        await RefreshDataAsync();

        // Assert - events pointing to the soft-deleted organization should be cleaned up
        Assert.Equal(50, await _eventRepository.CountAsync(o => o.IncludeSoftDeletes()));
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

        // Assert - the stack with more events is kept; duplicate is soft-deleted; events reassigned
        var updatedOriginal = await _stackRepository.GetByIdAsync(originalStack.Id, o => o.IncludeSoftDeletes());
        var updatedDuplicate = await _stackRepository.GetByIdAsync(duplicateStack.Id, o => o.IncludeSoftDeletes());

        Assert.NotNull(updatedOriginal);
        Assert.NotNull(updatedDuplicate);
        Assert.False(updatedOriginal.IsDeleted);
        Assert.True(updatedDuplicate.IsDeleted);
        Assert.Equal(110, updatedOriginal.TotalOccurrences);

        Assert.Equal(110, await _eventRepository.CountAsync(o => o.IncludeSoftDeletes()));
        Assert.Equal(110, await _eventRepository.CountAsync(q => q.Stack(originalStack.Id)));
        Assert.Equal(0, await _eventRepository.CountAsync(q => q.Stack(duplicateStack.Id)));
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

        // Assert - oldest stack should be kept when there are no events
        var updatedOriginal = await _stackRepository.GetByIdAsync(originalStack.Id, o => o.IncludeSoftDeletes());
        var updatedDuplicate = await _stackRepository.GetByIdAsync(duplicateStack.Id, o => o.IncludeSoftDeletes());

        Assert.NotNull(updatedOriginal);
        Assert.NotNull(updatedDuplicate);
        Assert.False(updatedOriginal.IsDeleted);
        Assert.True(updatedDuplicate.IsDeleted);
    }
}
