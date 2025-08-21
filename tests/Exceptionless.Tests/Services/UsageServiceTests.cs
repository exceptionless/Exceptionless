using System.Diagnostics;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services;
using Exceptionless.Tests.Extensions;
using Foundatio.AsyncEx;
using Foundatio.Messaging;
using Foundatio.Repositories;
using Xunit;
using Xunit.Abstractions;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Exceptionless.Tests.Services;

public sealed class UsageServiceTests : IntegrationTestsBase
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly UsageService _usageService;
    private readonly BillingPlans _plans;

    public UsageServiceTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        TimeProvider.SetUtcNow(new DateTime(2015, 2, 13, 0, 0, 0, DateTimeKind.Utc));
        Log.SetLogLevel<OrganizationRepository>(LogLevel.Information);
        _usageService = GetService<UsageService>();
        _organizationRepository = GetService<IOrganizationRepository>();
        _projectRepository = GetService<IProjectRepository>();
        _plans = GetService<BillingPlans>();
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
        });

        var organization = await _organizationRepository.AddAsync(new Organization { Name = "Test", MaxEventsPerMonth = 750, PlanId = _plans.SmallPlan.Id }, o => o.ImmediateConsistency().Cache());
        var project = await _projectRepository.AddAsync(new Project { Name = "Test", OrganizationId = organization.Id, NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Ticks }, o => o.ImmediateConsistency().Cache());
        int eventsLeftInBucket = await _usageService.GetEventsLeftAsync(organization.Id);
        Assert.InRange(eventsLeftInBucket, 1, 750);
        Assert.Empty(organization.Usage);
        Assert.Empty(organization.UsageHours);

        int totalToIncrement = eventsLeftInBucket - 1;
        await _usageService.IncrementTotalAsync(organization.Id, project.Id, totalToIncrement);
        await countdown.WaitAsync(TimeSpan.FromMilliseconds(150));
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
        Assert.Single(organization.UsageHours);
        var usage = organization.Usage.Single();
        Assert.Equal(organization.MaxEventsPerMonth, usage.Limit);
        Assert.Equal(eventsLeftInBucket, usage.Total);
        Assert.Equal(0, usage.Blocked);
        Assert.Equal(0, usage.TooBig);

        project = await _projectRepository.GetByIdAsync(project.Id);
        Assert.Single(project.UsageHours);
        usage = project.Usage.Single();
        Assert.Equal(eventsLeftInBucket, usage.Total);
        Assert.Equal(0, usage.Blocked);
        Assert.Equal(0, usage.TooBig);
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
        });

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
        Assert.Single(organization.UsageHours);
        var usage = organization.Usage.Single();
        Assert.Equal(organization.MaxEventsPerMonth, usage.Limit);
        Assert.Equal(0, usage.Total);
        Assert.Equal(1, usage.Blocked);
        Assert.Equal(0, usage.TooBig);
        var overage = organization.UsageHours.Single();
        Assert.Equal(0, overage.Total);
        Assert.Equal(1, overage.Blocked);
        Assert.Equal(0, overage.TooBig);

        project = await _projectRepository.GetByIdAsync(project.Id);
        Assert.Single(project.UsageHours);

        usage = project.Usage.Single();
        Assert.Equal(0, usage.Total);
        Assert.Equal(1, usage.Blocked);
        Assert.Equal(0, usage.TooBig);

        overage = project.UsageHours.Single();
        Assert.Equal(0, overage.Total);
        Assert.Equal(1, overage.Blocked);
        Assert.Equal(0, overage.TooBig);
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
        Assert.Single(organization.UsageHours);
        var usage = organization.Usage.Single();
        Assert.Equal(organization.MaxEventsPerMonth, usage.Limit);
        Assert.Equal(0, usage.Total);
        Assert.Equal(1, usage.Discarded);
        Assert.Equal(0, usage.TooBig);
        var overage = organization.UsageHours.Single();
        Assert.Equal(0, overage.Total);
        Assert.Equal(1, overage.Discarded);
        Assert.Equal(0, overage.TooBig);

        project = await _projectRepository.GetByIdAsync(project.Id);
        Assert.Single(project.UsageHours);

        usage = project.Usage.Single();
        Assert.Equal(0, usage.Total);
        Assert.Equal(1, usage.Discarded);
        Assert.Equal(0, usage.TooBig);

        overage = project.UsageHours.Single();
        Assert.Equal(0, overage.Total);
        Assert.Equal(1, overage.Discarded);
        Assert.Equal(0, overage.TooBig);
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
        Assert.Single(organization.UsageHours);
        var usage = organization.Usage.Single();
        Assert.Equal(organization.MaxEventsPerMonth, usage.Limit);
        Assert.Equal(0, usage.Total);
        Assert.Equal(0, usage.Blocked);
        Assert.Equal(1, usage.TooBig);

        project = await _projectRepository.GetByIdAsync(project.Id);
        Assert.Single(project.UsageHours);
        usage = project.Usage.Single();
        Assert.Equal(0, usage.Total);
        Assert.Equal(0, usage.Blocked);
        Assert.Equal(1, usage.TooBig);
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
}
