using System.Diagnostics;
using Exceptionless.Tests.Extensions;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Models;
using Exceptionless.Core.Services;
using Exceptionless.Tests.Utility;
using Foundatio.AsyncEx;
using Foundatio.Caching;
using Foundatio.Messaging;
using Foundatio.Repositories;
using Foundatio.Utility;
using Xunit;
using Xunit.Abstractions;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Exceptionless.Tests.Services;

public sealed class UsageServiceTests : IntegrationTestsBase {
    private readonly ICacheClient _cache;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly UsageService _usageService;
    private readonly BillingPlans _plans;

    public UsageServiceTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory) {
        Log.SetLogLevel<OrganizationRepository>(LogLevel.Information);
        _cache = GetService<ICacheClient>();
        _usageService = GetService<UsageService>();
        _organizationRepository = GetService<IOrganizationRepository>();
        _projectRepository = GetService<IProjectRepository>();
        _plans = GetService<BillingPlans>();
    }

    [Fact]
    public async Task CanIncrementUsageAsync() {
        var messageBus = GetService<IMessageBus>();

        var countdown = new AsyncCountdownEvent(2);
        await messageBus.SubscribeAsync<PlanOverage>(po => {
            _logger.LogInformation("Plan Overage for {organization} (Hourly: {IsHourly})", po.OrganizationId, po.IsHourly);
            countdown.Signal();
        });

        var organization = await _organizationRepository.AddAsync(new Organization { Name = "Test", MaxEventsPerMonth = 750, PlanId = _plans.SmallPlan.Id }, o => o.ImmediateConsistency());
        var project = await _projectRepository.AddAsync(new Project { Name = "Test", OrganizationId = organization.Id, NextSummaryEndOfDayTicks = SystemClock.UtcNow.Ticks }, o => o.ImmediateConsistency());
        Assert.InRange(organization.GetHourlyEventLimit(_plans), 1, 750);

        int totalToIncrement = organization.GetHourlyEventLimit(_plans) - 1;
        Assert.False(await _usageService.IncrementUsageAsync(organization, project, false, totalToIncrement));
        organization = await _organizationRepository.GetByIdAsync(organization.Id);

        await countdown.WaitAsync(TimeSpan.FromMilliseconds(150));
        Assert.Equal(2, countdown.CurrentCount);
        var organizationUsage = await _usageService.GetUsageAsync(organization);
        var projectUsage = await _usageService.GetUsageAsync(organization, project);
        Assert.Equal(totalToIncrement, organizationUsage.HourlyTotal);
        Assert.Equal(totalToIncrement, projectUsage.HourlyTotal);
        Assert.Equal(totalToIncrement, organizationUsage.MonthlyTotal);
        Assert.Equal(totalToIncrement, projectUsage.MonthlyTotal);
        Assert.Equal(0, organizationUsage.HourlyBlocked);
        Assert.Equal(0, projectUsage.HourlyBlocked);
        Assert.Equal(0, organizationUsage.MonthlyBlocked);
        Assert.Equal(0, projectUsage.MonthlyBlocked);

        Assert.True(await _usageService.IncrementUsageAsync(organization, project, false, 2));
        await countdown.WaitAsync(TimeSpan.FromMilliseconds(150));
        Assert.Equal(1, countdown.CurrentCount);

        organizationUsage = await _usageService.GetUsageAsync(organization);
        projectUsage = await _usageService.GetUsageAsync(organization, project);
        Assert.Equal(totalToIncrement + 2, organizationUsage.HourlyTotal);
        Assert.Equal(totalToIncrement + 2, projectUsage.HourlyTotal);
        Assert.Equal(totalToIncrement + 2, organizationUsage.MonthlyTotal);
        Assert.Equal(totalToIncrement + 2, projectUsage.MonthlyTotal);
        Assert.Equal(1, organizationUsage.HourlyBlocked);
        Assert.Equal(1, projectUsage.HourlyBlocked);
        Assert.Equal(1, organizationUsage.MonthlyBlocked);
        Assert.Equal(1, projectUsage.MonthlyBlocked);

        organization = await _organizationRepository.AddAsync(new Organization { Name = "Test", MaxEventsPerMonth = 750, PlanId = _plans.SmallPlan.Id }, o => o.ImmediateConsistency());
        project = await _projectRepository.AddAsync(new Project { Name = "Test", OrganizationId = organization.Id, NextSummaryEndOfDayTicks = SystemClock.UtcNow.Ticks }, o => o.ImmediateConsistency());

        await _cache.RemoveAllAsync();
        totalToIncrement = organization.GetHourlyEventLimit(_plans) + 20;
        Assert.True(await _usageService.IncrementUsageAsync(organization, project, false, totalToIncrement));

        await countdown.WaitAsync(TimeSpan.FromMilliseconds(150));
        Assert.Equal(0, countdown.CurrentCount);

        organizationUsage = await _usageService.GetUsageAsync(organization);
        projectUsage = await _usageService.GetUsageAsync(organization, project);
        Assert.Equal(totalToIncrement, organizationUsage.HourlyTotal);
        Assert.Equal(totalToIncrement, projectUsage.HourlyTotal);
        Assert.Equal(totalToIncrement, organizationUsage.MonthlyTotal);
        Assert.Equal(totalToIncrement, projectUsage.MonthlyTotal);
        Assert.Equal(20, organizationUsage.HourlyBlocked);
        Assert.Equal(20, projectUsage.HourlyBlocked);
        Assert.Equal(20, organizationUsage.MonthlyBlocked);
        Assert.Equal(20, projectUsage.MonthlyBlocked);
    }

    [Fact]
    public async Task CanHandleZeroUsage() {
        var organization = await _organizationRepository.AddAsync(new Organization { Name = "Test", MaxEventsPerMonth = 750, PlanId = _plans.SmallPlan.Id }, o => o.ImmediateConsistency());
        var project = await _projectRepository.AddAsync(new Project { Name = "Test", OrganizationId = organization.Id, NextSummaryEndOfDayTicks = SystemClock.UtcNow.Ticks }, o => o.ImmediateConsistency());
        Assert.InRange(organization.GetHourlyEventLimit(_plans), 1, 750);

        Assert.False(await _usageService.IncrementUsageAsync(organization, project, tooBig: false, 0, applyHourlyLimit: false));
        var organizationUsage = await _usageService.GetUsageAsync(organization);
        var projectUsage = await _usageService.GetUsageAsync(organization, project);
        Assert.Equal(0, organizationUsage.HourlyTotal);
        Assert.Equal(0, projectUsage.HourlyTotal);
        Assert.Equal(0, organizationUsage.MonthlyTotal);
        Assert.Equal(0, projectUsage.MonthlyTotal);
        Assert.Equal(0, organizationUsage.HourlyBlocked);
        Assert.Equal(0, projectUsage.HourlyBlocked);
        Assert.Equal(0, organizationUsage.MonthlyBlocked);
        Assert.Equal(0, projectUsage.MonthlyBlocked);
        Assert.Equal(0, organizationUsage.HourlyTooBig);
        Assert.Equal(0, projectUsage.HourlyTooBig);
        Assert.Equal(0, organizationUsage.MonthlyTooBig);
        Assert.Equal(0, projectUsage.MonthlyTooBig);

        Assert.False(await _usageService.IncrementUsageAsync(organization, project, tooBig: true, 0, applyHourlyLimit: false));
        organizationUsage = await _usageService.GetUsageAsync(organization); 
        projectUsage = await _usageService.GetUsageAsync(organization, project);
        Assert.Equal(0, organizationUsage.HourlyTotal);
        Assert.Equal(0, projectUsage.HourlyTotal);
        Assert.Equal(0, organizationUsage.MonthlyTotal);
        Assert.Equal(0, projectUsage.MonthlyTotal);
        Assert.Equal(0, organizationUsage.HourlyBlocked);
        Assert.Equal(0, projectUsage.HourlyBlocked);
        Assert.Equal(0, organizationUsage.MonthlyBlocked);
        Assert.Equal(0, projectUsage.MonthlyBlocked);
        Assert.Equal(1, organizationUsage.HourlyTooBig);
        Assert.Equal(1, projectUsage.HourlyTooBig);
        Assert.Equal(1, organizationUsage.MonthlyTooBig);
        Assert.Equal(1, projectUsage.MonthlyTooBig);
    }

    [Fact]
    public async Task CanIncrementUsageWithDiscardedValuesAsync() {
        var organization = await _organizationRepository.AddAsync(new Organization { Name = "Test", MaxEventsPerMonth = 750, PlanId = _plans.SmallPlan.Id }, o => o.ImmediateConsistency());
        var project = await _projectRepository.AddAsync(new Project { Name = "Test", OrganizationId = organization.Id, NextSummaryEndOfDayTicks = SystemClock.UtcNow.Ticks }, o => o.ImmediateConsistency());
        Assert.InRange(organization.GetHourlyEventLimit(_plans), 1, 750);
        
        Assert.False(await _usageService.IncrementUsageAsync(organization, project, false, -1));

        var organizationUsage = await _usageService.GetUsageAsync(organization);
        var projectUsage = await _usageService.GetUsageAsync(organization, project);
        Assert.Equal(0, organizationUsage.HourlyTotal);
        Assert.Equal(0, projectUsage.HourlyTotal);
        Assert.Equal(0, organizationUsage.MonthlyTotal);
        Assert.Equal(0, projectUsage.MonthlyTotal);
        Assert.Equal(0, organizationUsage.HourlyBlocked);
        Assert.Equal(0, projectUsage.HourlyBlocked);
        Assert.Equal(0, organizationUsage.MonthlyBlocked);
        Assert.Equal(0, projectUsage.MonthlyBlocked);

        organization = await _organizationRepository.GetByIdAsync(organization.Id);
        Assert.True(organization.Usage.All(u => u.Total == 0 && u.Blocked == 0));
        project = await _projectRepository.GetByIdAsync(project.Id);
        Assert.True(project.Usage.All(u => u.Total == 0 && u.Blocked == 0));
    }

    [Fact]
    public async Task WillNotThrottleFreePlan() {
        var messageBus = GetService<IMessageBus>();

        var countdown = new AsyncCountdownEvent(2);
        await messageBus.SubscribeAsync<PlanOverage>(po => {
            _logger.LogInformation("Plan Overage for {organization} (Hourly: {IsHourly})", po.OrganizationId, po.IsHourly);
            countdown.Signal();
        });

        const int limit = 750;
        var organization = await _organizationRepository.AddAsync(new Organization { Name = "Test", MaxEventsPerMonth = limit, PlanId = _plans.FreePlan.Id }, o => o.ImmediateConsistency());
        var project = await _projectRepository.AddAsync(new Project { Name = "Test", OrganizationId = organization.Id, NextSummaryEndOfDayTicks = SystemClock.UtcNow.Ticks }, o => o.ImmediateConsistency());
        Assert.Equal(limit, organization.GetHourlyEventLimit(_plans));

        Assert.False(await _usageService.IncrementUsageAsync(organization, project, false, limit));
        organization = await _organizationRepository.GetByIdAsync(organization.Id);

        await countdown.WaitAsync(TimeSpan.FromMilliseconds(150));
        Assert.Equal(2, countdown.CurrentCount);

        var organizationUsage = await _usageService.GetUsageAsync(organization);
        var projectUsage = await _usageService.GetUsageAsync(organization, project);
        Assert.Equal(limit, organizationUsage.HourlyTotal);
        Assert.Equal(limit, projectUsage.HourlyTotal);
        Assert.Equal(limit, organizationUsage.MonthlyTotal);
        Assert.Equal(limit, projectUsage.MonthlyTotal);
        Assert.Equal(0, organizationUsage.HourlyBlocked);
        Assert.Equal(0, projectUsage.HourlyBlocked);
        Assert.Equal(0, organizationUsage.MonthlyBlocked);
        Assert.Equal(0, projectUsage.MonthlyBlocked);

        Assert.True(await _usageService.IncrementUsageAsync(organization, project, false, 2));
        organization = await _organizationRepository.GetByIdAsync(organization.Id);

        await countdown.WaitAsync(TimeSpan.FromMilliseconds(150));
        Assert.Equal(1, countdown.CurrentCount);

        organizationUsage = await _usageService.GetUsageAsync(organization);
        projectUsage = await _usageService.GetUsageAsync(organization, project);
        Assert.Equal(limit + 2, organizationUsage.HourlyTotal);
        Assert.Equal(limit + 2, projectUsage.HourlyTotal);
        Assert.Equal(limit + 2, organizationUsage.MonthlyTotal);
        Assert.Equal(limit + 2, projectUsage.MonthlyTotal);
        Assert.Equal(2, organizationUsage.HourlyBlocked);
        Assert.Equal(2, projectUsage.HourlyBlocked);
        Assert.Equal(2, organizationUsage.MonthlyBlocked);
        Assert.Equal(2, projectUsage.MonthlyBlocked);
    }

    [Fact]
    public async Task CanIncrementSuspendedOrganizationUsageAsync() {
        var messageBus = GetService<IMessageBus>();

        var countdown = new AsyncCountdownEvent(2);
        await messageBus.SubscribeAsync<PlanOverage>(po => {
            _logger.LogInformation("Plan Overage for {organization} (Hourly: {IsHourly})", po.OrganizationId, po.IsHourly);
            countdown.Signal();
        });

        var organization = await _organizationRepository.AddAsync(new Organization { Name = "Test", MaxEventsPerMonth = 750, PlanId = _plans.SmallPlan.Id }, o => o.ImmediateConsistency());
        var project = await _projectRepository.AddAsync(new Project { Name = "Test", OrganizationId = organization.Id, NextSummaryEndOfDayTicks = SystemClock.UtcNow.Ticks }, o => o.ImmediateConsistency());
        Assert.False(await _usageService.IncrementUsageAsync(organization, project, false, 5));

        await countdown.WaitAsync(TimeSpan.FromMilliseconds(150));
        Assert.Equal(2, countdown.CurrentCount);

        var organizationUsage = await _usageService.GetUsageAsync(organization);
        var projectUsage = await _usageService.GetUsageAsync(organization, project);
        Assert.Equal(5, organizationUsage.HourlyTotal);
        Assert.Equal(5, projectUsage.HourlyTotal);
        Assert.Equal(5, organizationUsage.MonthlyTotal);
        Assert.Equal(5, projectUsage.MonthlyTotal);
        Assert.Equal(0, organizationUsage.HourlyBlocked);
        Assert.Equal(0, projectUsage.HourlyBlocked);
        Assert.Equal(0, organizationUsage.MonthlyBlocked);
        Assert.Equal(0, projectUsage.MonthlyBlocked);

        organization.IsSuspended = true;
        organization.SuspendedByUserId = TestConstants.UserId;
        organization.SuspensionDate = SystemClock.UtcNow;
        organization.SuspensionCode = SuspensionCode.Billing;
        organization = await _organizationRepository.SaveAsync(organization, o => o.ImmediateConsistency());

        Assert.True(await _usageService.IncrementUsageAsync(organization, project, false, 4995));

        await countdown.WaitAsync(TimeSpan.FromMilliseconds(150));
        Assert.Equal(1, countdown.CurrentCount);

        organizationUsage = await _usageService.GetUsageAsync(organization);
        projectUsage = await _usageService.GetUsageAsync(organization, project);
        Assert.Equal(5000, organizationUsage.HourlyTotal);
        Assert.Equal(5000, projectUsage.HourlyTotal);
        Assert.Equal(5000, organizationUsage.MonthlyTotal);
        Assert.Equal(5000, projectUsage.MonthlyTotal);
        Assert.Equal(4995, organizationUsage.HourlyBlocked);
        Assert.Equal(4995, projectUsage.HourlyBlocked);
        Assert.Equal(4995, organizationUsage.MonthlyBlocked);
        Assert.Equal(4995, projectUsage.MonthlyBlocked);

        organization.RemoveSuspension();
        organization = await _organizationRepository.SaveAsync(organization, o => o.ImmediateConsistency());

        Assert.False(await _usageService.IncrementUsageAsync(organization, project, false, 1));
        await countdown.WaitAsync(TimeSpan.FromMilliseconds(150));
        Assert.Equal(1, countdown.CurrentCount);

        organizationUsage = await _usageService.GetUsageAsync(organization);
        projectUsage = await _usageService.GetUsageAsync(organization, project);
        Assert.Equal(5001, organizationUsage.HourlyTotal);
        Assert.Equal(5001, projectUsage.HourlyTotal);
        Assert.Equal(5001, organizationUsage.MonthlyTotal);
        Assert.Equal(5001, projectUsage.MonthlyTotal);
        Assert.Equal(4995, organizationUsage.HourlyBlocked);
        Assert.Equal(4995, projectUsage.HourlyBlocked);
        Assert.Equal(4995, organizationUsage.MonthlyBlocked);
        Assert.Equal(4995, projectUsage.MonthlyBlocked);
    }

    [Fact]
    public async Task RunBenchmarkAsync() {
        const int iterations = 10000;
        var organization = await _organizationRepository.AddAsync(new Organization { Name = "Test", MaxEventsPerMonth = 1000000, PlanId = _plans.ExtraLargePlan.Id }, o => o.ImmediateConsistency());
        var project = await _projectRepository.AddAsync(new Project { Name = "Test", OrganizationId = organization.Id, NextSummaryEndOfDayTicks = SystemClock.UtcNow.Ticks }, o => o.ImmediateConsistency());

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
            await _usageService.IncrementUsageAsync(organization, project, false);

        sw.Stop();
        _logger.LogInformation("Time: {Duration:g}, Avg: ({AverageTickDuration:g}ticks | {AverageDuration}ms)", sw.Elapsed, sw.ElapsedTicks / iterations, sw.ElapsedMilliseconds / iterations);
    }
}
