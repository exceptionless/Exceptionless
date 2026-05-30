using Exceptionless.Core;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Models;
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
    public async Task RunAsync_SuspendedOrganization_SuspendsRelatedTokens()
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
    public async Task RunAsync_SoftDeletedOrganization_RemovesAllRelatedData()
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
    public async Task RunAsync_SoftDeletedProject_RemovesProjectAndEvents()
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
    public async Task RunAsync_SoftDeletedStack_RemovesStackAndEvents()
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
    public async Task RunAsync_EventsOutsideRetentionPeriod_RemovesExpiredEvents()
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
    public async Task DeleteOrphanedEventsByStack_WithLargeDataset_DeletesAllOrphanedEvents()
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
    public async Task RemoveProjectsAsync_SoftDeletedProjectWithEvents_IncrementsDeletedUsage()
    {
        // Arrange
        var organization = await _organizationRepository.AddAsync(_organizationData.GenerateSampleOrganization(_billingManager, _plans), o => o.ImmediateConsistency());

        var project = _projectData.GenerateSampleProject();
        project.IsDeleted = true;
        await _projectRepository.AddAsync(project, o => o.ImmediateConsistency());

        var stack = await _stackRepository.AddAsync(_stackData.GenerateSampleStack(), o => o.ImmediateConsistency());
        var events = _eventData.GenerateEvents(5, organization.Id, project.Id, stack.Id).ToList();
        await _eventRepository.AddAsync(events, o => o.ImmediateConsistency());

        // Act
        await _job.RunAsync(TestCancellationToken);

        // Assert
        var orgUsage = await _usageService.GetUsageAsync(organization.Id, null);
        Assert.Equal(5, orgUsage.CurrentUsage.Deleted);
        Assert.Equal(5, orgUsage.CurrentHourUsage.Deleted);

        TimeProvider.Advance(TimeSpan.FromMinutes(10));
        await _usageService.SavePendingUsageAsync();

        var savedOrg = await _organizationRepository.GetByIdAsync(organization.Id);
        Assert.NotNull(savedOrg);
        Assert.Equal(5, savedOrg.Usage.Sum(u => u.Deleted));

        Assert.Null(await _projectRepository.GetByIdAsync(project.Id, o => o.IncludeSoftDeletes()));
        var allEvents = await _eventRepository.GetAllAsync();
        Assert.DoesNotContain(allEvents.Documents, e => String.Equals(e.ProjectId, project.Id, StringComparison.Ordinal));
    }

    [Fact]
    public async Task RemoveProjectsAsync_SoftDeletedEmptyProject_DoesNotIncrementDeletedUsage()
    {
        // Arrange
        var organization = await _organizationRepository.AddAsync(_organizationData.GenerateSampleOrganization(_billingManager, _plans), o => o.ImmediateConsistency());

        var project = _projectData.GenerateSampleProject();
        project.IsDeleted = true;
        await _projectRepository.AddAsync(project, o => o.ImmediateConsistency());

        // Act
        await _job.RunAsync(TestCancellationToken);

        // Assert
        var orgUsage = await _usageService.GetUsageAsync(organization.Id, null);
        Assert.Equal(0, orgUsage.CurrentUsage.Deleted);
        Assert.Equal(0, orgUsage.CurrentHourUsage.Deleted);

        Assert.Null(await _projectRepository.GetByIdAsync(project.Id, o => o.IncludeSoftDeletes()));
    }

    [Fact]
    public async Task RemoveStacksAsync_SoftDeletedStack_IncrementsDeletedUsage()
    {
        // Arrange
        var organization = await _organizationRepository.AddAsync(_organizationData.GenerateSampleOrganization(_billingManager, _plans), o => o.ImmediateConsistency());
        var project = await _projectRepository.AddAsync(_projectData.GenerateSampleProject(), o => o.ImmediateConsistency());

        var stack = _stackData.GenerateSampleStack();
        stack.IsDeleted = true;
        await _stackRepository.AddAsync(stack, o => o.ImmediateConsistency());

        var events = _eventData.GenerateEvents(3, organization.Id, project.Id, stack.Id).ToList();
        await _eventRepository.AddAsync(events, o => o.ImmediateConsistency());

        // Act
        await _job.RunAsync(TestCancellationToken);

        // Assert
        var usageResponse = await _usageService.GetUsageAsync(organization.Id, project.Id);
        Assert.Equal(3, usageResponse.CurrentUsage.Deleted);
        Assert.Equal(3, usageResponse.CurrentHourUsage.Deleted);

        TimeProvider.Advance(TimeSpan.FromMinutes(10));
        await _usageService.SavePendingUsageAsync();

        var savedOrg = await _organizationRepository.GetByIdAsync(organization.Id);
        Assert.NotNull(savedOrg);
        Assert.Equal(3, savedOrg.Usage.Sum(u => u.Deleted));

        Assert.Null(await _stackRepository.GetByIdAsync(stack.Id, o => o.IncludeSoftDeletes()));
    }

    [Fact]
    public async Task RemoveStacksAsync_MultipleProjectsSoftDeleted_TracksExactDeletedUsagePerProject()
    {
        // Arrange
        var organization = await _organizationRepository.AddAsync(_organizationData.GenerateSampleOrganization(_billingManager, _plans), o => o.ImmediateConsistency());
        var project1 = await _projectRepository.AddAsync(_projectData.GenerateSampleProject(), o => o.ImmediateConsistency());
        var project2 = await _projectRepository.AddAsync(_projectData.GenerateProject(generateId: true, organizationId: organization.Id), o => o.ImmediateConsistency());

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

        // Act
        await _job.RunAsync(TestCancellationToken);

        // Assert
        var usageProject1 = await _usageService.GetUsageAsync(organization.Id, project1.Id);
        var usageProject2 = await _usageService.GetUsageAsync(organization.Id, project2.Id);
        Assert.Equal(6, usageProject1.CurrentUsage.Deleted);
        Assert.Equal(3, usageProject2.CurrentUsage.Deleted);

        TimeProvider.Advance(TimeSpan.FromMinutes(10));
        await _usageService.SavePendingUsageAsync();

        var savedOrg = await _organizationRepository.GetByIdAsync(organization.Id);
        Assert.NotNull(savedOrg);
        Assert.Equal(9, savedOrg.Usage.Sum(u => u.Deleted));
    }

    [Fact]
    public async Task EnforceRetentionAsync_ExpiredEvents_DoesNotIncrementDeletedUsage()
    {
        // Arrange
        var organization = _organizationData.GenerateSampleOrganization(_billingManager, _plans);
        _billingManager.ApplyBillingPlan(organization, _plans.FreePlan);
        await _organizationRepository.AddAsync(organization, o => o.ImmediateConsistency());
        var project = await _projectRepository.AddAsync(_projectData.GenerateSampleProject(), o => o.ImmediateConsistency());

        var options = GetService<AppOptions>();
        var expiredDate = DateTimeOffset.UtcNow.SubtractDays(options.MaximumRetentionDays);

        var stack = await _stackRepository.AddAsync(_stackData.GenerateSampleStack(), o => o.ImmediateConsistency());
        await _eventRepository.AddAsync(_eventData.GenerateEvent(organization.Id, project.Id, stack.Id, expiredDate, expiredDate, expiredDate), o => o.ImmediateConsistency());

        // Act
        await _job.RunAsync(TestCancellationToken);

        // Assert
        var usageResponse = await _usageService.GetUsageAsync(organization.Id, project.Id);
        Assert.Equal(0, usageResponse.CurrentUsage.Deleted);
        Assert.Equal(0, usageResponse.CurrentHourUsage.Deleted);
    }

    [Fact]
    public async Task CleanupSoftDeletedOrganizations_WithMultiplePages_RemovesAllData()
    {
        // Create more than the page size (5) of soft-deleted organizations to test pagination
        var orgs = new List<Organization>();
        for (int i = 0; i < 12; i++)
        {
            var org = _organizationData.GenerateSampleOrganization(_billingManager, _plans);
            org.Id = ObjectId.GenerateNewId().ToString();
            org.IsDeleted = true;
            orgs.Add(org);
        }
        await _organizationRepository.AddAsync(orgs, o => o.ImmediateConsistency());

        // Create associated projects and events for a subset
        var project = _projectData.GenerateSampleProject();
        project.OrganizationId = orgs[0].Id;
        await _projectRepository.AddAsync(project, o => o.ImmediateConsistency());

        var stack = _stackData.GenerateStack(generateId: true, organizationId: orgs[0].Id, projectId: project.Id);
        await _stackRepository.AddAsync(stack, o => o.ImmediateConsistency());

        await _eventRepository.AddAsync(_eventData.GenerateEvents(10, orgs[0].Id, project.Id, stack.Id), o => o.ImmediateConsistency());

        await _job.RunAsync(TestCancellationToken);

        // All soft-deleted orgs should be removed
        foreach (var org in orgs)
            Assert.Null(await _organizationRepository.GetByIdAsync(org.Id, o => o.IncludeSoftDeletes()));

        // Associated data should be gone too
        Assert.Null(await _projectRepository.GetByIdAsync(project.Id, o => o.IncludeSoftDeletes()));
        Assert.Null(await _stackRepository.GetByIdAsync(stack.Id, o => o.IncludeSoftDeletes()));
        var eventCount = await _eventRepository.CountAsync(o => o.IncludeSoftDeletes());
        Assert.Equal(0, eventCount);
    }

    [Fact]
    public async Task EnforceRetention_WithMultipleOrganizations_RespectsPerOrgRetention()
    {
        // Retention enforcement uses GetBillingPlanByUpsellingRetentionPeriod which returns the next
        // plan with retention > org's retention. FreePlan (3d) → effective 30d, SmallPlan (30d) → effective 90d.
        var org1 = _organizationData.GenerateSampleOrganization(_billingManager, _plans);
        org1.Id = ObjectId.GenerateNewId().ToString();
        _billingManager.ApplyBillingPlan(org1, _plans.SmallPlan); // effective retention: 90 days (next plan up is Large)
        org1.StripeCustomerId = "cust_test1";
        org1.CardLast4 = "4242";
        org1.SubscribeDate = DateTime.UtcNow;
        org1.BillingChangedByUserId = TestConstants.UserId;

        var org2 = _organizationData.GenerateSampleOrganization(_billingManager, _plans);
        org2.Id = ObjectId.GenerateNewId().ToString();
        _billingManager.ApplyBillingPlan(org2, _plans.FreePlan); // effective retention: 30 days (next plan up is Small)
        await _organizationRepository.AddAsync(new[] { org1, org2 }, o => o.ImmediateConsistency());

        var project1 = _projectData.GenerateProject(generateId: true, organizationId: org1.Id);
        var project2 = _projectData.GenerateProject(generateId: true, organizationId: org2.Id);
        await _projectRepository.AddAsync(new[] { project1, project2 }, o => o.ImmediateConsistency());

        var stack1 = _stackData.GenerateStack(generateId: true, organizationId: org1.Id, projectId: project1.Id);
        var stack2 = _stackData.GenerateStack(generateId: true, organizationId: org2.Id, projectId: project2.Id);
        await _stackRepository.AddAsync(new[] { stack1, stack2 }, o => o.ImmediateConsistency());

        // Events inside retention for both (2 days old)
        var recentStart = DateTimeOffset.UtcNow.AddDays(-2);
        var recentEnd = DateTimeOffset.UtcNow.AddDays(-1);
        await _eventRepository.AddAsync(_eventData.GenerateEvents(10, org1.Id, project1.Id, stack1.Id, startDate: recentStart, endDate: recentEnd), o => o.ImmediateConsistency());
        await _eventRepository.AddAsync(_eventData.GenerateEvents(10, org2.Id, project2.Id, stack2.Id, startDate: recentStart, endDate: recentEnd), o => o.ImmediateConsistency());

        // Events at 35 days old: outside org2's effective retention (30d) but inside org1's (90d)
        var olderStart = DateTimeOffset.UtcNow.AddDays(-37);
        var olderEnd = DateTimeOffset.UtcNow.AddDays(-33);
        await _eventRepository.AddAsync(_eventData.GenerateEvents(10, org1.Id, project1.Id, stack1.Id, startDate: olderStart, endDate: olderEnd), o => o.ImmediateConsistency());
        await _eventRepository.AddAsync(_eventData.GenerateEvents(10, org2.Id, project2.Id, stack2.Id, startDate: olderStart, endDate: olderEnd), o => o.ImmediateConsistency());

        await _job.RunAsync(TestCancellationToken);
        await RefreshDataAsync();

        // org1 should keep all events (within 90 day effective retention)
        var org1Events = await _eventRepository.CountAsync(q => q.FilterExpression($"organization:{org1.Id}"));
        Assert.Equal(20, org1Events);

        // org2 should only keep recent events (older ones outside 30 day effective retention)
        var org2Events = await _eventRepository.CountAsync(q => q.FilterExpression($"organization:{org2.Id}"));
        Assert.Equal(10, org2Events);
    }

    [Fact]
    public async Task CleanupSoftDeletedStacks_WithMultiplePages_RemovesAllStacks()
    {
        var organization = await _organizationRepository.AddAsync(_organizationData.GenerateSampleOrganization(_billingManager, _plans), o => o.ImmediateConsistency());
        var project = await _projectRepository.AddAsync(_projectData.GenerateSampleProject(), o => o.ImmediateConsistency());

        // Create more stacks than the page size (500) to test pagination
        var stacks = new List<Stack>();
        for (int i = 0; i < 600; i++)
        {
            var stack = _stackData.GenerateStack(generateId: true, organizationId: organization.Id, projectId: project.Id);
            stack.IsDeleted = true;
            stacks.Add(stack);
        }
        await _stackRepository.AddAsync(stacks, o => o.ImmediateConsistency());

        // Create a valid non-deleted stack with events
        var validStack = await _stackRepository.AddAsync(_stackData.GenerateStack(generateId: true, organizationId: organization.Id, projectId: project.Id), o => o.ImmediateConsistency());
        await _eventRepository.AddAsync(_eventData.GenerateEvents(5, organization.Id, project.Id, validStack.Id), o => o.ImmediateConsistency());

        await _job.RunAsync(TestCancellationToken);
        await RefreshDataAsync();

        // All soft-deleted stacks should be removed
        var remainingStacks = await _stackRepository.CountAsync(o => o.IncludeSoftDeletes());
        Assert.Equal(1, remainingStacks);

        // Valid events should remain
        var eventCount = await _eventRepository.CountAsync(o => o.IncludeSoftDeletes());
        Assert.Equal(5, eventCount);
    }

    [Fact]
    public async Task EnforceRetention_WithEventsOutsideRetention_DeletesOnlyExpiredEvents()
    {
        // FreePlan has 3 day retention, but enforcement uses GetBillingPlanByUpsellingRetentionPeriod
        // which returns the next plan up (SmallPlan, 30 days). So effective retention = 30 days.
        var organization = _organizationData.GenerateSampleOrganization(_billingManager, _plans);
        _billingManager.ApplyBillingPlan(organization, _plans.FreePlan);
        await _organizationRepository.AddAsync(organization, o => o.ImmediateConsistency());

        var project = await _projectRepository.AddAsync(_projectData.GenerateSampleProject(), o => o.ImmediateConsistency());
        var stack = await _stackRepository.AddAsync(_stackData.GenerateSampleStack(), o => o.ImmediateConsistency());

        // Events outside effective retention (30 days) should be deleted
        var outsideRetentionStart = DateTimeOffset.UtcNow.SubtractDays(37);
        var outsideRetentionEnd = DateTimeOffset.UtcNow.SubtractDays(33);
        await _eventRepository.AddAsync(_eventData.GenerateEvents(10, organization.Id, project.Id, stack.Id, startDate: outsideRetentionStart, endDate: outsideRetentionEnd), o => o.ImmediateConsistency());

        // Events inside effective retention should be kept
        var insideRetentionStart = DateTimeOffset.UtcNow.SubtractDays(2);
        var insideRetentionEnd = DateTimeOffset.UtcNow.SubtractDays(1);
        await _eventRepository.AddAsync(_eventData.GenerateEvents(10, organization.Id, project.Id, stack.Id, startDate: insideRetentionStart, endDate: insideRetentionEnd), o => o.ImmediateConsistency());

        await _job.RunAsync(TestCancellationToken);
        await RefreshDataAsync();

        var eventCount = await _eventRepository.CountAsync(o => o.IncludeSoftDeletes());
        Assert.Equal(10, eventCount);
    }
}
