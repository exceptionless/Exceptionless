using System.Collections.Concurrent;
using System.Diagnostics;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services;
using Exceptionless.Tests.Extensions;
using Foundatio.AsyncEx;
using Foundatio.Caching;
using Foundatio.Messaging;
using Foundatio.Repositories;
using Xunit;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Exceptionless.Tests.Services;

public sealed partial class UsageServiceTests
{
    [Fact]
    public async Task GetSmartThrottleRateAsync_OrganizationNotFound_ReturnsNoThrottle()
    {
        // Regression test: When the organization does not exist, GetMaxEventsPerMonthAsync
        // returns 0 (default). GetSmartThrottleRateAsync must not divide by zero.
        string nonExistentOrgId = "000000000000000000000099";
        string nonExistentProjectId = "000000000000000000000098";

        // Act - this would divide by zero before the fix (maxEventsPerMonth=0 returned for missing org)
        var result = await _usageService.GetSmartThrottleRateAsync(nonExistentOrgId, nonExistentProjectId);

        // Assert - should return NoThrottle since maxEventsPerMonth <= 0 means unlimited/invalid
        Assert.False(result.IsThrottled);
        Assert.Equal(1.0, result.SampleRate);
    }

    // ── GetEventIngestAllowanceAsync ────────────────────────────────────────

    [Fact]
    public async Task GetEventIngestAllowanceAsync_Project_WithNoIngestLimit_ReturnsOrgLimit()
    {
        var organization = await _organizationRepository.AddAsync(new Organization { Name = "Test", MaxEventsPerMonth = 750, PlanId = _plans.SmallPlan.Id }, o => o.ImmediateConsistency().Cache());
        var project = await _projectRepository.AddAsync(new Project { Name = "Test", OrganizationId = organization.Id, NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Ticks }, o => o.ImmediateConsistency().Cache());

        var result = await _usageService.GetEventIngestAllowanceAsync(organization.Id, project.Id);

        Assert.True(result.EventsLeft > 0);
        Assert.Equal(1.0, result.SampleRate);
        Assert.False(result.IsOverOrgLimit);
        Assert.False(result.IsOverProjectLimit);
    }

    [Fact]
    public async Task GetEventIngestAllowanceAsync_Project_WithFixedIngestLimit_BelowOrgLimit_ReturnsFixed()
    {
        var organization = await _organizationRepository.AddAsync(new Organization { Name = "Test", MaxEventsPerMonth = 750, PlanId = _plans.SmallPlan.Id }, o => o.ImmediateConsistency().Cache());
        var project = await _projectRepository.AddAsync(new Project
        {
            Name = "Test",
            OrganizationId = organization.Id,
            NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Ticks,
            IngestLimit = new ProjectIngestLimit { Type = ProjectIngestLimitType.Fixed, FixedLimit = 100 }
        }, o => o.ImmediateConsistency().Cache());

        var result = await _usageService.GetEventIngestAllowanceAsync(organization.Id, project.Id);

        Assert.Equal(100, result.EventsLeft);
        Assert.Equal(100, result.EffectiveProjectLimit);
        Assert.False(result.IsOverOrgLimit);
        Assert.False(result.IsOverProjectLimit);
    }

    [Fact]
    public async Task GetEventIngestAllowanceAsync_Project_WithFixedIngestLimit_AboveOrgLimit_ReturnsOrgLimit()
    {
        var organization = await _organizationRepository.AddAsync(new Organization { Name = "Test", MaxEventsPerMonth = 750, PlanId = _plans.SmallPlan.Id }, o => o.ImmediateConsistency().Cache());
        var project = await _projectRepository.AddAsync(new Project
        {
            Name = "Test",
            OrganizationId = organization.Id,
            NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Ticks,
            IngestLimit = new ProjectIngestLimit { Type = ProjectIngestLimitType.Fixed, FixedLimit = 10000 }
        }, o => o.ImmediateConsistency().Cache());

        await _usageService.IncrementTotalAsync(organization.Id, project.Id, 749);

        var result = await _usageService.GetEventIngestAllowanceAsync(organization.Id, project.Id);

        Assert.Equal(1, result.EventsLeft);
        Assert.Equal(750, result.EffectiveProjectLimit);
        Assert.False(result.IsOverOrgLimit);
        Assert.False(result.IsOverProjectLimit);
    }

    [Fact]
    public async Task GetEventIngestAllowanceAsync_Project_WithPercentageIngestLimit_50Percent_ReturnsCorrectLimit()
    {
        var organization = await _organizationRepository.AddAsync(new Organization { Name = "Test", MaxEventsPerMonth = 1000, PlanId = _plans.SmallPlan.Id }, o => o.ImmediateConsistency().Cache());
        var project = await _projectRepository.AddAsync(new Project
        {
            Name = "Test",
            OrganizationId = organization.Id,
            NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Ticks,
            IngestLimit = new ProjectIngestLimit { Type = ProjectIngestLimitType.PercentOfOrganizationLimit, PercentOfOrganizationLimit = 50 }
        }, o => o.ImmediateConsistency().Cache());

        var result = await _usageService.GetEventIngestAllowanceAsync(organization.Id, project.Id);

        Assert.Equal(500, result.EventsLeft);
        Assert.Equal(500, result.EffectiveProjectLimit);
        Assert.False(result.IsOverOrgLimit);
        Assert.False(result.IsOverProjectLimit);
    }

    [Fact]
    public async Task GetEventIngestAllowanceAsync_Project_WithInvalidPersistedPercentage_IsInactive()
    {
        var organization = await _organizationRepository.AddAsync(new Organization { Name = "Test", MaxEventsPerMonth = 1000, PlanId = _plans.SmallPlan.Id }, o => o.ImmediateConsistency().Cache());
        var project = await _projectRepository.AddAsync(new Project
        {
            Name = "Test",
            OrganizationId = organization.Id,
            NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Ticks
        }, o => o.ImmediateConsistency().Cache());
        project.IngestLimit = new ProjectIngestLimit { Type = ProjectIngestLimitType.PercentOfOrganizationLimit, PercentOfOrganizationLimit = 150 };

        var result = await _usageService.GetEventIngestAllowanceAsync(organization, project);

        Assert.Equal(-1, result.EffectiveProjectLimit);
        Assert.True(result.EventsLeft > 0);
        Assert.False(result.IsOverOrgLimit);
        Assert.False(result.IsOverProjectLimit);
    }

    [Fact]
    public async Task GetEventIngestAllowanceAsync_Project_WithFixedLimit_OverLimit_IsOverProjectLimitTrue()
    {
        var organization = await _organizationRepository.AddAsync(new Organization { Name = "Test", MaxEventsPerMonth = 750, PlanId = _plans.SmallPlan.Id }, o => o.ImmediateConsistency().Cache());
        var project = await _projectRepository.AddAsync(new Project
        {
            Name = "Test",
            OrganizationId = organization.Id,
            NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Ticks,
            IngestLimit = new ProjectIngestLimit { Type = ProjectIngestLimitType.Fixed, FixedLimit = 100 }
        }, o => o.ImmediateConsistency().Cache());
        project.GetCurrentUsage(TimeProvider).Total = 100;
        await _projectRepository.SaveAsync(project, o => o.ImmediateConsistency().Cache());

        var result = await _usageService.GetEventIngestAllowanceAsync(organization.Id, project.Id);

        Assert.Equal(0, result.EventsLeft);
        Assert.True(result.IsOverProjectLimit);
        Assert.False(result.IsOverOrgLimit);
        Assert.Equal(100, result.EffectiveProjectLimit);
    }

    [Fact]
    public async Task GetEventIngestAllowanceAsync_Organization_OverLimit_IsOverOrgLimitTrue()
    {
        var organization = await _organizationRepository.AddAsync(new Organization { Name = "Test", MaxEventsPerMonth = 750, PlanId = _plans.SmallPlan.Id }, o => o.ImmediateConsistency().Cache());
        var project = await _projectRepository.AddAsync(new Project { Name = "Test", OrganizationId = organization.Id, NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Ticks }, o => o.ImmediateConsistency().Cache());

        await _usageService.IncrementTotalAsync(organization.Id, project.Id, 750);

        var result = await _usageService.GetEventIngestAllowanceAsync(organization.Id, project.Id);

        Assert.Equal(0, result.EventsLeft);
        Assert.True(result.IsOverOrgLimit);
        Assert.False(result.IsOverProjectLimit);
    }

    [Fact]
    public async Task GetEventIngestAllowanceAsync_UnlimitedOrg_WithFixedProjectLimit_ReturnsFixed()
    {
        var organization = await _organizationRepository.AddAsync(new Organization { Name = "Test", MaxEventsPerMonth = _plans.UnlimitedPlan.MaxEventsPerMonth, PlanId = _plans.UnlimitedPlan.Id }, o => o.ImmediateConsistency().Cache());
        var project = await _projectRepository.AddAsync(new Project
        {
            Name = "Test",
            OrganizationId = organization.Id,
            NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Ticks,
            IngestLimit = new ProjectIngestLimit { Type = ProjectIngestLimitType.Fixed, FixedLimit = 500 }
        }, o => o.ImmediateConsistency().Cache());

        var result = await _usageService.GetEventIngestAllowanceAsync(organization.Id, project.Id);

        Assert.Equal(500, result.EventsLeft);
        Assert.Equal(500, result.EffectiveProjectLimit);
        Assert.False(result.IsOverOrgLimit);
        Assert.False(result.IsOverProjectLimit);
    }

    // ── GetSmartThrottleRateAsync ────────────────────────────────────────────

    [Fact]
    public async Task GetSmartThrottleRateAsync_BelowThreshold_ReturnsNoThrottle()
    {
        var organization = new Organization { Name = "Test", MaxEventsPerMonth = 1000, PlanId = _plans.SmallPlan.Id };
        organization.GetCurrentUsage(TimeProvider).Total = 600;
        organization = await _organizationRepository.AddAsync(organization, o => o.ImmediateConsistency().Cache());
        var project1 = await _projectRepository.AddAsync(new Project { Name = "P1", OrganizationId = organization.Id, NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Ticks }, o => o.ImmediateConsistency().Cache());
        var project2 = await _projectRepository.AddAsync(new Project { Name = "P2", OrganizationId = organization.Id, NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Ticks }, o => o.ImmediateConsistency().Cache());
        project1.GetCurrentUsage(TimeProvider).Total = 600;
        await _projectRepository.SaveAsync(project1, o => o.ImmediateConsistency().Cache());

        var result = await _usageService.GetSmartThrottleRateAsync(organization.Id, project1.Id);

        Assert.False(result.IsThrottled);
        Assert.Equal(1.0, result.SampleRate);
        _ = project2;
    }

    [Fact]
    public async Task GetSmartThrottleRateAsync_SingleProjectInOrg_ReturnsNoThrottle()
    {
        var organization = new Organization { Name = "Test", MaxEventsPerMonth = 1000, PlanId = _plans.SmallPlan.Id };
        organization.GetCurrentUsage(TimeProvider).Total = 900;
        organization = await _organizationRepository.AddAsync(organization, o => o.ImmediateConsistency().Cache());
        var project = await _projectRepository.AddAsync(new Project { Name = "P1", OrganizationId = organization.Id, NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Ticks }, o => o.ImmediateConsistency().Cache());
        project.GetCurrentUsage(TimeProvider).Total = 900;
        await _projectRepository.SaveAsync(project, o => o.ImmediateConsistency().Cache());

        var result = await _usageService.GetSmartThrottleRateAsync(organization.Id, project.Id);

        Assert.False(result.IsThrottled);
        Assert.Equal(1.0, result.SampleRate);
    }

    [Fact]
    public async Task GetSmartThrottleRateAsync_NoOrgUsage_ReturnsNoThrottle()
    {
        var organization = await _organizationRepository.AddAsync(new Organization { Name = "Test", MaxEventsPerMonth = 1000, PlanId = _plans.SmallPlan.Id }, o => o.ImmediateConsistency().Cache());
        var project1 = await _projectRepository.AddAsync(new Project { Name = "P1", OrganizationId = organization.Id, NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Ticks }, o => o.ImmediateConsistency().Cache());
        var project2 = await _projectRepository.AddAsync(new Project { Name = "P2", OrganizationId = organization.Id, NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Ticks }, o => o.ImmediateConsistency().Cache());

        var result = await _usageService.GetSmartThrottleRateAsync(organization.Id, project1.Id);

        Assert.False(result.IsThrottled);
        Assert.Equal(1.0, result.SampleRate);
        _ = project2;
    }

    [Fact]
    public async Task GetSmartThrottleRateAsync_NoProjectUsage_ReturnsNoThrottle()
    {
        var organization = new Organization { Name = "Test", MaxEventsPerMonth = 1000, PlanId = _plans.SmallPlan.Id };
        organization.GetCurrentUsage(TimeProvider).Total = 900;
        organization = await _organizationRepository.AddAsync(organization, o => o.ImmediateConsistency().Cache());
        var project1 = await _projectRepository.AddAsync(new Project { Name = "P1", OrganizationId = organization.Id, NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Ticks }, o => o.ImmediateConsistency().Cache());
        var project2 = await _projectRepository.AddAsync(new Project { Name = "P2", OrganizationId = organization.Id, NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Ticks }, o => o.ImmediateConsistency().Cache());

        var result = await _usageService.GetSmartThrottleRateAsync(organization.Id, project1.Id);

        Assert.False(result.IsThrottled);
        Assert.Equal(1.0, result.SampleRate);
        _ = project2;
    }

    [Fact]
    public async Task GetSmartThrottleRateAsync_AboveThreshold_FairShare_ReturnsNoThrottle()
    {
        var organization = new Organization { Name = "Test", MaxEventsPerMonth = 1000, PlanId = _plans.SmallPlan.Id };
        organization.GetCurrentUsage(TimeProvider).Total = 900;
        organization = await _organizationRepository.AddAsync(organization, o => o.ImmediateConsistency().Cache());
        var project1 = await _projectRepository.AddAsync(new Project { Name = "P1", OrganizationId = organization.Id, NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Ticks }, o => o.ImmediateConsistency().Cache());
        var project2 = await _projectRepository.AddAsync(new Project { Name = "P2", OrganizationId = organization.Id, NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Ticks }, o => o.ImmediateConsistency().Cache());
        // fairShare = 1000/2 = 500; projectTotal = 900 → fairShareRatio = 1.8 ≤ 2.0
        project1.GetCurrentUsage(TimeProvider).Total = 900;
        await _projectRepository.SaveAsync(project1, o => o.ImmediateConsistency().Cache());

        var result = await _usageService.GetSmartThrottleRateAsync(organization.Id, project1.Id);

        Assert.False(result.IsThrottled);
        Assert.Equal(1.0, result.SampleRate);
        _ = project2;
    }

    [Fact]
    public async Task GetSmartThrottleRateAsync_CurrentWindowSpike_UsesFixedFivePercentSample()
    {
        var organization = await _organizationRepository.AddAsync(new Organization
        {
            Name = "Test",
            MaxEventsPerMonth = 1_000_000,
            PlanId = _plans.ExtraLargePlan.Id
        }, o => o.ImmediateConsistency().Cache());
        var project1 = await _projectRepository.AddAsync(new Project { Name = "P1", OrganizationId = organization.Id, NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Ticks }, o => o.ImmediateConsistency().Cache());
        await _projectRepository.AddAsync(new Project { Name = "P2", OrganizationId = organization.Id, NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Ticks }, o => o.ImmediateConsistency().Cache());
        await _projectRepository.AddAsync(new Project { Name = "P3", OrganizationId = organization.Id, NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Ticks }, o => o.ImmediateConsistency().Cache());

        await _usageService.IncrementTotalAsync(organization, project1.Id, 1_900);

        var result = await _usageService.GetSmartThrottleRateAsync(organization.Id, project1.Id);

        Assert.True(result.IsThrottled);
        Assert.Equal(0.05, result.SampleRate);
        Assert.Equal(1_900, result.CurrentProjectUsage);
        Assert.Equal(333_333, result.FairShareLimit);
    }

    [Fact]
    public async Task GetSmartThrottleRateAsync_HighMonthlyUsageWithoutCurrentSpike_ReturnsNoThrottle()
    {
        var organization = new Organization { Name = "Test", MaxEventsPerMonth = 1_000_000, PlanId = _plans.ExtraLargePlan.Id };
        organization.GetCurrentUsage(TimeProvider).Total = 900_000;
        organization = await _organizationRepository.AddAsync(organization, o => o.ImmediateConsistency().Cache());
        var project1 = new Project { Name = "P1", OrganizationId = organization.Id, NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Ticks };
        project1.GetCurrentUsage(TimeProvider).Total = 900_000;
        project1 = await _projectRepository.AddAsync(project1, o => o.ImmediateConsistency().Cache());
        await _projectRepository.AddAsync(new Project { Name = "P2", OrganizationId = organization.Id, NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Ticks }, o => o.ImmediateConsistency().Cache());

        var result = await _usageService.GetSmartThrottleRateAsync(organization.Id, project1.Id);

        Assert.False(result.IsThrottled);
        Assert.Equal(1.0, result.SampleRate);
    }

    [Fact]
    public async Task GetSmartThrottleRateAsync_UnlimitedOrg_ReturnsNoThrottle()
    {
        var organization = await _organizationRepository.AddAsync(new Organization { Name = "Test", MaxEventsPerMonth = _plans.UnlimitedPlan.MaxEventsPerMonth, PlanId = _plans.UnlimitedPlan.Id }, o => o.ImmediateConsistency().Cache());
        var project = await _projectRepository.AddAsync(new Project { Name = "P1", OrganizationId = organization.Id, NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Ticks }, o => o.ImmediateConsistency().Cache());

        var result = await _usageService.GetSmartThrottleRateAsync(organization.Id, project.Id);

        Assert.False(result.IsThrottled);
        Assert.Equal(1.0, result.SampleRate);
    }

    // ── Budget Alert Threshold tests ─────────────────────────────────────────

    [Fact]
    public async Task IncrementTotalAsync_CrossingThreshold_PublishesAlertMessage()
    {
        var messageBus = GetService<IMessageBus>();
        var countdown = new AsyncCountdownEvent(1);
        OrganizationBudgetAlert? alert = null;
        await messageBus.SubscribeAsync<OrganizationBudgetAlert>(a =>
        {
            alert = a;
            countdown.Signal();
        }, TestCancellationToken);

        var organization = new Organization
        {
            Name = "Test",
            MaxEventsPerMonth = 1000,
            PlanId = _plans.SmallPlan.Id,
            BudgetAlertSettings = new OrganizationBudgetAlertSettings { Enabled = true, Thresholds = new SortedSet<int> { 50 } }
        };
        organization.GetCurrentUsage(TimeProvider).Total = 490;
        organization = await _organizationRepository.AddAsync(organization, o => o.ImmediateConsistency().Cache());
        var project = await _projectRepository.AddAsync(new Project { Name = "Test", OrganizationId = organization.Id, NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Ticks }, o => o.ImmediateConsistency().Cache());

        await _usageService.GetEventsLeftAsync(organization.Id);
        await _usageService.IncrementTotalAsync(organization.Id, project.Id, 15);
        await countdown.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.NotNull(alert);
        Assert.Equal(organization.Id, alert.OrganizationId);
        Assert.Equal(50, alert.Threshold);
        Assert.Equal(500, alert.ThresholdEventCount);
        Assert.Equal(1000, alert.EventLimit);
    }

    [Fact]
    public async Task IncrementTotalAsync_Threshold_NotDuplicated_WhenCrossedTwice()
    {
        var messageBus = GetService<IMessageBus>();
        int alertCount = 0;
        var firstAlert = new AsyncCountdownEvent(1);
        await messageBus.SubscribeAsync<OrganizationBudgetAlert>(a =>
        {
            int count = Interlocked.Increment(ref alertCount);
            if (count == 1) firstAlert.Signal();
        }, TestCancellationToken);

        var organization = new Organization
        {
            Name = "Test",
            MaxEventsPerMonth = 1000,
            PlanId = _plans.SmallPlan.Id,
            BudgetAlertSettings = new OrganizationBudgetAlertSettings { Enabled = true, Thresholds = new SortedSet<int> { 50 } }
        };
        organization.GetCurrentUsage(TimeProvider).Total = 490;
        organization = await _organizationRepository.AddAsync(organization, o => o.ImmediateConsistency().Cache());
        var project = await _projectRepository.AddAsync(new Project { Name = "Test", OrganizationId = organization.Id, NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Ticks }, o => o.ImmediateConsistency().Cache());

        await _usageService.GetEventsLeftAsync(organization.Id);
        await _usageService.IncrementTotalAsync(organization.Id, project.Id, 15);
        await firstAlert.WaitAsync(TimeSpan.FromSeconds(5));

        await _usageService.IncrementTotalAsync(organization.Id, project.Id, 20);
        await Task.Delay(200, TestCancellationToken);

        Assert.Equal(1, alertCount);
    }

    [Fact]
    public async Task CheckBudgetAlertThresholdsAsync_ConcurrentCalls_PublishesAlertExactlyOnce()
    {
        // Regression: non-atomic get-then-set allowed two concurrent workers to both see the
        // key absent and both publish. The fix uses AddAsync (atomic) so only the worker
        // whose increment returns 1 sends the alert.
        var messageBus = GetService<IMessageBus>();
        int alertCount = 0;
        var firstAlert = new AsyncCountdownEvent(1);
        await messageBus.SubscribeAsync<OrganizationBudgetAlert>(_ =>
        {
            int count = Interlocked.Increment(ref alertCount);
            if (count == 1) firstAlert.Signal();
        }, TestCancellationToken);

        var organization = new Organization
        {
            Name = "Test",
            MaxEventsPerMonth = 1000,
            PlanId = _plans.SmallPlan.Id,
            BudgetAlertSettings = new OrganizationBudgetAlertSettings { Enabled = true, Thresholds = new SortedSet<int> { 50 } }
        };
        organization.GetCurrentUsage(TimeProvider).Total = 490;
        organization = await _organizationRepository.AddAsync(organization, o => o.ImmediateConsistency().Cache());
        var project = await _projectRepository.AddAsync(new Project { Name = "Test", OrganizationId = organization.Id, NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Ticks }, o => o.ImmediateConsistency().Cache());

        // Simulate concurrent workers by calling GetEventsLeftAsync + IncrementTotalAsync
        // concurrently. Both workers see usage crossing 50%; only one should publish the alert.
        await _usageService.GetEventsLeftAsync(organization.Id);
        var tasks = Enumerable.Range(0, 5)
            .Select(_ => _usageService.IncrementTotalAsync(organization.Id, project.Id, 15))
            .ToArray();
        await Task.WhenAll(tasks);

        // Wait for the first (and only) alert, then give extra time for any duplicates.
        await firstAlert.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(300, TestCancellationToken);

        Assert.Equal(1, alertCount);
    }

    [Fact]
    public async Task IncrementTotalAsync_DisabledAlerts_DoNotPublishMessages()
    {
        var messageBus = GetService<IMessageBus>();
        int alertCount = 0;
        await messageBus.SubscribeAsync<OrganizationBudgetAlert>(_ => { Interlocked.Increment(ref alertCount); }, TestCancellationToken);

        var organization = new Organization
        {
            Name = "Test",
            MaxEventsPerMonth = 1000,
            PlanId = _plans.SmallPlan.Id,
            BudgetAlertSettings = new OrganizationBudgetAlertSettings { Enabled = false, Thresholds = new SortedSet<int> { 50 } }
        };
        organization.GetCurrentUsage(TimeProvider).Total = 490;
        organization = await _organizationRepository.AddAsync(organization, o => o.ImmediateConsistency().Cache());
        var project = await _projectRepository.AddAsync(new Project { Name = "Test", OrganizationId = organization.Id, NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Ticks }, o => o.ImmediateConsistency().Cache());

        await _usageService.GetEventsLeftAsync(organization.Id);
        await _usageService.IncrementTotalAsync(organization.Id, project.Id, 100);

        await Task.Delay(200, TestCancellationToken);
        Assert.Equal(0, alertCount);
    }

    [Fact]
    public async Task IncrementTotalAsync_AllThresholds_AllFireOnce()
    {
        var messageBus = GetService<IMessageBus>();
        var firedThresholds = new System.Collections.Concurrent.ConcurrentBag<int>();
        var countdown = new AsyncCountdownEvent(3);
        await messageBus.SubscribeAsync<OrganizationBudgetAlert>(a =>
        {
            firedThresholds.Add(a.Threshold);
            countdown.Signal();
        }, TestCancellationToken);

        var organization = new Organization
        {
            Name = "Test",
            MaxEventsPerMonth = 1000,
            PlanId = _plans.SmallPlan.Id,
            BudgetAlertSettings = new OrganizationBudgetAlertSettings { Enabled = true, Thresholds = new SortedSet<int> { 50, 80, 90 } }
        };
        organization = await _organizationRepository.AddAsync(organization, o => o.ImmediateConsistency().Cache());
        var project = await _projectRepository.AddAsync(new Project { Name = "Test", OrganizationId = organization.Id, NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Ticks }, o => o.ImmediateConsistency().Cache());

        await _usageService.GetEventsLeftAsync(organization.Id);
        await _usageService.IncrementTotalAsync(organization.Id, project.Id, 950);
        await countdown.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(3, firedThresholds.Count);
        Assert.Contains(50, firedThresholds);
        Assert.Contains(80, firedThresholds);
        Assert.Contains(90, firedThresholds);
    }

    [Fact]
    public async Task GetEventIngestAllowanceAsync_SmallPercentageLimit_UsesRoundUp_NotZero()
    {
        // Regression: 1% of 50 events floored to 0 with (int)cast, making the project
        // permanently over-budget on the first event. Math.Ceiling must be used.
        var organization = new Organization { Name = "Test", MaxEventsPerMonth = 50, PlanId = _plans.SmallPlan.Id };
        organization = await _organizationRepository.AddAsync(organization, o => o.ImmediateConsistency().Cache());
        var project = await _projectRepository.AddAsync(new Project
        {
            Name = "Test",
            OrganizationId = organization.Id,
            NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Ticks,
            IngestLimit = new ProjectIngestLimit { Type = ProjectIngestLimitType.PercentOfOrganizationLimit, PercentOfOrganizationLimit = 1 }
        }, o => o.ImmediateConsistency().Cache());

        // 1% of 50 = 0.5 → must ceil to 1, not floor to 0
        var result = await _usageService.GetEventIngestAllowanceAsync(organization.Id, project.Id);
        Assert.True(result.EffectiveProjectLimit >= 1, $"EffectiveProjectLimit was {result.EffectiveProjectLimit}, expected >= 1 (floor bug would produce 0)");
    }

    [Fact]
    public async Task GetEventIngestAllowanceAsync_DecimalPercentage_UsesExactCeiling()
    {
        var organization = await _organizationRepository.AddAsync(new Organization { Name = "Test", MaxEventsPerMonth = 3000, PlanId = _plans.SmallPlan.Id }, o => o.ImmediateConsistency().Cache());
        var project = await _projectRepository.AddAsync(new Project
        {
            Name = "Test",
            OrganizationId = organization.Id,
            NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Ticks,
            IngestLimit = new ProjectIngestLimit { Type = ProjectIngestLimitType.PercentOfOrganizationLimit, PercentOfOrganizationLimit = 1.1m }
        }, o => o.ImmediateConsistency().Cache());

        var result = await _usageService.GetEventIngestAllowanceAsync(organization, project);

        Assert.Equal(33, result.EffectiveProjectLimit);
    }

    [Fact]
    public void GetBudgetThresholdEventCount_ExactPercentage_DoesNotRoundPastInteger()
    {
        Assert.Equal(210, UsageService.GetBudgetThresholdEventCount(3000, 7));
        Assert.Equal(5250, UsageService.GetBudgetThresholdEventCount(75000, 7));
    }

    [Fact]
    public async Task ReserveEventIngestAsync_ConcurrentProjectCapReservations_DoNotExceedRemainingLimit()
    {
        var organization = await _organizationRepository.AddAsync(new Organization { Name = "Test", MaxEventsPerMonth = 1000, PlanId = _plans.SmallPlan.Id }, o => o.ImmediateConsistency().Cache());
        var project = await _projectRepository.AddAsync(new Project
        {
            Name = "Test",
            OrganizationId = organization.Id,
            NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Ticks,
            IngestLimit = new ProjectIngestLimit { Type = ProjectIngestLimitType.Fixed, FixedLimit = 100 }
        }, o => o.ImmediateConsistency().Cache());
        await _usageService.IncrementTotalAsync(organization, project.Id, 90);

        var candidates = Enumerable.Range(0, 10).Select(index => new EventIngestCandidate(index, (ulong)index)).ToArray();
        var reservations = await Task.WhenAll(Enumerable.Range(0, 10).Select(index =>
            _usageService.ReserveEventIngestAsync(organization, project, $"reservation-{index}", candidates, TestCancellationToken)));

        Assert.Equal(10, reservations.Sum(reservation => reservation.ReservedCount));

        await Task.WhenAll(reservations.Select(_usageService.ReleaseEventIngestReservationAsync));
    }

    [Fact]
    public async Task CompleteEventIngestReservationAsync_ReleasesUnprocessedCapacity()
    {
        var organization = await _organizationRepository.AddAsync(new Organization { Name = "Test", MaxEventsPerMonth = 1000, PlanId = _plans.SmallPlan.Id }, o => o.ImmediateConsistency().Cache());
        var project = await _projectRepository.AddAsync(new Project
        {
            Name = "Test",
            OrganizationId = organization.Id,
            NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Ticks,
            IngestLimit = new ProjectIngestLimit { Type = ProjectIngestLimitType.Fixed, FixedLimit = 100 }
        }, o => o.ImmediateConsistency().Cache());
        await _usageService.IncrementTotalAsync(organization, project.Id, 90);
        var candidates = Enumerable.Range(0, 10).Select(index => new EventIngestCandidate(index, (ulong)index)).ToArray();
        var first = await _usageService.ReserveEventIngestAsync(organization, project, "first", candidates, TestCancellationToken);

        await _usageService.CompleteEventIngestReservationAsync(first, organization, 4);
        var second = await _usageService.ReserveEventIngestAsync(organization, project, "second", candidates, TestCancellationToken);

        Assert.Equal(6, second.ReservedCount);
        await _usageService.ReleaseEventIngestReservationAsync(second);
    }

    [Fact]
    public async Task ReserveEventIngestAsync_AfterRelease_RecreatesReservationWithoutDoubleDecrement()
    {
        var organization = await _organizationRepository.AddAsync(new Organization { Name = "Retry", MaxEventsPerMonth = 1000, PlanId = _plans.SmallPlan.Id }, o => o.ImmediateConsistency().Cache());
        var project = await _projectRepository.AddAsync(new Project
        {
            Name = "Retry",
            OrganizationId = organization.Id,
            NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Ticks,
            IngestLimit = new ProjectIngestLimit { Type = ProjectIngestLimitType.Fixed, FixedLimit = 10 }
        }, o => o.ImmediateConsistency().Cache());
        var candidates = Enumerable.Range(0, 10).Select(index => new EventIngestCandidate(index, (ulong)index)).ToArray();
        var first = await _usageService.ReserveEventIngestAsync(organization, project, "retry", candidates, TestCancellationToken);
        await _usageService.ReleaseEventIngestReservationAsync(first);

        var retried = await _usageService.ReserveEventIngestAsync(organization, project, "retry", candidates, TestCancellationToken);
        var competing = await _usageService.ReserveEventIngestAsync(organization, project, "competing", candidates, TestCancellationToken);

        Assert.Equal(10, retried.ReservedCount);
        Assert.Equal(0, competing.ReservedCount);
        await _usageService.ReleaseEventIngestReservationAsync(retried);
        await _usageService.CompleteEventIngestReservationAsync(competing, organization, 0);
    }

    [Fact]
    public async Task ReserveEventIngestAsync_AfterCompletion_ReturnsCompletedTombstone()
    {
        var organization = await _organizationRepository.AddAsync(new Organization { Name = "Completed", MaxEventsPerMonth = 100, PlanId = _plans.SmallPlan.Id }, o => o.ImmediateConsistency().Cache());
        var project = await _projectRepository.AddAsync(new Project { Name = "Completed", OrganizationId = organization.Id, NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Ticks }, o => o.ImmediateConsistency().Cache());
        var candidates = Enumerable.Range(0, 2).Select(index => new EventIngestCandidate(index, (ulong)index)).ToArray();
        var reservation = await _usageService.ReserveEventIngestAsync(organization, project, "completed", candidates, TestCancellationToken);
        await _usageService.CompleteEventIngestReservationAsync(reservation, organization, 2);

        var retry = await _usageService.ReserveEventIngestAsync(organization, project, "completed", candidates, TestCancellationToken);

        Assert.True(retry.IsCompleted);
        Assert.Equal(2, retry.ProcessedCount);
    }

    [Fact]
    public async Task ReserveEventIngestAsync_ColdBucketNoisyBatch_IsSampledImmediately()
    {
        var organization = await _organizationRepository.AddAsync(new Organization { Name = "Test", MaxEventsPerMonth = 1_000_000, PlanId = _plans.ExtraLargePlan.Id }, o => o.ImmediateConsistency().Cache());
        var project = await _projectRepository.AddAsync(new Project { Name = "Noisy", OrganizationId = organization.Id, NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Ticks }, o => o.ImmediateConsistency().Cache());
        await _projectRepository.AddAsync(new Project { Name = "Quiet", OrganizationId = organization.Id, NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Ticks }, o => o.ImmediateConsistency().Cache());
        await _projectRepository.AddAsync(new Project { Name = "Other", OrganizationId = organization.Id, NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Ticks }, o => o.ImmediateConsistency().Cache());
        int spike = 2000;
        var candidates = Enumerable.Range(0, spike)
            .Select(index => new EventIngestCandidate(index, (ulong)index * 7919UL))
            .ToArray();

        var reservation = await _usageService.ReserveEventIngestAsync(organization, project, "cold-bucket", candidates, TestCancellationToken);

        Assert.True(reservation.SmartThrottle.IsThrottled);
        Assert.InRange(reservation.ReservedCount, 60, 140);
        await _usageService.ReleaseEventIngestReservationAsync(reservation);
    }

    [Fact]
    public async Task GetEventIngestAllowanceAsync_FirstMinutesOfMonth_IgnoresPreviousMonthBucket()
    {
        TimeProvider.SetUtcNow(new DateTimeOffset(2026, 7, 31, 23, 59, 0, TimeSpan.Zero));
        var organization = await _organizationRepository.AddAsync(new Organization { Name = "Test", MaxEventsPerMonth = 1000, PlanId = _plans.SmallPlan.Id }, o => o.ImmediateConsistency().Cache());
        var project = await _projectRepository.AddAsync(new Project { Name = "Test", OrganizationId = organization.Id, NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Ticks }, o => o.ImmediateConsistency().Cache());
        await _usageService.IncrementTotalAsync(organization, project.Id, 900);
        TimeProvider.Advance(TimeSpan.FromMinutes(3));

        var result = await _usageService.GetEventIngestAllowanceAsync(organization, project);

        Assert.Equal(1000, result.EventsLeft);
    }

    [Fact]
    public async Task CompleteEventIngestReservationAsync_AfterMonthBoundary_ChargesCurrentPeriodWithoutReleasingCapacity()
    {
        TimeProvider.SetUtcNow(new DateTimeOffset(2026, 7, 31, 23, 59, 0, TimeSpan.Zero));
        var organization = await _organizationRepository.AddAsync(new Organization { Name = "Boundary", MaxEventsPerMonth = 100, PlanId = _plans.SmallPlan.Id }, o => o.ImmediateConsistency().Cache());
        var project = await _projectRepository.AddAsync(new Project { Name = "Boundary", OrganizationId = organization.Id, NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Ticks }, o => o.ImmediateConsistency().Cache());
        var candidates = Enumerable.Range(0, 10).Select(index => new EventIngestCandidate(index, (ulong)index)).ToArray();
        var reservation = await _usageService.ReserveEventIngestAsync(organization, project, "month-boundary", candidates, TestCancellationToken);

        TimeProvider.Advance(TimeSpan.FromMinutes(2));
        await _usageService.CompleteEventIngestReservationAsync(reservation, organization, 10);

        Assert.Equal(90, (await _usageService.GetEventIngestAllowanceAsync(organization, project)).EventsLeft);
    }

    [Fact]
    public async Task CompleteEventIngestReservationAsync_MissingState_FailsClosed()
    {
        var organization = await _organizationRepository.AddAsync(new Organization { Name = "Missing", MaxEventsPerMonth = 100, PlanId = _plans.SmallPlan.Id }, o => o.ImmediateConsistency().Cache());
        var project = await _projectRepository.AddAsync(new Project { Name = "Missing", OrganizationId = organization.Id, NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Ticks }, o => o.ImmediateConsistency().Cache());
        var reservation = await _usageService.ReserveEventIngestAsync(organization, project, "missing-state", [new EventIngestCandidate(0, 0)], TestCancellationToken);
        await GetService<ICacheClient>().RemoveAsync($"usage:ingest-reservation:{{{organization.Id}}}:missing-state");

        await Assert.ThrowsAsync<UsageServiceException>(() => _usageService.CompleteEventIngestReservationAsync(reservation, organization, 1));
    }
}
