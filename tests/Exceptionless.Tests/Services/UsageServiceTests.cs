using System.Diagnostics;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Exceptionless.Core;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Ingestion;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Tests.Extensions;
using Foundatio.AsyncEx;
using Foundatio.Caching;
using Foundatio.Lock;
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
    public async Task SavePendingUsageAsync_OrganizationCleanupFailsMidBucket_RetriesFailedAndRemainingOrganizations()
    {
        var pendingUsage = await CreatePendingUsageAsync();
        DateTime bucketUtc = TimeProvider.GetUtcNow().UtcDateTime.Floor(TimeSpan.FromMinutes(5));
        var cache = GetService<ICacheClient>();
        string discoveryKey = $"usage:{bucketUtc.ToEpoch()}:organizations";
        var discovery = await cache.GetListAsync<string>(discoveryKey);
        string[] orderedIds = Assert.IsAssignableFrom<IEnumerable<string>>(discovery.Value).ToArray();
        Assert.Equal(3, orderedIds.Length);
        string completedId = orderedIds[0];
        string failedId = orderedIds[1];
        string remainingId = orderedIds[2];
        var expectedById = pendingUsage.ToDictionary(item => item.Organization.Id, item => item.EventCount, StringComparer.Ordinal);
        var failingCache = OneShotFailureCacheProxy.Create(
            cache,
            (method, arguments) => IsCounterCleanupFor(method, arguments, failedId));
        UsageService usageService = CreateUsageService(failingCache);
        TimeProvider.Advance(TimeSpan.FromMinutes(10));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => usageService.SavePendingUsageAsync());
        Assert.Equal(OneShotFailureCacheProxy.FailureMessage, exception.Message);

        Assert.Equal(expectedById[completedId], (await _organizationRepository.GetByIdAsync(completedId))!.Usage.Single().Total);
        Assert.Equal(expectedById[failedId], (await _organizationRepository.GetByIdAsync(failedId))!.Usage.Single().Total);
        Assert.Empty((await _organizationRepository.GetByIdAsync(remainingId))!.Usage);
        var retryDiscovery = await cache.GetListAsync<string>(discoveryKey);
        Assert.DoesNotContain(completedId, retryDiscovery.Value);
        Assert.Contains(failedId, retryDiscovery.Value);
        Assert.Contains(remainingId, retryDiscovery.Value);

        await usageService.SavePendingUsageAsync();

        foreach (var item in pendingUsage)
        {
            var organization = await _organizationRepository.GetByIdAsync(item.Organization.Id);
            Assert.Equal(item.EventCount, organization!.Usage.Single().Total);
        }

        var completedDiscovery = await cache.GetListAsync<string>(discoveryKey);
        Assert.True(!completedDiscovery.HasValue || completedDiscovery.Value.Count == 0);
    }

    [Fact]
    public async Task SavePendingUsageAsync_ProjectCleanupFailsMidBucket_RetriesFailedAndRemainingProjects()
    {
        var pendingUsage = await CreatePendingUsageAsync();
        DateTime bucketUtc = TimeProvider.GetUtcNow().UtcDateTime.Floor(TimeSpan.FromMinutes(5));
        var cache = GetService<ICacheClient>();
        string discoveryKey = $"usage:{bucketUtc.ToEpoch()}:projects";
        var discovery = await cache.GetListAsync<string>(discoveryKey);
        string[] orderedIds = Assert.IsAssignableFrom<IEnumerable<string>>(discovery.Value).ToArray();
        Assert.Equal(3, orderedIds.Length);
        string completedId = orderedIds[0];
        string failedId = orderedIds[1];
        string remainingId = orderedIds[2];
        var expectedById = pendingUsage.ToDictionary(item => item.Project.Id, item => item.EventCount, StringComparer.Ordinal);
        var failingCache = OneShotFailureCacheProxy.Create(
            cache,
            (method, arguments) => IsCounterCleanupFor(method, arguments, failedId));
        UsageService usageService = CreateUsageService(failingCache);
        TimeProvider.Advance(TimeSpan.FromMinutes(10));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => usageService.SavePendingUsageAsync());
        Assert.Equal(OneShotFailureCacheProxy.FailureMessage, exception.Message);

        Assert.Equal(expectedById[completedId], (await _projectRepository.GetByIdAsync(completedId))!.Usage.Single().Total);
        Assert.Equal(expectedById[failedId], (await _projectRepository.GetByIdAsync(failedId))!.Usage.Single().Total);
        Assert.Empty((await _projectRepository.GetByIdAsync(remainingId))!.Usage);
        var retryDiscovery = await cache.GetListAsync<string>(discoveryKey);
        Assert.DoesNotContain(completedId, retryDiscovery.Value);
        Assert.Contains(failedId, retryDiscovery.Value);
        Assert.Contains(remainingId, retryDiscovery.Value);

        await usageService.SavePendingUsageAsync();

        foreach (var item in pendingUsage)
        {
            var project = await _projectRepository.GetByIdAsync(item.Project.Id);
            Assert.Equal(item.EventCount, project!.Usage.Single().Total);
        }

        var completedDiscovery = await cache.GetListAsync<string>(discoveryKey);
        Assert.True(!completedDiscovery.HasValue || completedDiscovery.Value.Count == 0);
    }

    [Fact]
    public async Task SavePendingUsageAsync_OrganizationProcessedMarkerWriteFailsAfterSave_RetryDoesNotApplyBucketTwice()
    {
        DateTime bucketUtc = TimeProvider.GetUtcNow().UtcDateTime.Floor(TimeSpan.FromMinutes(5));
        var organization = await _organizationRepository.AddAsync(new Organization
        {
            Name = "Organization durable usage marker",
            MaxEventsPerMonth = 750,
            PlanId = _plans.SmallPlan.Id
        }, options => options.ImmediateConsistency().Cache());
        var project = await _projectRepository.AddAsync(new Project
        {
            Name = "Organization durable usage marker",
            OrganizationId = organization.Id,
            NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Ticks
        }, options => options.ImmediateConsistency().Cache());
        await _usageService.IncrementTotalAsync(organization.Id, project.Id, 3);
        var cache = GetService<ICacheClient>();
        var failingCache = OneShotFailureCacheProxy.Create(
            cache,
            (method, arguments) => IsProcessedMarkerWriteFor(method, arguments, organization.Id));
        UsageService usageService = CreateUsageService(failingCache);
        TimeProvider.Advance(TimeSpan.FromMinutes(10));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => usageService.SavePendingUsageAsync());
        Assert.Equal(OneShotFailureCacheProxy.FailureMessage, exception.Message);

        organization = await _organizationRepository.GetByIdAsync(organization.Id);
        Assert.NotNull(organization);
        Assert.Equal(bucketUtc, organization.LastAppliedUsageBucketUtc);
        Assert.Equal(3, organization.Usage.Single().Total);
        Assert.Empty((await _projectRepository.GetByIdAsync(project.Id))!.Usage);

        await usageService.SavePendingUsageAsync();

        organization = await _organizationRepository.GetByIdAsync(organization.Id);
        project = await _projectRepository.GetByIdAsync(project.Id);
        Assert.NotNull(organization);
        Assert.NotNull(project);
        Assert.Equal(bucketUtc, organization.LastAppliedUsageBucketUtc);
        Assert.Equal(bucketUtc, project.LastAppliedUsageBucketUtc);
        Assert.Equal(3, organization.Usage.Single().Total);
        Assert.Equal(3, project.Usage.Single().Total);
    }

    [Fact]
    public async Task SavePendingUsageAsync_ProjectProcessedMarkerWriteFailsAfterSave_RetryDoesNotApplyBucketTwice()
    {
        DateTime bucketUtc = TimeProvider.GetUtcNow().UtcDateTime.Floor(TimeSpan.FromMinutes(5));
        var organization = await _organizationRepository.AddAsync(new Organization
        {
            Name = "Project durable usage marker",
            MaxEventsPerMonth = 750,
            PlanId = _plans.SmallPlan.Id
        }, options => options.ImmediateConsistency().Cache());
        var project = await _projectRepository.AddAsync(new Project
        {
            Name = "Project durable usage marker",
            OrganizationId = organization.Id,
            NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Ticks
        }, options => options.ImmediateConsistency().Cache());
        await _usageService.IncrementTotalAsync(organization.Id, project.Id, 4);
        var cache = GetService<ICacheClient>();
        var failingCache = OneShotFailureCacheProxy.Create(
            cache,
            (method, arguments) => IsProcessedMarkerWriteFor(method, arguments, project.Id));
        UsageService usageService = CreateUsageService(failingCache);
        TimeProvider.Advance(TimeSpan.FromMinutes(10));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => usageService.SavePendingUsageAsync());
        Assert.Equal(OneShotFailureCacheProxy.FailureMessage, exception.Message);

        organization = await _organizationRepository.GetByIdAsync(organization.Id);
        project = await _projectRepository.GetByIdAsync(project.Id);
        Assert.NotNull(organization);
        Assert.NotNull(project);
        Assert.Equal(bucketUtc, organization.LastAppliedUsageBucketUtc);
        Assert.Equal(bucketUtc, project.LastAppliedUsageBucketUtc);
        Assert.Equal(4, organization.Usage.Single().Total);
        Assert.Equal(4, project.Usage.Single().Total);

        await usageService.SavePendingUsageAsync();

        organization = await _organizationRepository.GetByIdAsync(organization.Id);
        project = await _projectRepository.GetByIdAsync(project.Id);
        Assert.NotNull(organization);
        Assert.NotNull(project);
        Assert.Equal(bucketUtc, organization.LastAppliedUsageBucketUtc);
        Assert.Equal(bucketUtc, project.LastAppliedUsageBucketUtc);
        Assert.Equal(4, organization.Usage.Single().Total);
        Assert.Equal(4, project.Usage.Single().Total);
    }

    [Fact]
    public async Task SavePendingUsageAsync_LegacyNullMarkers_AppliesFirstAndNextBucketsOnce()
    {
        DateTime firstBucketUtc = TimeProvider.GetUtcNow().UtcDateTime.Floor(TimeSpan.FromMinutes(5));
        var organization = await _organizationRepository.AddAsync(new Organization
        {
            Name = "Legacy usage marker",
            MaxEventsPerMonth = 750,
            PlanId = _plans.SmallPlan.Id
        }, options => options.ImmediateConsistency().Cache());
        var project = await _projectRepository.AddAsync(new Project
        {
            Name = "Legacy usage marker",
            OrganizationId = organization.Id,
            NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Ticks
        }, options => options.ImmediateConsistency().Cache());
        Assert.Null(organization.LastAppliedUsageBucketUtc);
        Assert.Null(project.LastAppliedUsageBucketUtc);

        await _usageService.IncrementTotalAsync(organization.Id, project.Id, 1);
        TimeProvider.Advance(TimeSpan.FromMinutes(10));
        await _usageService.SavePendingUsageAsync();

        organization = await _organizationRepository.GetByIdAsync(organization.Id);
        project = await _projectRepository.GetByIdAsync(project.Id);
        Assert.NotNull(organization);
        Assert.NotNull(project);
        Assert.Equal(firstBucketUtc, organization.LastAppliedUsageBucketUtc);
        Assert.Equal(firstBucketUtc, project.LastAppliedUsageBucketUtc);
        Assert.Equal(1, organization.Usage.Single().Total);
        Assert.Equal(1, project.Usage.Single().Total);

        DateTime secondBucketUtc = TimeProvider.GetUtcNow().UtcDateTime.Floor(TimeSpan.FromMinutes(5));
        await _usageService.IncrementTotalAsync(organization.Id, project.Id, 2);
        TimeProvider.Advance(TimeSpan.FromMinutes(10));
        await _usageService.SavePendingUsageAsync();

        organization = await _organizationRepository.GetByIdAsync(organization.Id);
        project = await _projectRepository.GetByIdAsync(project.Id);
        Assert.NotNull(organization);
        Assert.NotNull(project);
        Assert.Equal(secondBucketUtc, organization.LastAppliedUsageBucketUtc);
        Assert.Equal(secondBucketUtc, project.LastAppliedUsageBucketUtc);
        Assert.Equal(3, organization.Usage.Single().Total);
        Assert.Equal(3, project.Usage.Single().Total);
    }

    [Fact]
    public async Task ReserveEventsAsync_ConcurrentCallers_DoNotOverReserve()
    {
        var organization = await _organizationRepository.AddAsync(new Organization
        {
            Name = "Concurrent reservation",
            MaxEventsPerMonth = 750,
            PlanId = _plans.SmallPlan.Id
        }, o => o.ImmediateConsistency().Cache());
        int available = await _usageService.GetEventsLeftAsync(organization.Id);

        int[] reservations = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(async _ => (await _usageService.ReserveEventsAsync(organization.Id, available)).Count));

        Assert.Equal(available, reservations.Sum());
        var activeReservations = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => _usageService.ReserveEventsAsync(organization.Id, available)));
        Assert.All(activeReservations, reservation => Assert.Equal(0, reservation.Count));

        TimeProvider.Advance(TimeSpan.FromMinutes(11));
        var afterExpiry = await _usageService.ReserveEventsAsync(organization.Id, available);
        Assert.Equal(available, afterExpiry.Count);
        await _usageService.ReleaseEventReservationAsync(afterExpiry);
    }

    [Fact]
    public async Task ReserveEventsAsync_BucketRollsBeforeLeaseExpires_DoesNotReuseCapacity()
    {
        var organization = await _organizationRepository.AddAsync(new Organization
        {
            Name = "Bucket rollover reservation",
            MaxEventsPerMonth = 750,
            PlanId = _plans.SmallPlan.Id
        }, o => o.ImmediateConsistency().Cache());
        int available = await _usageService.GetEventsLeftAsync(organization.Id);
        var first = await _usageService.ReserveEventsAsync(organization.Id, available);
        Assert.Equal(available, first.Count);

        TimeProvider.Advance(TimeSpan.FromMinutes(5));
        var nextBucket = await _usageService.ReserveEventsAsync(organization.Id, available);

        Assert.Equal(0, nextBucket.Count);
        await _usageService.ReleaseEventReservationAsync(first);
        var afterRelease = await _usageService.ReserveEventsAsync(organization.Id, available);
        Assert.Equal(available, afterRelease.Count);
        await _usageService.ReleaseEventReservationAsync(afterRelease);
    }

    [Fact]
    public async Task ReserveEventsAsync_PlanLimitIncreases_PreservesOutstandingCapacity()
    {
        var organization = await _organizationRepository.AddAsync(new Organization
        {
            Name = "Plan change reservation",
            MaxEventsPerMonth = 750,
            PlanId = _plans.SmallPlan.Id
        }, o => o.ImmediateConsistency().Cache());
        int originalAvailable = await _usageService.GetEventsLeftAsync(organization.Id);
        var first = await _usageService.ReserveEventsAsync(organization.Id, originalAvailable);
        Assert.Equal(750, first.Count);

        organization.MaxEventsPerMonth = 1000;
        await _organizationRepository.SaveAsync(organization, o => o.ImmediateConsistency().Cache());
        await GetService<ICacheClient>().RemoveAsync($"usage:limits:{organization.Id}");
        int increasedAvailable = await _usageService.GetEventsLeftAsync(organization.Id);
        var afterPlanIncrease = await _usageService.ReserveEventsAsync(organization.Id, increasedAvailable);

        Assert.Equal(1000, increasedAvailable);
        Assert.Equal(250, afterPlanIncrease.Count);
        await _usageService.ReleaseEventReservationAsync(first);
        await _usageService.ReleaseEventReservationAsync(afterPlanIncrease);
    }

    [Fact]
    public async Task ReserveEventsAsync_PartialCapacity_IsAdmittedDeterministically()
    {
        var organization = await _organizationRepository.AddAsync(new Organization
        {
            Name = "Partial reservation",
            MaxEventsPerMonth = 750,
            PlanId = _plans.SmallPlan.Id
        }, o => o.ImmediateConsistency().Cache());
        int available = await _usageService.GetEventsLeftAsync(organization.Id);

        var first = await _usageService.ReserveEventsAsync(organization.Id, available - 1);
        var second = await _usageService.ReserveEventsAsync(organization.Id, 5);

        Assert.Equal(available - 1, first.Count);
        Assert.Equal(1, second.Count);
        await _usageService.ReleaseEventReservationAsync(first);
        await _usageService.ReleaseEventReservationAsync(second);

        // Releasing an already released lease is a no-op and cannot create negative capacity.
        await _usageService.ReleaseEventReservationAsync(second);
        var third = await _usageService.ReserveEventsAsync(organization.Id, available);
        Assert.Equal(available, third.Count);
        await _usageService.ReleaseEventReservationAsync(third);
    }

    [Fact]
    public async Task IncrementTotalAsync_WriterOwnedV3Settlements_CountsDistinctEventsInCommit()
    {
        var organization = await _organizationRepository.AddAsync(new Organization
        {
            Name = "Idempotent settlement",
            MaxEventsPerMonth = 750,
            PlanId = _plans.SmallPlan.Id
        }, o => o.ImmediateConsistency().Cache());
        var project = await _projectRepository.AddAsync(new Project
        {
            Name = "Idempotent settlement",
            OrganizationId = organization.Id,
            NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Ticks
        }, o => o.ImmediateConsistency().Cache());
        DateTime createdUtc = TimeProvider.GetUtcNow().UtcDateTime;
        EventUsageSettlement[] settlements =
        [
            new("event-1", createdUtc),
            new("event-2", createdUtc),
            new("event-1", createdUtc)
        ];

        await _usageService.IncrementTotalAsync(organization.Id, project.Id, settlements);

        UsageInfoResponse organizationUsage = await _usageService.GetUsageAsync(organization.Id);
        UsageInfoResponse projectUsage = await _usageService.GetUsageAsync(organization.Id, project.Id);
        Assert.Equal(2, organizationUsage.CurrentUsage.Total);
        Assert.Equal(2, projectUsage.CurrentUsage.Total);
    }

    [Fact]
    public async Task IncrementTotalAsync_LateWriterOwnedSettlementAfterBucketSave_MovesToCurrentBucket()
    {
        var organization = await _organizationRepository.AddAsync(new Organization
        {
            Name = "Late settlement",
            MaxEventsPerMonth = 750,
            PlanId = _plans.SmallPlan.Id
        }, o => o.ImmediateConsistency().Cache());
        var project = await _projectRepository.AddAsync(new Project
        {
            Name = "Late settlement",
            OrganizationId = organization.Id,
            NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Ticks
        }, o => o.ImmediateConsistency().Cache());
        DateTime originalBucketUtc = TimeProvider.GetUtcNow().UtcDateTime;
        var first = new EventUsageSettlement("event-1", originalBucketUtc);

        await _usageService.IncrementTotalAsync(organization.Id, project.Id, [first]);
        TimeProvider.Advance(TimeSpan.FromMinutes(11));
        await _usageService.SavePendingUsageAsync();

        await _usageService.IncrementTotalAsync(organization.Id, project.Id,
            [new EventUsageSettlement("event-2", originalBucketUtc)]);

        UsageInfoResponse organizationUsage = await _usageService.GetUsageAsync(organization.Id);
        UsageInfoResponse projectUsage = await _usageService.GetUsageAsync(organization.Id, project.Id);
        Assert.Equal(2, organizationUsage.CurrentUsage.Total);
        Assert.Equal(2, projectUsage.CurrentUsage.Total);
    }

    [Fact]
    public async Task IncrementTotalAsync_SettlementPastSafetyWindow_FailsOpen()
    {
        var organization = await _organizationRepository.AddAsync(new Organization
        {
            Name = "Expired idempotency settlement",
            MaxEventsPerMonth = 750,
            PlanId = _plans.SmallPlan.Id
        }, o => o.ImmediateConsistency().Cache());
        var project = await _projectRepository.AddAsync(new Project
        {
            Name = "Expired idempotency settlement",
            OrganizationId = organization.Id,
            NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Ticks
        }, o => o.ImmediateConsistency().Cache());
        DateTime createdUtc = TimeProvider.GetUtcNow().UtcDateTime;
        var settlement = new EventUsageSettlement("event-expired", createdUtc);

        await _usageService.IncrementTotalAsync(organization.Id, project.Id, [settlement]);
        TimeProvider.Advance(TimeSpan.FromMinutes(11));
        await _usageService.SavePendingUsageAsync();

        TimeSpan idempotencyWindow = GetService<AppOptions>().EventIngestionV3.IdempotencyWindow;
        TimeProvider.Advance(idempotencyWindow.Subtract(TimeSpan.FromMinutes(11)).Add(TimeSpan.FromSeconds(1)));
        await _usageService.IncrementTotalAsync(organization.Id, project.Id, [settlement]);
        Assert.Equal(1, (await _usageService.GetUsageAsync(organization.Id)).CurrentUsage.Total);
        Assert.Equal(1, (await _usageService.GetUsageAsync(organization.Id, project.Id)).CurrentUsage.Total);

        // The durable event age still prevents reconstruction of an old, already-closed bucket
        // after the processed marker expires.
        TimeProvider.Advance(TimeSpan.FromMinutes(15));
        await _usageService.IncrementTotalAsync(organization.Id, project.Id, [settlement]);
        Assert.Equal(1, (await _usageService.GetUsageAsync(organization.Id)).CurrentUsage.Total);
        Assert.Equal(1, (await _usageService.GetUsageAsync(organization.Id, project.Id)).CurrentUsage.Total);
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

    private async Task<List<PendingUsage>> CreatePendingUsageAsync()
    {
        var result = new List<PendingUsage>();
        for (int index = 0; index < 3; index++)
        {
            var organization = await _organizationRepository.AddAsync(new Organization
            {
                Name = $"Retry organization {index}",
                MaxEventsPerMonth = 750,
                PlanId = _plans.SmallPlan.Id
            }, options => options.ImmediateConsistency().Cache());
            var project = await _projectRepository.AddAsync(new Project
            {
                Name = $"Retry project {index}",
                OrganizationId = organization.Id,
                NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Ticks
            }, options => options.ImmediateConsistency().Cache());
            int eventCount = index + 1;
            await _usageService.IncrementTotalAsync(organization.Id, project.Id, eventCount);
            result.Add(new PendingUsage(organization, project, eventCount));
        }

        return result;
    }

    private UsageService CreateUsageService(ICacheClient cache) => new(
        _organizationRepository,
        _projectRepository,
        cache,
        GetService<IIngestionQuotaStore>(),
        GetService<IMessagePublisher>(),
        _notificationService,
        GetService<ILockProvider>(),
        GetService<AppOptions>(),
        TimeProvider,
        Log);

    private static bool IsCounterCleanupFor(MethodInfo method, object?[]? arguments, string entityId)
    {
        return String.Equals(method.Name, nameof(ICacheClient.RemoveAllAsync), StringComparison.Ordinal)
            && arguments is { Length: > 0 }
            && arguments[0] is IEnumerable<string> keys
            && keys.Any(key => key.Contains(entityId, StringComparison.Ordinal));
    }

    private static bool IsProcessedMarkerWriteFor(MethodInfo method, object?[]? arguments, string entityId)
    {
        return String.Equals(method.Name, nameof(ICacheClient.SetAsync), StringComparison.Ordinal)
            && arguments is { Length: > 0 }
            && arguments[0] is string key
            && key.EndsWith(":total:v3:processed", StringComparison.Ordinal)
            && key.Contains(entityId, StringComparison.Ordinal);
    }

    private sealed record PendingUsage(Organization Organization, Project Project, int EventCount);

    private class OneShotFailureCacheProxy : DispatchProxy
    {
        public const string FailureMessage = "Injected usage cache failure.";
        private ICacheClient _inner = null!;
        private Func<MethodInfo, object?[]?, bool> _shouldFail = null!;
        private int _failureAvailable;

        public static ICacheClient Create(ICacheClient inner, Func<MethodInfo, object?[]?, bool> shouldFail)
        {
            ICacheClient proxy = DispatchProxy.Create<ICacheClient, OneShotFailureCacheProxy>();
            var implementation = (OneShotFailureCacheProxy)(object)proxy;
            implementation._inner = inner;
            implementation._shouldFail = shouldFail;
            implementation._failureAvailable = 1;
            return proxy;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            if (Volatile.Read(ref _failureAvailable) == 1
                && _shouldFail(targetMethod, args)
                && Interlocked.Exchange(ref _failureAvailable, 0) == 1)
            {
                throw new InvalidOperationException(FailureMessage);
            }

            try
            {
                return targetMethod.Invoke(_inner, args);
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw;
            }
        }
    }
}
