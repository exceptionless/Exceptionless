using Exceptionless.Core;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Tests.Utility;
using Foundatio.Repositories;
using Foundatio.Repositories.Utility;
using Xunit;

namespace Exceptionless.Tests.Jobs;

public class CleanupDataJobTests : IntegrationTestsBase
{
    private readonly CleanupDataJob _job;
    private readonly UsageService _usageService;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly OrganizationData _organizationData;
    private readonly IProjectRepository _projectRepository;
    private readonly ProjectData _projectData;
    private readonly IStackRepository _stackRepository;
    private readonly StackData _stackData;
    private readonly IEventRepository _eventRepository;
    private readonly EventData _eventData;
    private readonly ITokenRepository _tokenRepository;
    private readonly TokenData _tokenData;
    private readonly BillingManager _billingManager;
    private readonly BillingPlans _plans;

    public CleanupDataJobTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _job = GetService<CleanupDataJob>();
        _usageService = GetService<UsageService>();
        _organizationData = GetService<OrganizationData>();
        _organizationRepository = GetService<IOrganizationRepository>();
        _projectData = GetService<ProjectData>();
        _projectRepository = GetService<IProjectRepository>();
        _stackData = GetService<StackData>();
        _stackRepository = GetService<IStackRepository>();
        _eventData = GetService<EventData>();
        _eventRepository = GetService<IEventRepository>();
        _tokenData = GetService<TokenData>();
        _tokenRepository = GetService<ITokenRepository>();
        _billingManager = GetService<BillingManager>();
        _plans = GetService<BillingPlans>();
    }

    [Fact]
    public async Task CanCleanupSuspendedTokens()
    {
        var organization = _organizationData.GenerateSampleOrganization(_billingManager, _plans);
        organization.IsSuspended = true;
        organization.SuspensionDate = DateTime.UtcNow;
        organization.SuspendedByUserId = TestConstants.UserId;
        organization.SuspensionCode = Core.Models.SuspensionCode.Billing;
        organization.SuspensionNotes = "blah";
        await _organizationRepository.AddAsync(organization, o => o.ImmediateConsistency());

        var project = await _projectRepository.AddAsync(_projectData.GenerateSampleProject());
        var token = await _tokenRepository.AddAsync(_tokenData.GenerateSampleApiKeyToken(), o => o.ImmediateConsistency());
        Assert.False(token.IsSuspended);

        await _job.RunAsync(TestCancellationToken);

        token = await _tokenRepository.GetByIdAsync(token.Id);
        Assert.NotNull(token);
        Assert.True(token.IsSuspended);
    }

    [Fact]
    public async Task CanCleanupSoftDeletedOrganization()
    {
        var organization = _organizationData.GenerateSampleOrganization(_billingManager, _plans);
        organization.IsDeleted = true;
        await _organizationRepository.AddAsync(organization, o => o.ImmediateConsistency());

        var project = await _projectRepository.AddAsync(_projectData.GenerateSampleProject(), o => o.ImmediateConsistency());
        var stack = await _stackRepository.AddAsync(_stackData.GenerateSampleStack(), o => o.ImmediateConsistency());
        var persistentEvent = await _eventRepository.AddAsync(_eventData.GenerateEvent(organization.Id, project.Id, stack.Id), o => o.ImmediateConsistency());

        await _job.RunAsync(TestCancellationToken);

        Assert.Null(await _organizationRepository.GetByIdAsync(organization.Id, o => o.IncludeSoftDeletes()));
        Assert.Null(await _projectRepository.GetByIdAsync(project.Id, o => o.IncludeSoftDeletes()));
        Assert.Null(await _stackRepository.GetByIdAsync(stack.Id, o => o.IncludeSoftDeletes()));
        Assert.Null(await _eventRepository.GetByIdAsync(persistentEvent.Id, o => o.IncludeSoftDeletes()));
    }

    [Fact]
    public async Task CanCleanupSoftDeletedProject()
    {
        var organization = await _organizationRepository.AddAsync(_organizationData.GenerateSampleOrganization(_billingManager, _plans), o => o.ImmediateConsistency());

        var project = _projectData.GenerateSampleProject();
        project.IsDeleted = true;
        await _projectRepository.AddAsync(project, o => o.ImmediateConsistency());

        var stack = await _stackRepository.AddAsync(_stackData.GenerateSampleStack(), o => o.ImmediateConsistency());
        var persistentEvent = await _eventRepository.AddAsync(_eventData.GenerateEvent(organization.Id, project.Id, stack.Id), o => o.ImmediateConsistency());

        await _job.RunAsync(TestCancellationToken);

        Assert.NotNull(await _organizationRepository.GetByIdAsync(organization.Id));
        Assert.Null(await _projectRepository.GetByIdAsync(project.Id, o => o.IncludeSoftDeletes()));
        Assert.Null(await _stackRepository.GetByIdAsync(stack.Id, o => o.IncludeSoftDeletes()));
        Assert.Null(await _eventRepository.GetByIdAsync(persistentEvent.Id, o => o.IncludeSoftDeletes()));
    }

    [Fact]
    public async Task CanCleanupSoftDeletedStack()
    {
        var organization = await _organizationRepository.AddAsync(_organizationData.GenerateSampleOrganization(_billingManager, _plans), o => o.ImmediateConsistency());
        var project = await _projectRepository.AddAsync(_projectData.GenerateSampleProject(), o => o.ImmediateConsistency());

        var stack = _stackData.GenerateSampleStack();
        stack.IsDeleted = true;
        await _stackRepository.AddAsync(stack, o => o.ImmediateConsistency());

        var persistentEvent = await _eventRepository.AddAsync(_eventData.GenerateEvent(organization.Id, project.Id, stack.Id), o => o.ImmediateConsistency());

        await _job.RunAsync(TestCancellationToken);

        Assert.NotNull(await _organizationRepository.GetByIdAsync(organization.Id));
        Assert.NotNull(await _projectRepository.GetByIdAsync(project.Id));
        Assert.Null(await _stackRepository.GetByIdAsync(stack.Id, o => o.IncludeSoftDeletes()));
        Assert.Null(await _eventRepository.GetByIdAsync(persistentEvent.Id, o => o.IncludeSoftDeletes()));
    }

    [Fact]
    public async Task CanCleanupEventsOutsideOfRetentionPeriod()
    {
        var organization = _organizationData.GenerateSampleOrganization(_billingManager, _plans);
        _billingManager.ApplyBillingPlan(organization, _plans.FreePlan);
        await _organizationRepository.AddAsync(organization, o => o.ImmediateConsistency());

        var project = await _projectRepository.AddAsync(_projectData.GenerateSampleProject(), o => o.ImmediateConsistency());
        var stack = await _stackRepository.AddAsync(_stackData.GenerateSampleStack(), o => o.ImmediateConsistency());

        var options = GetService<AppOptions>();
        var date = DateTimeOffset.UtcNow.SubtractDays(options.MaximumRetentionDays);
        var persistentEvent = await _eventRepository.AddAsync(_eventData.GenerateEvent(organization.Id, project.Id, stack.Id, date, date, date), o => o.ImmediateConsistency());

        await _job.RunAsync(TestCancellationToken);

        Assert.NotNull(await _organizationRepository.GetByIdAsync(organization.Id));
        Assert.NotNull(await _projectRepository.GetByIdAsync(project.Id));
        Assert.NotNull(await _stackRepository.GetByIdAsync(stack.Id));
        Assert.Null(await _eventRepository.GetByIdAsync(persistentEvent.Id, o => o.IncludeSoftDeletes()));
    }

    [Fact]
    public async Task CanDeleteOrphanedEventsByStack()
    {
        var organization = _organizationData.GenerateSampleOrganization(_billingManager, _plans);
        await _organizationRepository.AddAsync(organization, o => o.ImmediateConsistency());
        var project = await _projectRepository.AddAsync(_projectData.GenerateSampleProject(), o => o.ImmediateConsistency());

        var stack = await _stackRepository.AddAsync(_stackData.GenerateSampleStack(), o => o.ImmediateConsistency());
        await _eventRepository.AddAsync(_eventData.GenerateEvents(5000, organization.Id, project.Id, stack.Id), o => o.ImmediateConsistency());

        var orphanedEvents = _eventData.GenerateEvents(10000, organization.Id, project.Id).ToList();
        orphanedEvents.ForEach(e => e.StackId = ObjectId.GenerateNewId().ToString());

        await _eventRepository.AddAsync(orphanedEvents, o => o.ImmediateConsistency());

        var eventCount = await _eventRepository.CountAsync(o => o.IncludeSoftDeletes().ImmediateConsistency());
        Assert.Equal(15000, eventCount);

        await GetService<CleanupOrphanedDataJob>().RunAsync(TestCancellationToken);

        eventCount = await _eventRepository.CountAsync(o => o.IncludeSoftDeletes().ImmediateConsistency());
        Assert.Equal(5000, eventCount);
    }

    [Fact]
    public async Task CanCleanupSoftDeletedProject_TracksDeletedUsage()
    {
        var organization = await _organizationRepository.AddAsync(_organizationData.GenerateSampleOrganization(_billingManager, _plans), o => o.ImmediateConsistency());

        var project = _projectData.GenerateSampleProject();
        project.IsDeleted = true;
        await _projectRepository.AddAsync(project, o => o.ImmediateConsistency());

        var stack = await _stackRepository.AddAsync(_stackData.GenerateSampleStack(), o => o.ImmediateConsistency());
        var events = _eventData.GenerateEvents(5, organization.Id, project.Id, stack.Id).ToList();
        await _eventRepository.AddAsync(events, o => o.ImmediateConsistency());

        await _job.RunAsync(TestCancellationToken);

        // Project is now hard-deleted; check org-level cache (includes deleted project's contribution)
        var orgUsage = await _usageService.GetUsageAsync(organization.Id, null);
        Assert.Equal(5, orgUsage.CurrentUsage.Deleted);
        Assert.Equal(5, orgUsage.CurrentHourUsage.Deleted);

        // Flush to persistent storage
        TimeProvider.Advance(TimeSpan.FromMinutes(10));
        await _usageService.SavePendingUsageAsync();

        var savedOrg = await _organizationRepository.GetByIdAsync(organization.Id);
        Assert.NotNull(savedOrg);
        Assert.Equal(5, savedOrg.Usage.Sum(u => u.Deleted));

        // Events and project are gone
        Assert.Null(await _projectRepository.GetByIdAsync(project.Id, o => o.IncludeSoftDeletes()));
        var allEvents = await _eventRepository.GetAllAsync();
        Assert.DoesNotContain(allEvents.Documents, e => e.ProjectId == project.Id);
    }

    [Fact]
    public async Task CanCleanupSoftDeletedProject_EmptyProject_NoDeletedUsageTracked()
    {
        var organization = await _organizationRepository.AddAsync(_organizationData.GenerateSampleOrganization(_billingManager, _plans), o => o.ImmediateConsistency());

        var project = _projectData.GenerateSampleProject();
        project.IsDeleted = true;
        await _projectRepository.AddAsync(project, o => o.ImmediateConsistency());

        await _job.RunAsync(TestCancellationToken);

        // Project is now hard-deleted; check org-level cache to confirm no events were deleted
        var orgUsage = await _usageService.GetUsageAsync(organization.Id, null);
        Assert.Equal(0, orgUsage.CurrentUsage.Deleted);
        Assert.Equal(0, orgUsage.CurrentHourUsage.Deleted);

        Assert.Null(await _projectRepository.GetByIdAsync(project.Id, o => o.IncludeSoftDeletes()));
    }

    [Fact]
    public async Task CanCleanupSoftDeletedStack_TracksDeletedUsage()
    {
        var organization = await _organizationRepository.AddAsync(_organizationData.GenerateSampleOrganization(_billingManager, _plans), o => o.ImmediateConsistency());
        var project = await _projectRepository.AddAsync(_projectData.GenerateSampleProject(), o => o.ImmediateConsistency());

        var stack = _stackData.GenerateSampleStack();
        stack.IsDeleted = true;
        await _stackRepository.AddAsync(stack, o => o.ImmediateConsistency());

        var events = _eventData.GenerateEvents(3, organization.Id, project.Id, stack.Id).ToList();
        await _eventRepository.AddAsync(events, o => o.ImmediateConsistency());

        await _job.RunAsync(TestCancellationToken);

        var usageResponse = await _usageService.GetUsageAsync(organization.Id, project.Id);
        Assert.Equal(3, usageResponse.CurrentUsage.Deleted);
        Assert.Equal(3, usageResponse.CurrentHourUsage.Deleted);

        // Flush and verify org-level usage persisted
        TimeProvider.Advance(TimeSpan.FromMinutes(10));
        await _usageService.SavePendingUsageAsync();

        var savedOrg = await _organizationRepository.GetByIdAsync(organization.Id);
        Assert.NotNull(savedOrg);
        Assert.Equal(3, savedOrg.Usage.Sum(u => u.Deleted));

        Assert.Null(await _stackRepository.GetByIdAsync(stack.Id, o => o.IncludeSoftDeletes()));
    }

    [Fact]
    public async Task CanCleanupSoftDeletedStack_MultiProject_TracksExactDeletedUsagePerProject()
    {
        var organization = await _organizationRepository.AddAsync(_organizationData.GenerateSampleOrganization(_billingManager, _plans), o => o.ImmediateConsistency());
        var project1 = await _projectRepository.AddAsync(_projectData.GenerateSampleProject(), o => o.ImmediateConsistency());
        var project2 = await _projectRepository.AddAsync(_projectData.GenerateProject(generateId: true, organizationId: organization.Id), o => o.ImmediateConsistency());

        // 2 soft-deleted stacks in project1 (4+2=6 events), 1 in project2 (3 events)
        var stack1a = _stackData.GenerateStack(generateId: true, organizationId: organization.Id, projectId: project1.Id);
        stack1a.IsDeleted = true;
        var stack1b = _stackData.GenerateStack(generateId: true, organizationId: organization.Id, projectId: project1.Id);
        stack1b.IsDeleted = true;
        var stack2 = _stackData.GenerateStack(generateId: true, organizationId: organization.Id, projectId: project2.Id);
        stack2.IsDeleted = true;
        await _stackRepository.AddAsync([stack1a, stack1b, stack2], o => o.ImmediateConsistency());

        await _eventRepository.AddAsync(_eventData.GenerateEvents(4, organization.Id, project1.Id, stack1a.Id), o => o.ImmediateConsistency());
        await _eventRepository.AddAsync(_eventData.GenerateEvents(2, organization.Id, project1.Id, stack1b.Id), o => o.ImmediateConsistency());
        await _eventRepository.AddAsync(_eventData.GenerateEvents(3, organization.Id, project2.Id, stack2.Id), o => o.ImmediateConsistency());

        await _job.RunAsync(TestCancellationToken);

        // Exact per-project counts (no proportional distribution)
        var usageProject1 = await _usageService.GetUsageAsync(organization.Id, project1.Id);
        var usageProject2 = await _usageService.GetUsageAsync(organization.Id, project2.Id);
        Assert.Equal(6, usageProject1.CurrentUsage.Deleted);
        Assert.Equal(3, usageProject2.CurrentUsage.Deleted);

        // Flush and verify org-level totals are consistent
        TimeProvider.Advance(TimeSpan.FromMinutes(10));
        await _usageService.SavePendingUsageAsync();

        var savedOrg = await _organizationRepository.GetByIdAsync(organization.Id);
        Assert.NotNull(savedOrg);
        Assert.Equal(9, savedOrg.Usage.Sum(u => u.Deleted));
    }

    [Fact]
    public async Task CanCleanupSoftDeletedStack_DoesNotTrackRetentionEnforcementAsDeleted()
    {
        // Retention enforcement calls RemoveStacksAsync with trackDeletedUsage=false
        // so those event removals must NOT show up in Deleted usage
        var organization = _organizationData.GenerateSampleOrganization(_billingManager, _plans);
        _billingManager.ApplyBillingPlan(organization, _plans.FreePlan);
        await _organizationRepository.AddAsync(organization, o => o.ImmediateConsistency());
        var project = await _projectRepository.AddAsync(_projectData.GenerateSampleProject(), o => o.ImmediateConsistency());

        var options = GetService<AppOptions>();
        var expiredDate = DateTimeOffset.UtcNow.SubtractDays(options.MaximumRetentionDays);

        // Stack at retention boundary — not soft-deleted, will be removed by retention enforcement
        var stack = await _stackRepository.AddAsync(_stackData.GenerateSampleStack(), o => o.ImmediateConsistency());
        await _eventRepository.AddAsync(_eventData.GenerateEvent(organization.Id, project.Id, stack.Id, expiredDate, expiredDate, expiredDate), o => o.ImmediateConsistency());

        await _job.RunAsync(TestCancellationToken);

        // Event removed by retention — but Deleted usage must remain zero
        var usageResponse = await _usageService.GetUsageAsync(organization.Id, project.Id);
        Assert.Equal(0, usageResponse.CurrentUsage.Deleted);
        Assert.Equal(0, usageResponse.CurrentHourUsage.Deleted);
    }
}
