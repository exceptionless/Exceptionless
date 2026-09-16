using System.Reflection;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Jobs.WorkItemHandlers;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Tests.Mail;
using Foundatio.Jobs;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Exceptionless.Tests.Jobs.WorkItemHandlers;

public sealed class UsageBudgetNotificationTests : TestWithServices
{
    private const string OrganizationId = "664ec4c1f12e4f2b7a0d3101";
    private const string ProjectId = "664ec4c1f12e4f2b7a0d3201";
    private const string UserId = "664ec4c1f12e4f2b7a0d3301";

    public UsageBudgetNotificationTests(ITestOutputHelper output) : base(output) { }

    private OrganizationRepositoryProxy OrganizationRepository => (OrganizationRepositoryProxy)(object)GetService<IOrganizationRepository>();
    private ProjectRepositoryProxy ProjectRepository => (ProjectRepositoryProxy)(object)GetService<IProjectRepository>();
    private UserRepositoryProxy UserRepository => (UserRepositoryProxy)(object)GetService<IUserRepository>();
    private CountingMailer Mailer => GetService<CountingMailer>();
    private UsageService UsageService => GetService<UsageService>();

    protected override void RegisterServices(IServiceCollection services, AppOptions options)
    {
        base.RegisterServices(services, options);

        var organizationRepository = DispatchProxy.Create<IOrganizationRepository, OrganizationRepositoryProxy>();
        var projectRepository = DispatchProxy.Create<IProjectRepository, ProjectRepositoryProxy>();
        var userRepository = DispatchProxy.Create<IUserRepository, UserRepositoryProxy>();
        services.Replace(ServiceDescriptor.Singleton<IOrganizationRepository>(organizationRepository));
        services.Replace(ServiceDescriptor.Singleton<IProjectRepository>(projectRepository));
        services.Replace(ServiceDescriptor.Singleton<IUserRepository>(userRepository));

        services.AddSingleton<CountingMailer>();
        services.Replace(ServiceDescriptor.Singleton<IMailer>(serviceProvider => serviceProvider.GetRequiredService<CountingMailer>()));
    }

    [Theory]
    [InlineData(1_000_000, 1)]
    [InlineData(100_000_000, 0)]
    public async Task ProjectHandler_PlanChangedBeforeHandling_UsesLiveThrottleState(int maxEventsPerMonth, int expectedEmailCount)
    {
        TimeProvider.SetUtcNow(new DateTimeOffset(2026, 8, 2, 12, 0, 0, TimeSpan.Zero));

        var organization = new Organization
        {
            Id = OrganizationId,
            Name = "Budget Organization",
            PlanId = "EX_XL",
            MaxEventsPerMonth = 1_000_000
        };
        var project = new Project
        {
            Id = ProjectId,
            OrganizationId = OrganizationId,
            Name = "Noisy Project"
        };
        var user = new User
        {
            Id = UserId,
            FullName = "Budget Owner",
            EmailAddress = "budget-owner@example.org",
            IsEmailAddressVerified = true,
            EmailNotificationsEnabled = true
        };

        OrganizationRepository.Organization = organization;
        ProjectRepository.Project = project;
        ProjectRepository.ProjectCount = 3;
        UserRepository.Users = new FindResults<User>(
            [new FindHit<User>(UserId, user, 0, null, null, null)],
            1,
            null,
            null,
            null);

        int spike = GetCurrentWindowSpike(organization.MaxEventsPerMonth);
        await UsageService.IncrementTotalAsync(organization, project.Id, spike);
        var allowance = await UsageService.GetEventIngestAllowanceAsync(organization, project);
        Assert.True(allowance.SmartThrottle.IsThrottled);
        Assert.True(await UsageService.IsProjectSmartThrottledAsync(OrganizationId, ProjectId));

        organization.MaxEventsPerMonth = maxEventsPerMonth;
        Assert.Equal(expectedEmailCount == 1, (await UsageService.GetSmartThrottleRateAsync(OrganizationId, ProjectId)).IsThrottled);

        await HandleAsync(GetService<ProjectSmartThrottleWorkItemHandler>(), new ProjectSmartThrottleWorkItem
        {
            OrganizationId = OrganizationId,
            ProjectId = ProjectId,
            SampleRate = 0.99,
            CurrentEventCount = 1,
            EventLimit = 2,
            UsagePeriod = TimeProvider.GetUtcNow().UtcDateTime.StartOfMonth().ToEpoch()
        });

        Assert.Equal(expectedEmailCount, Mailer.ProjectThrottleCalls.Count);
        if (expectedEmailCount == 1)
        {
            var call = Assert.Single(Mailer.ProjectThrottleCalls);
            Assert.Equal(allowance.SmartThrottle.SampleRate, call.SampleRate);
            Assert.Equal(allowance.SmartThrottle.CurrentProjectUsage, call.CurrentEventCount);
            Assert.Equal(allowance.SmartThrottle.FairShareLimit, call.EventLimit);
        }
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

    private class OrganizationRepositoryProxy : DispatchProxy
    {
        public Organization? Organization { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "GetByIdAsync")
                return Task.FromResult(Organization);

            throw new NotSupportedException(targetMethod?.Name);
        }
    }

    private class ProjectRepositoryProxy : DispatchProxy
    {
        public Project? Project { get; set; }
        public int ProjectCount { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return targetMethod?.Name switch
            {
                "GetByIdAsync" => Task.FromResult(Project),
                "GetCountByOrganizationIdAsync" => Task.FromResult(new CountResult(ProjectCount, null, null)),
                _ => throw new NotSupportedException(targetMethod?.Name)
            };
        }
    }

    private class UserRepositoryProxy : DispatchProxy
    {
        public FindResults<User> Users { get; set; } = new();

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "GetByOrganizationIdAsync")
                return Task.FromResult(Users);

            throw new NotSupportedException(targetMethod?.Name);
        }
    }
}
