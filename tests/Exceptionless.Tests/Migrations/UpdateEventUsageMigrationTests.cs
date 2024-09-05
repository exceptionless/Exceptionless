using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Migrations;
using Exceptionless.Core.Repositories;
using Exceptionless.Tests.Utility;
using Foundatio.Lock;
using Foundatio.Repositories;
using Foundatio.Repositories.Migrations;
using Foundatio.Utility;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Tests.Migrations;

public class UpdateEventUsageMigrationTests : IntegrationTestsBase
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IStackRepository _stackRepository;
    private readonly IEventRepository _eventRepository;

    public UpdateEventUsageMigrationTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _organizationRepository = GetService<IOrganizationRepository>();
        _projectRepository = GetService<IProjectRepository>();
        _stackRepository = GetService<IStackRepository>();
        _eventRepository = GetService<IEventRepository>();
    }

    protected override void RegisterServices(IServiceCollection services)
    {
        services.AddTransient<SetStackDuplicateSignature>();
        services.AddSingleton<ILock>(new EmptyLock());
        base.RegisterServices(services);
    }

    [Fact]
    public async Task ShouldPopulateUsageStats()
    {
        var billingPlans = GetService<BillingPlans>();
        var organization = await _organizationRepository.AddAsync(OrganizationData.GenerateSampleOrganizationWithPlan(GetService<BillingManager>(), billingPlans, billingPlans.MediumPlan), o => o.ImmediateConsistency());
        Assert.Single(organization.Usage);

        var project = await _projectRepository.AddAsync(ProjectData.GenerateSampleProject(), o => o.ImmediateConsistency());
        Assert.Empty(project.Usage);

        var stack = await _stackRepository.AddAsync(StackData.GenerateSampleStack(), o => o.ImmediateConsistency());

        var previousMonthUsageDate = _timeProvider.GetUtcNow().UtcDateTime.SubtractMonths(1).StartOfMonth();
        await _eventRepository.AddAsync(EventData.GenerateEvents(count: 100, stackId: stack.Id, startDate: previousMonthUsageDate, endDate: previousMonthUsageDate.EndOfMonth()), o => o.ImmediateConsistency());

        var currentMonthUsageDate = _timeProvider.GetUtcNow().UtcDateTime.StartOfMonth();
        await _eventRepository.AddAsync(EventData.GenerateEvents(count: 10, stackId: stack.Id, startDate: currentMonthUsageDate, endDate: _timeProvider.GetUtcNow().UtcDateTime), o => o.ImmediateConsistency());

        var migration = GetService<UpdateEventUsage>();
        var context = new MigrationContext(GetService<ILock>(), _logger, CancellationToken.None);
        await migration.RunAsync(context);

        int limit = organization.GetMaxEventsPerMonthWithBonus();
        organization = await _organizationRepository.GetByIdAsync(organization.Id);
        Assert.Equal(2, organization.Usage.Count);
        var previousMonthsUsage = organization.GetUsage(previousMonthUsageDate);
        Assert.Equal(100, previousMonthsUsage.Total);
        Assert.Equal(limit, previousMonthsUsage.Limit);
        var currentMonthsUsage = organization.GetUsage(currentMonthUsageDate);
        Assert.Equal(10, currentMonthsUsage.Total);
        Assert.Equal(limit, currentMonthsUsage.Limit);

        project = await _projectRepository.GetByIdAsync(project.Id);
        Assert.Equal(2, project.Usage.Count);
        previousMonthsUsage = project.GetUsage(previousMonthUsageDate);
        Assert.Equal(100, previousMonthsUsage.Total);
        Assert.Equal(limit, previousMonthsUsage.Limit);
        currentMonthsUsage = project.GetUsage(currentMonthUsageDate);
        Assert.Equal(10, currentMonthsUsage.Total);
        Assert.Equal(limit, currentMonthsUsage.Limit);
    }
}
