using Exceptionless.Core.Models;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Tests.Utility;
using Foundatio.Repositories;
using Foundatio.Repositories.Utility;
using Xunit;

namespace Exceptionless.Tests.Jobs;

public partial class CleanupDataJobTests
{
    [Fact]
    public async Task RunAsync_WithMultiplePagesOfSoftDeletedOrganizations_RemovesAllData()
    {
        // Arrange
        var organizations = new List<Organization>();
        for (int i = 0; i < 12; i++)
        {
            var organization = _organizationData.GenerateOrganization(_billingManager, _plans, generateId: true);
            organization.IsDeleted = true;
            organizations.Add(organization);
        }

        await _organizationRepository.AddAsync(organizations, o => o.ImmediateConsistency());

        var project = await _projectRepository.AddAsync(
            _projectData.GenerateProject(generateId: true, organizationId: organizations[0].Id),
            o => o.ImmediateConsistency());
        var stack = await _stackRepository.AddAsync(
            _stackData.GenerateStack(generateId: true, organizationId: organizations[0].Id, projectId: project.Id),
            o => o.ImmediateConsistency());
        await _eventRepository.AddAsync(
            _eventData.GenerateEvents(10, organizations[0].Id, project.Id, stack.Id),
            o => o.ImmediateConsistency());

        // Act
        await _job.RunAsync(TestCancellationToken);

        // Assert
        foreach (var organization in organizations)
            Assert.Null(await _organizationRepository.GetByIdAsync(organization.Id, o => o.IncludeSoftDeletes()));

        Assert.Null(await _projectRepository.GetByIdAsync(project.Id, o => o.IncludeSoftDeletes()));
        Assert.Null(await _stackRepository.GetByIdAsync(stack.Id, o => o.IncludeSoftDeletes()));
        Assert.Equal(0, await _eventRepository.CountAsync(o => o.IncludeSoftDeletes()));
    }

    [Fact]
    public async Task RunAsync_WithMultipleOrganizationRetentionPeriods_RespectsPerOrganizationRetention()
    {
        // Arrange
        // Retention enforcement uses the next plan above the organization's retention:
        // FreePlan (3d) becomes 30d and SmallPlan (30d) becomes 90d.
        var organization1 = _organizationData.GenerateOrganization(_billingManager, _plans, generateId: true);
        _billingManager.ApplyBillingPlan(organization1, _plans.SmallPlan);
        organization1.StripeCustomerId = "cust_test1";
        organization1.CardLast4 = "4242";
        organization1.SubscribeDate = DateTime.UtcNow;
        organization1.BillingChangedByUserId = TestConstants.UserId;

        var organization2 = _organizationData.GenerateOrganization(_billingManager, _plans, generateId: true);
        _billingManager.ApplyBillingPlan(organization2, _plans.FreePlan);
        await _organizationRepository.AddAsync([organization1, organization2], o => o.ImmediateConsistency());

        var project1 = _projectData.GenerateProject(generateId: true, organizationId: organization1.Id);
        var project2 = _projectData.GenerateProject(generateId: true, organizationId: organization2.Id);
        await _projectRepository.AddAsync([project1, project2], o => o.ImmediateConsistency());

        var stack1 = _stackData.GenerateStack(generateId: true, organizationId: organization1.Id, projectId: project1.Id);
        var stack2 = _stackData.GenerateStack(generateId: true, organizationId: organization2.Id, projectId: project2.Id);
        await _stackRepository.AddAsync([stack1, stack2], o => o.ImmediateConsistency());

        var recentStart = DateTimeOffset.UtcNow.AddDays(-2);
        var recentEnd = DateTimeOffset.UtcNow.AddDays(-1);
        await _eventRepository.AddAsync(_eventData.GenerateEvents(10, organization1.Id, project1.Id, stack1.Id, startDate: recentStart, endDate: recentEnd), o => o.ImmediateConsistency());
        await _eventRepository.AddAsync(_eventData.GenerateEvents(10, organization2.Id, project2.Id, stack2.Id, startDate: recentStart, endDate: recentEnd), o => o.ImmediateConsistency());

        var olderStart = DateTimeOffset.UtcNow.AddDays(-37);
        var olderEnd = DateTimeOffset.UtcNow.AddDays(-33);
        await _eventRepository.AddAsync(_eventData.GenerateEvents(10, organization1.Id, project1.Id, stack1.Id, startDate: olderStart, endDate: olderEnd), o => o.ImmediateConsistency());
        await _eventRepository.AddAsync(_eventData.GenerateEvents(10, organization2.Id, project2.Id, stack2.Id, startDate: olderStart, endDate: olderEnd), o => o.ImmediateConsistency());

        // Act
        await _job.RunAsync(TestCancellationToken);
        await RefreshDataAsync();

        // Assert
        Assert.Equal(20, await _eventRepository.CountAsync(q => q.FilterExpression($"organization:{organization1.Id}")));
        Assert.Equal(10, await _eventRepository.CountAsync(q => q.FilterExpression($"organization:{organization2.Id}")));
    }

    [Fact]
    public async Task RunAsync_WithMultiplePagesOfSoftDeletedStacks_RemovesAllStacks()
    {
        // Arrange
        var organization = await _organizationRepository.AddAsync(
            _organizationData.GenerateOrganization(_billingManager, _plans, generateId: true),
            o => o.ImmediateConsistency());
        var project = await _projectRepository.AddAsync(
            _projectData.GenerateProject(generateId: true, organizationId: organization.Id),
            o => o.ImmediateConsistency());

        var stacks = new List<Stack>();
        for (int i = 0; i < 600; i++)
        {
            var stack = _stackData.GenerateStack(generateId: true, organizationId: organization.Id, projectId: project.Id);
            stack.IsDeleted = true;
            stacks.Add(stack);
        }

        await _stackRepository.AddAsync(stacks, o => o.ImmediateConsistency());

        var validStack = await _stackRepository.AddAsync(
            _stackData.GenerateStack(generateId: true, organizationId: organization.Id, projectId: project.Id),
            o => o.ImmediateConsistency());
        await _eventRepository.AddAsync(
            _eventData.GenerateEvents(5, organization.Id, project.Id, validStack.Id),
            o => o.ImmediateConsistency());

        // Act
        await _job.RunAsync(TestCancellationToken);
        await RefreshDataAsync();

        // Assert
        Assert.Equal(1, await _stackRepository.CountAsync(o => o.IncludeSoftDeletes()));
        Assert.Equal(5, await _eventRepository.CountAsync(o => o.IncludeSoftDeletes()));
    }

    [Fact]
    public async Task RunAsync_WithEventsOutsideRetention_DeletesOnlyExpiredEvents()
    {
        // Arrange
        // FreePlan's 3-day retention is enforced at the next plan threshold (30 days).
        var organization = _organizationData.GenerateOrganization(_billingManager, _plans, generateId: true);
        _billingManager.ApplyBillingPlan(organization, _plans.FreePlan);
        await _organizationRepository.AddAsync(organization, o => o.ImmediateConsistency());

        var project = await _projectRepository.AddAsync(
            _projectData.GenerateProject(generateId: true, organizationId: organization.Id),
            o => o.ImmediateConsistency());
        var stack = await _stackRepository.AddAsync(
            _stackData.GenerateStack(generateId: true, organizationId: organization.Id, projectId: project.Id),
            o => o.ImmediateConsistency());

        var outsideRetentionStart = DateTimeOffset.UtcNow.SubtractDays(37);
        var outsideRetentionEnd = DateTimeOffset.UtcNow.SubtractDays(33);
        await _eventRepository.AddAsync(_eventData.GenerateEvents(10, organization.Id, project.Id, stack.Id, startDate: outsideRetentionStart, endDate: outsideRetentionEnd), o => o.ImmediateConsistency());

        var insideRetentionStart = DateTimeOffset.UtcNow.SubtractDays(2);
        var insideRetentionEnd = DateTimeOffset.UtcNow.SubtractDays(1);
        await _eventRepository.AddAsync(_eventData.GenerateEvents(10, organization.Id, project.Id, stack.Id, startDate: insideRetentionStart, endDate: insideRetentionEnd), o => o.ImmediateConsistency());

        // Act
        await _job.RunAsync(TestCancellationToken);
        await RefreshDataAsync();

        // Assert
        Assert.Equal(10, await _eventRepository.CountAsync(o => o.IncludeSoftDeletes()));
    }
}
