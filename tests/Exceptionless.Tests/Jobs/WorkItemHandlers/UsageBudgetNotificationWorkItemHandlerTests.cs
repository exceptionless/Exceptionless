using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Jobs.WorkItemHandlers;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services;
using Exceptionless.Tests.Mail;
using Exceptionless.Tests.Extensions;
using Exceptionless.Tests.Utility;
using Foundatio.Jobs;
using Foundatio.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Exceptionless.Tests.Jobs.WorkItemHandlers;

public class UsageBudgetNotificationWorkItemHandlerTests : IntegrationTestsBase
{
    private const string OrganizationId = "664ec4c1f12e4f2b7a0d3101";
    private const string ProjectId = "664ec4c1f12e4f2b7a0d3201";
    private const string UserId = "664ec4c1f12e4f2b7a0d3301";

    public UsageBudgetNotificationWorkItemHandlerTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory) { }

    private CountingMailer Mailer => GetService<CountingMailer>();
    private IOrganizationRepository OrganizationRepository => GetService<IOrganizationRepository>();
    private IProjectRepository ProjectRepository => GetService<IProjectRepository>();
    private IUserRepository UserRepository => GetService<IUserRepository>();
    private UsageService UsageService => GetService<UsageService>();

    protected override void RegisterServices(IServiceCollection services)
    {
        base.RegisterServices(services);
        services.AddSingleton<CountingMailer>();
        services.ReplaceSingleton<IMailer>(serviceProvider => serviceProvider.GetRequiredService<CountingMailer>());
    }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        Mailer.Reset();

        var billingManager = GetService<BillingManager>();
        var billingPlans = GetService<BillingPlans>();
        var organization = GetService<OrganizationData>().GenerateOrganization(billingManager, billingPlans, id: OrganizationId, name: "Budget Organization", plan: billingPlans.ExtraLargePlan);
        organization.MaxEventsPerMonth = 1_000_000;
        organization.BudgetAlertSettings = new OrganizationBudgetAlertSettings { Enabled = true, Thresholds = [50] };
        await OrganizationRepository.AddAsync(organization, options => options.ImmediateConsistency().Cache());

        var projectData = GetService<ProjectData>();
        await ProjectRepository.AddAsync([
            projectData.GenerateProject(id: ProjectId, organizationId: OrganizationId, name: "Noisy Project"),
            projectData.GenerateProject(generateId: true, organizationId: OrganizationId, name: "Quiet Project"),
            projectData.GenerateProject(generateId: true, organizationId: OrganizationId, name: "Another Project")
        ], options => options.ImmediateConsistency().Cache());

        var user = GetService<UserData>().GenerateUser(id: UserId, organizationId: OrganizationId, emailAddress: "budget-owner@example.org");
        user.IsEmailAddressVerified = true;
        user.EmailNotificationsEnabled = true;
        await UserRepository.AddAsync(user, options => options.ImmediateConsistency().Cache());
    }

    [Fact]
    public async Task OrganizationHandler_CurrentCrossing_SendsCurrentUsageAndPlan()
    {
        var organization = await OrganizationRepository.GetByIdAsync(OrganizationId);
        Assert.NotNull(organization);
        organization.GetCurrentUsage(TimeProvider).Total = 600_000;
        await OrganizationRepository.SaveAsync(organization, options => options.ImmediateConsistency().Cache());

        await HandleAsync(GetService<OrganizationBudgetAlertWorkItemHandler>(), new OrganizationBudgetAlertWorkItem
        {
            OrganizationId = OrganizationId,
            Threshold = 50,
            ThresholdEventCount = 1,
            CurrentEventCount = 1,
            EventLimit = 1
        });

        var call = Assert.Single(Mailer.OrganizationBudgetAlertCalls);
        Assert.Equal(500_000, call.ThresholdEventCount);
        Assert.Equal(600_000, call.CurrentEventCount);
        Assert.Equal(1_000_000, call.EventLimit);
    }

    [Fact]
    public async Task OrganizationHandler_DisabledAfterQueue_SuppressesStaleEmail()
    {
        var organization = await OrganizationRepository.GetByIdAsync(OrganizationId);
        Assert.NotNull(organization);
        organization.BudgetAlertSettings = new OrganizationBudgetAlertSettings { Enabled = false, Thresholds = [50] };
        await OrganizationRepository.SaveAsync(organization, options => options.ImmediateConsistency().Cache());

        await HandleAsync(GetService<OrganizationBudgetAlertWorkItemHandler>(), new OrganizationBudgetAlertWorkItem
        {
            OrganizationId = OrganizationId,
            Threshold = 50,
            ThresholdEventCount = 500_000,
            CurrentEventCount = 600_000,
            EventLimit = 1_000_000
        });

        Assert.Empty(Mailer.OrganizationBudgetAlertCalls);
    }

    [Fact]
    public async Task ProjectHandler_CurrentThrottle_SendsProjectUsageAndFairShare()
    {
        var organization = await OrganizationRepository.GetByIdAsync(OrganizationId);
        var project = await ProjectRepository.GetByIdAsync(ProjectId);
        Assert.NotNull(organization);
        Assert.NotNull(project);

        int spike = GetCurrentWindowSpike(organization.MaxEventsPerMonth);
        await UsageService.IncrementTotalAsync(organization, project.Id, spike);
        Assert.True((await UsageService.GetEventIngestAllowanceAsync(organization, project)).SmartThrottle.IsThrottled);

        await HandleAsync(GetService<ProjectSmartThrottleWorkItemHandler>(), new ProjectSmartThrottleWorkItem
        {
            OrganizationId = OrganizationId,
            ProjectId = ProjectId,
            SampleRate = 0.05,
            CurrentEventCount = 1,
            EventLimit = 1
        });

        var call = Assert.Single(Mailer.ProjectThrottleCalls);
        Assert.Equal(spike, call.CurrentEventCount);
        Assert.Equal(333_333, call.EventLimit);
        Assert.Equal(0.05, call.SampleRate);
    }

    [Fact]
    public async Task ProjectHandler_ThrottleExpiredBeforeHandling_SuppressesStaleEmail()
    {
        await HandleAsync(GetService<ProjectSmartThrottleWorkItemHandler>(), new ProjectSmartThrottleWorkItem
        {
            OrganizationId = OrganizationId,
            ProjectId = ProjectId,
            SampleRate = 0.05,
            CurrentEventCount = 1,
            EventLimit = 1
        });

        Assert.Empty(Mailer.ProjectThrottleCalls);
    }

    private int GetCurrentWindowSpike(int maxEventsPerMonth)
    {
        var utcNow = TimeProvider.GetUtcNow().UtcDateTime;
        var endOfMonth = new DateTime(utcNow.Year, utcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1);
        double windowsLeft = Math.Max(1, Math.Ceiling((endOfMonth - utcNow).TotalMinutes / 5));
        return (int)Math.Floor(maxEventsPerMonth / windowsLeft * 10 * 0.9);
    }

    private static async Task HandleAsync(WorkItemHandlerBase handler, object workItem)
    {
        await using var workItemLock = await handler.GetWorkItemLockAsync(workItem, TestContext.Current.CancellationToken);
        Assert.NotNull(workItemLock);
        var context = new WorkItemContext(workItem, "test-job", workItemLock, TestContext.Current.CancellationToken, static (_, _) => Task.CompletedTask);
        await handler.HandleItemAsync(context);
    }
}
