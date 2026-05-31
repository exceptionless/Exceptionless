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
using Foundatio.Messaging;
using Foundatio.Repositories;
using Xunit;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Exceptionless.Tests.Services;

public sealed class UsageServiceTests : IntegrationTestsBase
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly UsageService _usageService;
    private readonly NotificationService _notificationService;
    private readonly BillingPlans _plans;

    public UsageServiceTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        TimeProvider.SetUtcNow(new DateTime(2015, 2, 13, 0, 0, 0, DateTimeKind.Utc));
        Log.SetLogLevel<OrganizationRepository>(LogLevel.Information);
        _usageService = GetService<UsageService>();
        _notificationService = GetService<NotificationService>();
        _organizationRepository = GetService<IOrganizationRepository>();
        _projectRepository = GetService<IProjectRepository>();
        _plans = GetService<BillingPlans>();
    }

    private Task SetMonthlySentMarkerAsync(string organizationId)
    {
        return _notificationService.SetOrganizationNotificationSentAsync(organizationId, isOverMonthlyLimit: true);
    }

    private Task<bool> MonthlySentMarkerExistsAsync(string organizationId)
    {
        return _notificationService.IsOrganizationNotificationSentAsync(organizationId, isOverMonthlyLimit: true);
    }

    [Fact]
    public async Task HandleOrganizationChangeAsync_WhenMonthlyPlanLimitIncreases_ShouldResetOverageNotificationSentMarker()
    {
        // Arrange
        var original = new Organization
        {
            Id = "664ec4c1f12e4f2b7a0d3001",
            Name = "Primary Organization",
            PlanId = _plans.SmallPlan.Id,
            MaxEventsPerMonth = 100
        };

        var modified = new Organization
        {
            Id = original.Id,
            Name = original.Name,
            PlanId = _plans.MediumPlan.Id,
            MaxEventsPerMonth = 200
        };

        await SetMonthlySentMarkerAsync(original.Id);

        // Act
        await _usageService.HandleOrganizationChangeAsync(modified, original);

        // Assert
        Assert.False(await MonthlySentMarkerExistsAsync(original.Id));
    }

    [Fact]
    public async Task HandleOrganizationChangeAsync_WhenMonthlyPlanLimitIncreasesButOrganizationIsStillOverLimit_ShouldKeepOverageNotificationSentMarker()
    {
        // Arrange
        var original = new Organization
        {
            Id = "664ec4c1f12e4f2b7a0d3005",
            Name = "Primary Organization",
            PlanId = _plans.SmallPlan.Id,
            MaxEventsPerMonth = 100
        };
        original.GetCurrentUsage(TimeProvider).Total = 250;

        var modified = new Organization
        {
            Id = original.Id,
            Name = original.Name,
            PlanId = _plans.MediumPlan.Id,
            MaxEventsPerMonth = 200
        };
        modified.GetCurrentUsage(TimeProvider).Total = 250;

        await SetMonthlySentMarkerAsync(original.Id);

        // Act
        await _usageService.HandleOrganizationChangeAsync(modified, original);

        // Assert
        Assert.True(await MonthlySentMarkerExistsAsync(original.Id));
    }

    [Fact]
    public async Task HandleOrganizationChangeAsync_WhenPlanChangesToUnlimited_ShouldResetOverageNotificationSentMarker()
    {
        // Arrange
        var original = new Organization
        {
            Id = "664ec4c1f12e4f2b7a0d3002",
            Name = "Primary Organization",
            PlanId = _plans.EnterprisePlan.Id,
            MaxEventsPerMonth = _plans.EnterprisePlan.MaxEventsPerMonth
        };

        var modified = new Organization
        {
            Id = original.Id,
            Name = original.Name,
            PlanId = _plans.UnlimitedPlan.Id,
            MaxEventsPerMonth = _plans.UnlimitedPlan.MaxEventsPerMonth
        };

        await SetMonthlySentMarkerAsync(original.Id);

        // Act
        await _usageService.HandleOrganizationChangeAsync(modified, original);

        // Assert
        Assert.False(await MonthlySentMarkerExistsAsync(original.Id));
    }

    [Fact]
    public async Task HandleOrganizationChangeAsync_WhenPlanLimitDecreasesBelowCurrentUsage_ShouldResetMarkerAndPublishMonthlyOverage()
    {
        // Arrange
        var messageBus = GetService<IMessageBus>();
        var countdown = new AsyncCountdownEvent(1);
        PlanOverage? overage = null;
        await messageBus.SubscribeAsync<PlanOverage>(po =>
        {
            overage = po;
            countdown.Signal();
        }, TestCancellationToken);

        var original = new Organization
        {
            Id = "664ec4c1f12e4f2b7a0d3003",
            Name = "Primary Organization",
            PlanId = _plans.MediumPlan.Id,
            MaxEventsPerMonth = 200
        };

        var modified = new Organization
        {
            Id = original.Id,
            Name = original.Name,
            PlanId = _plans.SmallPlan.Id,
            MaxEventsPerMonth = 100
        };
        modified.GetCurrentUsage(TimeProvider).Total = 150;

        await SetMonthlySentMarkerAsync(original.Id);

        // Act
        await _usageService.HandleOrganizationChangeAsync(modified, original);
        await countdown.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.False(await MonthlySentMarkerExistsAsync(original.Id));
        Assert.NotNull(overage);
        Assert.Equal(modified.Id, overage.OrganizationId);
        Assert.False(overage.IsHourly);
    }

    [Fact]
    public async Task HandleOrganizationChangeAsync_WhenAlreadyOverMonthlyLimitAndPlanLimitDecreases_ShouldKeepMarkerAndNotPublishMonthlyOverage()
    {
        // Arrange
        var messageBus = GetService<IMessageBus>();
        var countdown = new AsyncCountdownEvent(1);
        await messageBus.SubscribeAsync<PlanOverage>(po =>
        {
            if (!po.IsHourly)
                countdown.Signal();
        }, TestCancellationToken);

        var original = new Organization
        {
            Id = "664ec4c1f12e4f2b7a0d3004",
            Name = "Primary Organization",
            PlanId = _plans.MediumPlan.Id,
            MaxEventsPerMonth = 200
        };
        original.GetCurrentUsage(TimeProvider).Total = 250;

        var modified = new Organization
        {
            Id = original.Id,
            Name = original.Name,
            PlanId = _plans.SmallPlan.Id,
            MaxEventsPerMonth = 100
        };
        modified.GetCurrentUsage(TimeProvider).Total = 250;

        await SetMonthlySentMarkerAsync(original.Id);

        // Act
        await _usageService.HandleOrganizationChangeAsync(modified, original);

        // Assert
        await Assert.ThrowsAsync<TimeoutException>(() => countdown.WaitAsync(TimeSpan.FromSeconds(1)));
        Assert.True(await MonthlySentMarkerExistsAsync(original.Id));
    }

    [Fact]
    public async Task CanIncrementUsageAsync()
    {
        var messageBus = GetService<IMessageBus>();

        var countdown = new AsyncCountdownEvent(2);
        await messageBus.SubscribeAsync<PlanOverage>(po =>
        {
            _logger.LogInformation("Plan Overage for {Organization} (Hourly: {IsHourly})", po.OrganizationId, po.IsHourly);
            countdown.Signal();
        }, TestCancellationToken);

        var organization = await _organizationRepository.AddAsync(new Organization { Name = "Test", MaxEventsPerMonth = 750, PlanId = _plans.SmallPlan.Id }, o => o.ImmediateConsistency().Cache());
        var project = await _projectRepository.AddAsync(new Project { Name = "Test", OrganizationId = organization.Id, NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Ticks }, o => o.ImmediateConsistency().Cache());
        int eventsLeftInBucket = await _usageService.GetEventsLeftAsync(organization.Id);
        Assert.InRange(eventsLeftInBucket, 1, 750);
        Assert.Empty(organization.Usage);
        Assert.Empty(organization.UsageHours);

        int totalToIncrement = eventsLeftInBucket - 1;
        await _usageService.IncrementTotalAsync(organization.Id, project.Id, totalToIncrement);
        await Assert.ThrowsAsync<TimeoutException>(async () => await countdown.WaitAsync(TimeSpan.FromMilliseconds(150)));
        Assert.Equal(2, countdown.CurrentCount);

        int eventsLeft = await _usageService.GetEventsLeftAsync(organization.Id);
        Assert.Equal(1, eventsLeft);

        await _usageService.IncrementTotalAsync(organization.Id, project.Id, eventsLeft);
        await countdown.WaitAsync(TimeSpan.FromMilliseconds(150));
        Assert.Equal(0, countdown.CurrentCount);

        eventsLeft = await _usageService.GetEventsLeftAsync(organization.Id);
        Assert.Equal(0, eventsLeft);

        // move clock forward so that pending usages are saved
        TimeProvider.Advance(TimeSpan.FromMinutes(10));

        await _usageService.SavePendingUsageAsync();
        organization = await _organizationRepository.GetByIdAsync(organization.Id);
        Assert.NotNull(organization);
        Assert.Single(organization.UsageHours);
        var usage = organization.Usage.Single();
        Assert.Equal(organization.MaxEventsPerMonth, usage.Limit);
        Assert.Equal(eventsLeftInBucket, usage.Total);
        Assert.Equal(0, usage.Blocked);
        Assert.Equal(0, usage.TooBig);
        Assert.Equal(0, usage.Deleted);

        project = await _projectRepository.GetByIdAsync(project.Id);
        Assert.NotNull(project);
        Assert.Single(project.UsageHours);
        usage = project.Usage.Single();
        Assert.Equal(eventsLeftInBucket, usage.Total);
        Assert.Equal(0, usage.Blocked);
        Assert.Equal(0, usage.TooBig);
        Assert.Equal(0, usage.Deleted);
    }

    [Fact]
    public async Task CanGetEventsLeft()
    {
        var organization = await _organizationRepository.AddAsync(new Organization { Name = "Test", MaxEventsPerMonth = 750, PlanId = _plans.SmallPlan.Id }, o => o.ImmediateConsistency().Cache());
        var project = await _projectRepository.AddAsync(new Project { Name = "Test", OrganizationId = organization.Id, NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Ticks }, o => o.ImmediateConsistency().Cache());
        int eventsLeftInBucket = await _usageService.GetEventsLeftAsync(organization.Id);
        Assert.InRange(eventsLeftInBucket, 1, 750);
        Assert.Empty(organization.Usage);
        Assert.Empty(organization.UsageHours);

        await _usageService.IncrementTotalAsync(organization.Id, project.Id, eventsLeftInBucket);

        int eventsLeft = await _usageService.GetEventsLeftAsync(organization.Id);
        Assert.Equal(0, eventsLeft);

        // move clock forward past current time bucket
        TimeProvider.Advance(TimeSpan.FromMinutes(5));

        eventsLeft = await _usageService.GetEventsLeftAsync(organization.Id);
        Assert.Equal(0, eventsLeft);
    }

    [Fact]
    public async Task CanIncrementOverageUsageAsync()
    {
        var messageBus = GetService<IMessageBus>();

        var countdown = new AsyncCountdownEvent(2);
        await messageBus.SubscribeAsync<PlanOverage>(po =>
        {
            _logger.LogInformation("Plan Overage for {Organization} (Hourly: {IsHourly})", po.OrganizationId, po.IsHourly);
            countdown.Signal();
        }, TestCancellationToken);

        var organization = await _organizationRepository.AddAsync(new Organization { Name = "Test", MaxEventsPerMonth = 750, PlanId = _plans.SmallPlan.Id }, o => o.ImmediateConsistency().Cache());
        var project = await _projectRepository.AddAsync(new Project { Name = "Test", OrganizationId = organization.Id, NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Ticks }, o => o.ImmediateConsistency().Cache());
        int eventsLeftInBucket = await _usageService.GetEventsLeftAsync(organization.Id);
        Assert.InRange(eventsLeftInBucket, 1, 750);
        Assert.Empty(organization.Usage);
        Assert.Empty(organization.UsageHours);

        await _usageService.IncrementTotalAsync(organization.Id, project.Id, eventsLeftInBucket + 1);
        await countdown.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(0, countdown.CurrentCount);

        await _usageService.IncrementBlockedAsync(organization.Id, project.Id, 1);

        int eventsLeft = await _usageService.GetEventsLeftAsync(organization.Id);
        Assert.Equal(0, eventsLeft);

        // move clock forward so that pending usages are saved
        TimeProvider.Advance(TimeSpan.FromMinutes(10));

        await _usageService.SavePendingUsageAsync();
        organization = await _organizationRepository.GetByIdAsync(organization.Id);
        Assert.NotNull(organization);
        var overage = organization.UsageHours.Single();
        Assert.Equal(eventsLeftInBucket + 1, overage.Total);
        Assert.Equal(1, overage.Blocked);
        Assert.Equal(0, overage.TooBig);

        var usage = organization.Usage.Single();
        Assert.Equal(organization.MaxEventsPerMonth, usage.Limit);
        Assert.Equal(eventsLeftInBucket + 1, usage.Total);
        Assert.Equal(1, usage.Blocked);
        Assert.Equal(0, usage.TooBig);

        project = await _projectRepository.GetByIdAsync(project.Id);
        Assert.NotNull(project);
        overage = project.UsageHours.Single();
        Assert.Equal(eventsLeftInBucket + 1, overage.Total);
        Assert.Equal(1, overage.Blocked);
        Assert.Equal(0, overage.TooBig);

        usage = project.Usage.Single();
        Assert.Equal(eventsLeftInBucket + 1, usage.Total);
        Assert.Equal(1, usage.Blocked);
        Assert.Equal(0, usage.TooBig);

        await _usageService.IncrementBlockedAsync(organization.Id, project.Id, 1000);

        // move clock forward so that pending usages are saved
        TimeProvider.Advance(TimeSpan.FromMinutes(10));

        await _usageService.SavePendingUsageAsync();
        organization = await _organizationRepository.GetByIdAsync(organization.Id);
        Assert.NotNull(organization);
        overage = organization.UsageHours.Single();
        Assert.Equal(eventsLeftInBucket + 1, overage.Total);
        Assert.Equal(1001, overage.Blocked);
        Assert.Equal(0, overage.TooBig);

        usage = organization.Usage.Single();
        Assert.Equal(organization.MaxEventsPerMonth, usage.Limit);
        Assert.Equal(eventsLeftInBucket + 1, usage.Total);
        Assert.Equal(1001, usage.Blocked);
        Assert.Equal(0, usage.TooBig);

        project = await _projectRepository.GetByIdAsync(project.Id);
        Assert.NotNull(project);
        overage = project.UsageHours.Single();
        Assert.Equal(eventsLeftInBucket + 1, overage.Total);
        Assert.Equal(1001, overage.Blocked);
        Assert.Equal(0, overage.TooBig);

        usage = project.Usage.Single();
        Assert.Equal(eventsLeftInBucket + 1, usage.Total);
        Assert.Equal(1001, usage.Blocked);
        Assert.Equal(0, usage.TooBig);
    }

    [Fact]
    public async Task CanIncrementBlockedAsync()
    {
        var organization = await _organizationRepository.AddAsync(new Organization { Name = "Test", MaxEventsPerMonth = 750, PlanId = _plans.SmallPlan.Id }, o => o.ImmediateConsistency().Cache());
        var project = await _projectRepository.AddAsync(new Project { Name = "Test", OrganizationId = organization.Id, NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Ticks }, o => o.ImmediateConsistency().Cache());

        await _usageService.IncrementBlockedAsync(organization.Id, project.Id);

        // move clock forward so that pending usages are saved
        TimeProvider.Advance(TimeSpan.FromMinutes(10));

        await _usageService.SavePendingUsageAsync();
        organization = await _organizationRepository.GetByIdAsync(organization.Id);
        Assert.NotNull(organization);
        Assert.Single(organization.UsageHours);
        var usage = organization.Usage.Single();
        Assert.Equal(organization.MaxEventsPerMonth, usage.Limit);
        Assert.Equal(0, usage.Total);
        Assert.Equal(1, usage.Blocked);
        Assert.Equal(0, usage.TooBig);
        Assert.Equal(0, usage.Deleted);
        var overage = organization.UsageHours.Single();
        Assert.Equal(0, overage.Total);
        Assert.Equal(1, overage.Blocked);
        Assert.Equal(0, overage.TooBig);
        Assert.Equal(0, overage.Deleted);

        project = await _projectRepository.GetByIdAsync(project.Id);
        Assert.NotNull(project);
        Assert.Single(project.UsageHours);

        usage = project.Usage.Single();
        Assert.Equal(0, usage.Total);
        Assert.Equal(1, usage.Blocked);
        Assert.Equal(0, usage.TooBig);
        Assert.Equal(0, usage.Deleted);

        overage = project.UsageHours.Single();
        Assert.Equal(0, overage.Total);
        Assert.Equal(1, overage.Blocked);
        Assert.Equal(0, overage.TooBig);
        Assert.Equal(0, overage.Deleted);
    }

    [Fact]
    public async Task CanIncrementDiscardedAsync()
    {
        var organization = await _organizationRepository.AddAsync(new Organization { Name = "Test", MaxEventsPerMonth = 750, PlanId = _plans.SmallPlan.Id }, o => o.ImmediateConsistency().Cache());
        var project = await _projectRepository.AddAsync(new Project { Name = "Test", OrganizationId = organization.Id, NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Ticks }, o => o.ImmediateConsistency().Cache());

        await _usageService.IncrementDiscardedAsync(organization.Id, project.Id);

        // move clock forward so that pending usages are saved
        TimeProvider.Advance(TimeSpan.FromMinutes(10));

        await _usageService.SavePendingUsageAsync();
        organization = await _organizationRepository.GetByIdAsync(organization.Id);
        Assert.NotNull(organization);
        Assert.Single(organization.UsageHours);
        var usage = organization.Usage.Single();
        Assert.Equal(organization.MaxEventsPerMonth, usage.Limit);
        Assert.Equal(0, usage.Total);
        Assert.Equal(1, usage.Discarded);
        Assert.Equal(0, usage.TooBig);
        Assert.Equal(0, usage.Deleted);
        var overage = organization.UsageHours.Single();
        Assert.Equal(0, overage.Total);
        Assert.Equal(1, overage.Discarded);
        Assert.Equal(0, overage.TooBig);
        Assert.Equal(0, overage.Deleted);

        project = await _projectRepository.GetByIdAsync(project.Id);
        Assert.NotNull(project);
        Assert.Single(project.UsageHours);

        usage = project.Usage.Single();
        Assert.Equal(0, usage.Total);
        Assert.Equal(1, usage.Discarded);
        Assert.Equal(0, usage.TooBig);
        Assert.Equal(0, usage.Deleted);

        overage = project.UsageHours.Single();
        Assert.Equal(0, overage.Total);
        Assert.Equal(1, overage.Discarded);
        Assert.Equal(0, overage.TooBig);
        Assert.Equal(0, overage.Deleted);
    }

    [Fact]
    public async Task CanIncrementTooBigAsync()
    {
        var organization = await _organizationRepository.AddAsync(new Organization { Name = "Test", MaxEventsPerMonth = 750, PlanId = _plans.SmallPlan.Id }, o => o.ImmediateConsistency().Cache());
        var project = await _projectRepository.AddAsync(new Project { Name = "Test", OrganizationId = organization.Id, NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Ticks }, o => o.ImmediateConsistency().Cache());

        await _usageService.IncrementTooBigAsync(organization.Id, project.Id);

        // move clock forward so that pending usages are saved
        TimeProvider.Advance(TimeSpan.FromMinutes(10));

        await _usageService.SavePendingUsageAsync();
        organization = await _organizationRepository.GetByIdAsync(organization.Id);
        Assert.NotNull(organization);
        Assert.Single(organization.UsageHours);
        var usage = organization.Usage.Single();
        Assert.Equal(organization.MaxEventsPerMonth, usage.Limit);
        Assert.Equal(0, usage.Total);
        Assert.Equal(0, usage.Blocked);
        Assert.Equal(1, usage.TooBig);
        Assert.Equal(0, usage.Deleted);

        project = await _projectRepository.GetByIdAsync(project.Id);
        Assert.NotNull(project);
        Assert.Single(project.UsageHours);
        usage = project.Usage.Single();
        Assert.Equal(0, usage.Total);
        Assert.Equal(0, usage.Blocked);
        Assert.Equal(1, usage.TooBig);
        Assert.Equal(0, usage.Deleted);
    }

    [Fact]
    public async Task IncrementDeletedAsync_WithProjectId_PersistsOrgAndProjectUsage()
    {
        // Arrange
        var organization = await _organizationRepository.AddAsync(new Organization { Name = "Test", MaxEventsPerMonth = 750, PlanId = _plans.SmallPlan.Id }, o => o.ImmediateConsistency().Cache());
        var project = await _projectRepository.AddAsync(new Project { Name = "Test", OrganizationId = organization.Id, NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Ticks }, o => o.ImmediateConsistency().Cache());

        // Act
        await _usageService.IncrementDeletedAsync(organization.Id, project.Id, 5);
        TimeProvider.Advance(TimeSpan.FromMinutes(10));
        await _usageService.SavePendingUsageAsync();

        // Assert
        organization = await _organizationRepository.GetByIdAsync(organization.Id);
        Assert.NotNull(organization);
        Assert.Single(organization.UsageHours);
        var usage = organization.Usage.Single();
        Assert.Equal(organization.MaxEventsPerMonth, usage.Limit);
        Assert.Equal(0, usage.Total);
        Assert.Equal(0, usage.Blocked);
        Assert.Equal(0, usage.TooBig);
        Assert.Equal(0, usage.Discarded);
        Assert.Equal(5, usage.Deleted);
        var hourUsage = organization.UsageHours.Single();
        Assert.Equal(0, hourUsage.Total);
        Assert.Equal(0, hourUsage.Blocked);
        Assert.Equal(0, hourUsage.TooBig);
        Assert.Equal(0, hourUsage.Discarded);
        Assert.Equal(5, hourUsage.Deleted);

        project = await _projectRepository.GetByIdAsync(project.Id);
        Assert.NotNull(project);
        Assert.Single(project.UsageHours);
        usage = project.Usage.Single();
        Assert.Equal(0, usage.Total);
        Assert.Equal(0, usage.Blocked);
        Assert.Equal(0, usage.TooBig);
        Assert.Equal(0, usage.Discarded);
        Assert.Equal(5, usage.Deleted);
        hourUsage = project.UsageHours.Single();
        Assert.Equal(0, hourUsage.Total);
        Assert.Equal(0, hourUsage.Blocked);
        Assert.Equal(0, hourUsage.TooBig);
        Assert.Equal(0, hourUsage.Discarded);
        Assert.Equal(5, hourUsage.Deleted);
    }

    [Fact]
    public async Task IncrementDeletedAsync_WithoutProjectId_OnlyIncrementsOrgUsage()
    {
        // Arrange
        var organization = await _organizationRepository.AddAsync(new Organization { Name = "Test", MaxEventsPerMonth = 750, PlanId = _plans.SmallPlan.Id }, o => o.ImmediateConsistency().Cache());
        var project = await _projectRepository.AddAsync(new Project { Name = "Test", OrganizationId = organization.Id, NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Ticks }, o => o.ImmediateConsistency().Cache());

        // Act
        await _usageService.IncrementDeletedAsync(organization.Id, null, 10);
        TimeProvider.Advance(TimeSpan.FromMinutes(10));
        await _usageService.SavePendingUsageAsync();

        // Assert
        organization = await _organizationRepository.GetByIdAsync(organization.Id);
        Assert.NotNull(organization);
        Assert.Single(organization.UsageHours);
        var usage = organization.Usage.Single();
        Assert.Equal(10, usage.Deleted);
        Assert.Equal(0, usage.Total);

        project = await _projectRepository.GetByIdAsync(project.Id);
        Assert.NotNull(project);
        Assert.Empty(project.Usage);
    }

    [Fact]
    public async Task IncrementDeletedAsync_LargeCount_DoesNotReduceEventsLeft()
    {
        // Arrange
        var organization = await _organizationRepository.AddAsync(new Organization { Name = "Test", MaxEventsPerMonth = 750, PlanId = _plans.SmallPlan.Id }, o => o.ImmediateConsistency().Cache());
        var project = await _projectRepository.AddAsync(new Project { Name = "Test", OrganizationId = organization.Id, NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Ticks }, o => o.ImmediateConsistency().Cache());

        int eventsLeftBefore = await _usageService.GetEventsLeftAsync(organization.Id);

        // Act
        await _usageService.IncrementDeletedAsync(organization.Id, project.Id, 100);

        // Assert
        int eventsLeftAfter = await _usageService.GetEventsLeftAsync(organization.Id);
        Assert.Equal(eventsLeftBefore, eventsLeftAfter);
    }

    [Fact]
    public async Task IncrementDeletedAsync_MultipleCalls_AccumulatesCorrectly()
    {
        // Arrange
        var organization = await _organizationRepository.AddAsync(new Organization { Name = "Test", MaxEventsPerMonth = 750, PlanId = _plans.SmallPlan.Id }, o => o.ImmediateConsistency().Cache());
        var project = await _projectRepository.AddAsync(new Project { Name = "Test", OrganizationId = organization.Id, NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Ticks }, o => o.ImmediateConsistency().Cache());

        // Act
        await _usageService.IncrementDeletedAsync(organization.Id, project.Id, 3);
        await _usageService.IncrementDeletedAsync(organization.Id, project.Id, 7);
        TimeProvider.Advance(TimeSpan.FromMinutes(10));
        await _usageService.SavePendingUsageAsync();

        // Assert
        organization = await _organizationRepository.GetByIdAsync(organization.Id);
        Assert.NotNull(organization);
        var usage = organization.Usage.Single();
        Assert.Equal(10, usage.Deleted);

        project = await _projectRepository.GetByIdAsync(project.Id);
        Assert.NotNull(project);
        usage = project.Usage.Single();
        Assert.Equal(10, usage.Deleted);
    }

    [Fact]
    public async Task GetUsageAsync_PendingDeleted_IncludesInCurrentUsage()
    {
        // Arrange
        var organization = await _organizationRepository.AddAsync(new Organization { Name = "Test", MaxEventsPerMonth = 750, PlanId = _plans.SmallPlan.Id }, o => o.ImmediateConsistency().Cache());
        var project = await _projectRepository.AddAsync(new Project { Name = "Test", OrganizationId = organization.Id, NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Ticks }, o => o.ImmediateConsistency().Cache());

        // Act
        await _usageService.IncrementDeletedAsync(organization.Id, project.Id, 5);

        // Assert
        var usageResponse = await _usageService.GetUsageAsync(organization.Id);
        Assert.Equal(5, usageResponse.CurrentUsage.Deleted);
        Assert.Equal(5, usageResponse.CurrentHourUsage.Deleted);

        var projectUsageResponse = await _usageService.GetUsageAsync(organization.Id, project.Id);
        Assert.Equal(5, projectUsageResponse.CurrentUsage.Deleted);
        Assert.Equal(5, projectUsageResponse.CurrentHourUsage.Deleted);
    }

    [Fact]
    public async Task RunBenchmarkAsync()
    {
        const int iterations = 10000;
        var organization = await _organizationRepository.AddAsync(new Organization { Name = "Test", MaxEventsPerMonth = iterations - 10, PlanId = _plans.ExtraLargePlan.Id }, o => o.ImmediateConsistency());
        var project = await _projectRepository.AddAsync(new Project { Name = "Test", OrganizationId = organization.Id, NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Ticks }, o => o.ImmediateConsistency());

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            int eventsLeft = await _usageService.GetEventsLeftAsync(organization.Id);
            await _usageService.IncrementTotalAsync(organization.Id, project.Id);
        }

        sw.Stop();
        _logger.LogInformation("Time: {Duration:g}, Avg: ({AverageTickDuration:g}ticks | {AverageDuration}ms)", sw.Elapsed, sw.ElapsedTicks / iterations, sw.ElapsedMilliseconds / iterations);
    }

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
        Assert.Equal(10000, result.EffectiveProjectLimit);
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
    public async Task GetEventIngestAllowanceAsync_Project_WithPercentageIngestLimit_150Percent_CappedAtOrgLimit()
    {
        var organization = await _organizationRepository.AddAsync(new Organization { Name = "Test", MaxEventsPerMonth = 1000, PlanId = _plans.SmallPlan.Id }, o => o.ImmediateConsistency().Cache());
        var project = await _projectRepository.AddAsync(new Project
        {
            Name = "Test",
            OrganizationId = organization.Id,
            NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Ticks,
            IngestLimit = new ProjectIngestLimit { Type = ProjectIngestLimitType.PercentOfOrganizationLimit, PercentOfOrganizationLimit = 150 }
        }, o => o.ImmediateConsistency().Cache());

        var result = await _usageService.GetEventIngestAllowanceAsync(organization.Id, project.Id);

        Assert.Equal(1500, result.EffectiveProjectLimit);
        Assert.True(result.EventsLeft <= 1000);
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
    public async Task GetSmartThrottleRateAsync_AboveThreshold_2xFairShare_SampleRateHalf()
    {
        var organization = new Organization { Name = "Test", MaxEventsPerMonth = 1000, PlanId = _plans.SmallPlan.Id };
        organization.GetCurrentUsage(TimeProvider).Total = 900;
        organization = await _organizationRepository.AddAsync(organization, o => o.ImmediateConsistency().Cache());
        var project1 = await _projectRepository.AddAsync(new Project { Name = "P1", OrganizationId = organization.Id, NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Ticks }, o => o.ImmediateConsistency().Cache());
        var project2 = await _projectRepository.AddAsync(new Project { Name = "P2", OrganizationId = organization.Id, NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Ticks }, o => o.ImmediateConsistency().Cache());
        // fairShare = 1000/2 = 500; projectTotal = 1250 → fairShareRatio = 2.5; sampleRate = 1/2.5 = 0.4
        project1.GetCurrentUsage(TimeProvider).Total = 1250;
        await _projectRepository.SaveAsync(project1, o => o.ImmediateConsistency().Cache());

        var result = await _usageService.GetSmartThrottleRateAsync(organization.Id, project1.Id);

        Assert.True(result.IsThrottled);
        Assert.InRange(result.SampleRate, 0.35, 0.55);
        _ = project2;
    }

    [Fact]
    public async Task GetSmartThrottleRateAsync_AboveThreshold_4xFairShare_SampleRateQuarter()
    {
        var organization = new Organization { Name = "Test", MaxEventsPerMonth = 1000, PlanId = _plans.SmallPlan.Id };
        organization.GetCurrentUsage(TimeProvider).Total = 900;
        organization = await _organizationRepository.AddAsync(organization, o => o.ImmediateConsistency().Cache());
        var project1 = await _projectRepository.AddAsync(new Project { Name = "P1", OrganizationId = organization.Id, NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Ticks }, o => o.ImmediateConsistency().Cache());
        var project2 = await _projectRepository.AddAsync(new Project { Name = "P2", OrganizationId = organization.Id, NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Ticks }, o => o.ImmediateConsistency().Cache());
        // fairShare = 1000/2 = 500; projectTotal = 2000 → fairShareRatio = 4.0; sampleRate = 1/4 = 0.25
        project1.GetCurrentUsage(TimeProvider).Total = 2000;
        await _projectRepository.SaveAsync(project1, o => o.ImmediateConsistency().Cache());

        var result = await _usageService.GetSmartThrottleRateAsync(organization.Id, project1.Id);

        Assert.True(result.IsThrottled);
        Assert.Equal(0.25, result.SampleRate);
        _ = project2;
    }

    [Fact]
    public async Task GetSmartThrottleRateAsync_AboveThreshold_10xFairShare_SampleRateMin()
    {
        var organization = new Organization { Name = "Test", MaxEventsPerMonth = 1000, PlanId = _plans.SmallPlan.Id };
        organization.GetCurrentUsage(TimeProvider).Total = 900;
        organization = await _organizationRepository.AddAsync(organization, o => o.ImmediateConsistency().Cache());
        var project1 = await _projectRepository.AddAsync(new Project { Name = "P1", OrganizationId = organization.Id, NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Ticks }, o => o.ImmediateConsistency().Cache());
        var project2 = await _projectRepository.AddAsync(new Project { Name = "P2", OrganizationId = organization.Id, NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Ticks }, o => o.ImmediateConsistency().Cache());
        // fairShare = 1000/2 = 500; projectTotal = 5000 → fairShareRatio = 10.0; sampleRate = max(0.1, 0.1) = 0.1
        project1.GetCurrentUsage(TimeProvider).Total = 5000;
        await _projectRepository.SaveAsync(project1, o => o.ImmediateConsistency().Cache());

        var result = await _usageService.GetSmartThrottleRateAsync(organization.Id, project1.Id);

        Assert.True(result.IsThrottled);
        Assert.Equal(0.1, result.SampleRate);
        _ = project2;
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
            alertCount++;
            if (alertCount == 1) firstAlert.Signal();
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
    public async Task IncrementTotalAsync_DisabledAlerts_DoNotPublishMessages()
    {
        var messageBus = GetService<IMessageBus>();
        int alertCount = 0;
        await messageBus.SubscribeAsync<OrganizationBudgetAlert>(_ => { alertCount++; }, TestCancellationToken);

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
}
