using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Migrations;
using Exceptionless.Core.Repositories;
using Exceptionless.DateTimeExtensions;
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
    private readonly OrganizationData _organizationData;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ProjectData _projectData;
    private readonly IProjectRepository _projectRepository;
    private readonly StackData _stackData;
    private readonly IStackRepository _stackRepository;
    private readonly EventData _eventData;
    private readonly IEventRepository _eventRepository;

    public UpdateEventUsageMigrationTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _organizationData = GetService<OrganizationData>();
        _organizationRepository = GetService<IOrganizationRepository>();
        _projectData = GetService<ProjectData>();
        _projectRepository = GetService<IProjectRepository>();
        _stackData = GetService<StackData>();
        _stackRepository = GetService<IStackRepository>();
        _eventData = GetService<EventData>();
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
        var organization = await _organizationRepository.AddAsync(_organizationData.GenerateSampleOrganizationWithPlan(GetService<BillingManager>(), billingPlans, billingPlans.MediumPlan), o => o.ImmediateConsistency());
        Assert.Single(organization.Usage);

        var project = await _projectRepository.AddAsync(_projectData.GenerateSampleProject(), o => o.ImmediateConsistency());
        Assert.Empty(project.Usage);

        var stack = await _stackRepository.AddAsync(_stackData.GenerateSampleStack(), o => o.ImmediateConsistency());

        var previousMonthUsageDate = DateTime.UtcNow.SubtractMonths(1).StartOfMonth();
        await _eventRepository.AddAsync(_eventData.GenerateEvents(count: 100, stackId: stack.Id, startDate: previousMonthUsageDate, endDate: previousMonthUsageDate.EndOfMonth()), o => o.ImmediateConsistency());

        var currentMonthUsageDate = DateTime.UtcNow.StartOfMonth();
        await _eventRepository.AddAsync(_eventData.GenerateEvents(count: 10, stackId: stack.Id, startDate: currentMonthUsageDate, endDate: DateTime.UtcNow), o => o.ImmediateConsistency());

        var migration = GetService<UpdateEventUsage>();
        var context = new MigrationContext(GetService<ILock>(), _logger, CancellationToken.None);
        await migration.RunAsync(context);

        int limit = organization.GetMaxEventsPerMonthWithBonus(TimeProvider);
        organization = await _organizationRepository.GetByIdAsync(organization.Id);
        Assert.Equal(2, organization.Usage.Count);
        var previousMonthsUsage = organization.GetUsage(previousMonthUsageDate, TimeProvider);
        Assert.Equal(100, previousMonthsUsage.Total);
        Assert.Equal(limit, previousMonthsUsage.Limit);
        var currentMonthsUsage = organization.GetUsage(currentMonthUsageDate, TimeProvider);
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
