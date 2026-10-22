using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Tests.Mail;
using Exceptionless.Tests.Utility;
using Foundatio.Jobs;
using Foundatio.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Exceptionless.Tests.Jobs;

public class TrialExpirationJobTests : IntegrationTestsBase
{
    private const string TrialOrganizationId = "664ec4c1f12e4f2b7a0d3001";
    private const string PaidOrganizationId = "664ec4c1f12e4f2b7a0d3002";
    private const string VerifiedUserId = "664ec4c1f12e4f2b7a0d4001";
    private const string UnverifiedUserId = "664ec4c1f12e4f2b7a0d4002";

    public TrialExpirationJobTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory) { }

    private CountingMailer Mailer => GetService<CountingMailer>();
    private TrialExpirationJob Job => GetService<TrialExpirationJob>();
    private IOrganizationRepository OrganizationRepository => GetService<IOrganizationRepository>();
    private IUserRepository UserRepository => GetService<IUserRepository>();
    private BillingManager BillingManager => GetService<BillingManager>();
    private BillingPlans BillingPlans => GetService<BillingPlans>();
    private OrganizationData OrganizationData => GetService<OrganizationData>();
    private UserData UserData => GetService<UserData>();

    protected override void RegisterServices(IServiceCollection services)
    {
        base.RegisterServices(services);
        services.AddSingleton<CountingMailer>();
        services.ReplaceSingleton<IMailer>(sp => sp.GetRequiredService<CountingMailer>());
    }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        Mailer.Reset();

        var trialOrganization = new Organization { Id = TrialOrganizationId, Name = "Trial Organization" };
        BillingManager.ApplyTrialPlan(trialOrganization);
        var paidOrganization = OrganizationData.GenerateOrganization(BillingManager, BillingPlans, id: PaidOrganizationId, name: "Paid Organization", plan: BillingPlans.MediumPlan);
        await OrganizationRepository.AddAsync([trialOrganization, paidOrganization], options => options.ImmediateConsistency());

        var verifiedUser = UserData.GenerateUser(id: VerifiedUserId, organizationId: TrialOrganizationId, emailAddress: "trial-owner@example.org");
        verifiedUser.MarkEmailAddressVerified();
        var unverifiedUser = UserData.GenerateUser(id: UnverifiedUserId, organizationId: TrialOrganizationId, emailAddress: "trial-member@example.org");
        unverifiedUser.ResetVerifyEmailAddressTokenAndExpiration(TimeProvider);
        await UserRepository.AddAsync([verifiedUser, unverifiedUser], options => options.ImmediateConsistency());
    }

    [Fact]
    public async Task RunAsync_TrialLifecycle_SendsOneReminderThenMovesToFreePlan()
    {
        // Arrange
        TimeProvider.Advance(TimeSpan.FromDays(5.5));

        // Act
        await Job.RunAsync(TestCancellationToken);
        await Job.RunAsync(TestCancellationToken);

        // Assert
        var remindedOrganization = await OrganizationRepository.GetByIdAsync(TrialOrganizationId);
        Assert.NotNull(remindedOrganization);
        Assert.Equal(BillingPlans.TrialPlan.Id, remindedOrganization.PlanId);
        Assert.NotNull(remindedOrganization.TrialReminderSentUtc);

        var reminder = Assert.Single(Mailer.OrganizationTrialEndingCalls);
        Assert.Equal(new OrganizationTrialEndingCall(VerifiedUserId, TrialOrganizationId), reminder);

        // Arrange
        TimeProvider.Advance(TimeSpan.FromDays(2));

        // Act
        await Job.RunAsync(TestCancellationToken);

        // Assert
        var expiredOrganization = await OrganizationRepository.GetByIdAsync(TrialOrganizationId);
        Assert.NotNull(expiredOrganization);
        Assert.Equal(BillingPlans.FreePlan.Id, expiredOrganization.PlanId);
        Assert.False(expiredOrganization.HasPremiumFeatures);
        Assert.Equal(BillingPlans.FreePlan.RetentionDays, expiredOrganization.RetentionDays);
        Assert.NotNull(expiredOrganization.TrialEndsUtc);
        Assert.Single(Mailer.OrganizationTrialEndingCalls);

        var paidOrganization = await OrganizationRepository.GetByIdAsync(PaidOrganizationId);
        Assert.NotNull(paidOrganization);
        Assert.Equal(BillingPlans.MediumPlan.Id, paidOrganization.PlanId);
    }

    [Fact]
    public async Task RunAsync_TrialWithoutEndDate_MovesToFreePlan()
    {
        // Arrange
        var organization = await OrganizationRepository.GetByIdAsync(TrialOrganizationId);
        Assert.NotNull(organization);
        organization.TrialEndsUtc = null;
        await OrganizationRepository.SaveAsync(organization, options => options.ImmediateConsistency());

        // Act
        await Job.RunAsync(TestCancellationToken);

        // Assert
        var expiredOrganization = await OrganizationRepository.GetByIdAsync(TrialOrganizationId);
        Assert.NotNull(expiredOrganization);
        Assert.Equal(BillingPlans.FreePlan.Id, expiredOrganization.PlanId);
        Assert.Empty(Mailer.OrganizationTrialEndingCalls);
    }
}
