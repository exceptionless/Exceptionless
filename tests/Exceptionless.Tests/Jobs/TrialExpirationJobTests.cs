using Exceptionless.Core;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Configuration;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Tests.Mail;
using Exceptionless.Tests.Utility;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Exceptionless.Tests.Jobs;

public class TrialExpirationJobTests : IntegrationTestsBase
{
    private const string TrialOrganizationId = "664ec4c1f12e4f2b7a0d3001";
    private const string PaidOrganizationId = "664ec4c1f12e4f2b7a0d3002";
    private const string VerifiedUserId = "664ec4c1f12e4f2b7a0d4001";
    private const string UnverifiedUserId = "664ec4c1f12e4f2b7a0d4002";

    private readonly CountingMailer _mailer;
    private readonly TrialExpirationJob _job;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IUserRepository _userRepository;
    private readonly BillingManager _billingManager;
    private readonly BillingPlans _billingPlans;
    private readonly OrganizationData _organizationData;
    private readonly UserData _userData;
    private readonly BillingOptions _billingOptions;

    public TrialExpirationJobTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _mailer = GetService<CountingMailer>();
        _job = GetService<TrialExpirationJob>();
        _organizationRepository = GetService<IOrganizationRepository>();
        _userRepository = GetService<IUserRepository>();
        _billingManager = GetService<BillingManager>();
        _billingPlans = GetService<BillingPlans>();
        _organizationData = GetService<OrganizationData>();
        _userData = GetService<UserData>();
        var options = GetService<AppOptions>();
        _billingOptions = options.BillingOptions;
    }

    private TimeSpan TimeUntilReminder => TimeSpan.FromDays(_billingOptions.TrialDays - _billingOptions.TrialReminderDays + 0.5);

    protected override void RegisterServices(IServiceCollection services)
    {
        base.RegisterServices(services);
        services.AddSingleton<CountingMailer>();
        services.ReplaceSingleton<IMailer>(sp => sp.GetRequiredService<CountingMailer>());
    }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        _mailer.Reset();

        var trialOrganization = new Organization { Id = TrialOrganizationId, Name = "Trial Organization" };
        _billingManager.ApplyTrialPlan(trialOrganization);
        var paidOrganization = _organizationData.GenerateOrganization(_billingManager, _billingPlans, id: PaidOrganizationId, name: "Paid Organization", plan: _billingPlans.MediumPlan);
        await _organizationRepository.AddAsync([trialOrganization, paidOrganization], options => options.ImmediateConsistency());

        var verifiedUser = _userData.GenerateUser(id: VerifiedUserId, organizationId: TrialOrganizationId, emailAddress: "trial-owner@example.org");
        verifiedUser.MarkEmailAddressVerified();
        var unverifiedUser = _userData.GenerateUser(id: UnverifiedUserId, organizationId: TrialOrganizationId, emailAddress: "trial-member@example.org");
        unverifiedUser.ResetVerifyEmailAddressTokenAndExpiration(TimeProvider);
        await _userRepository.AddAsync([verifiedUser, unverifiedUser], options => options.ImmediateConsistency());
    }

    [Fact]
    public async Task RunAsync_TrialLifecycle_SendsOneReminderThenMovesToFreePlan()
    {
        // Arrange
        TimeProvider.Advance(TimeUntilReminder);

        // Act
        await _job.RunAsync(TestCancellationToken);
        await _job.RunAsync(TestCancellationToken);

        var remindedOrganization = await _organizationRepository.GetByIdAsync(TrialOrganizationId);
        var reminder = _mailer.OrganizationTrialEndingCalls.ToArray();
        TimeProvider.Advance(TimeSpan.FromDays(_billingOptions.TrialReminderDays));
        await _job.RunAsync(TestCancellationToken);

        // Assert
        Assert.NotNull(remindedOrganization);
        Assert.Equal(_billingPlans.TrialPlan.Id, remindedOrganization.PlanId);
        Assert.NotNull(remindedOrganization.TrialReminderSentUtc);
        Assert.Equal(new OrganizationTrialEndingCall(VerifiedUserId, TrialOrganizationId), Assert.Single(reminder));
        var expiredOrganization = await _organizationRepository.GetByIdAsync(TrialOrganizationId);
        Assert.NotNull(expiredOrganization);
        Assert.Equal(_billingPlans.FreePlan.Id, expiredOrganization.PlanId);
        Assert.False(expiredOrganization.HasPremiumFeatures);
        Assert.Equal(_billingPlans.FreePlan.RetentionDays, expiredOrganization.RetentionDays);
        Assert.NotNull(expiredOrganization.TrialEndsUtc);
        Assert.Single(_mailer.OrganizationTrialEndingCalls);

        var paidOrganization = await _organizationRepository.GetByIdAsync(PaidOrganizationId);
        Assert.NotNull(paidOrganization);
        Assert.Equal(_billingPlans.MediumPlan.Id, paidOrganization.PlanId);
    }

    [Fact]
    public async Task RunAsync_TrialWithMoreMembersThanOnePage_RemindsEveryVerifiedMember()
    {
        // Arrange
        var members = Enumerable.Range(0, 101).Select(index =>
        {
            var member = _userData.GenerateUser(generateId: true, organizationId: TrialOrganizationId, emailAddress: $"trial-member-{index}@example.org");
            member.MarkEmailAddressVerified();
            return member;
        }).ToList();
        await _userRepository.AddAsync(members, options => options.ImmediateConsistency());
        TimeProvider.Advance(TimeUntilReminder);

        // Act
        await _job.RunAsync(TestCancellationToken);

        // Assert
        Assert.Equal(members.Count + 1, _mailer.OrganizationTrialEndingCalls.Count);
        Assert.All(members, member => Assert.Contains(_mailer.OrganizationTrialEndingCalls, call => String.Equals(call.UserId, member.Id, StringComparison.Ordinal)));
    }

    [Fact]
    public async Task RunAsync_CancelledMidPage_StopsBeforeTheNextOrganization()
    {
        // Arrange
        const string secondTrialOrganizationId = "664ec4c1f12e4f2b7a0d3003";
        var secondTrialOrganization = new Organization { Id = secondTrialOrganizationId, Name = "Second Trial Organization" };
        _billingManager.ApplyTrialPlan(secondTrialOrganization);
        await _organizationRepository.AddAsync(secondTrialOrganization, options => options.ImmediateConsistency());
        var secondOwner = _userData.GenerateUser(generateId: true, organizationId: secondTrialOrganizationId, emailAddress: "second-trial-owner@example.org");
        secondOwner.MarkEmailAddressVerified();
        await _userRepository.AddAsync(secondOwner, options => options.ImmediateConsistency());
        TimeProvider.Advance(TimeUntilReminder);

        using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(TestCancellationToken);
        _mailer.OnOrganizationTrialEnding = cancellationTokenSource.Cancel;

        // Act
        await _job.RunAsync(cancellationTokenSource.Token);

        // Assert
        var reminder = Assert.Single(_mailer.OrganizationTrialEndingCalls);
        Assert.Equal(new OrganizationTrialEndingCall(VerifiedUserId, TrialOrganizationId), reminder);
        var skippedOrganization = await _organizationRepository.GetByIdAsync(secondTrialOrganizationId);
        Assert.NotNull(skippedOrganization);
        Assert.Null(skippedOrganization.TrialReminderSentUtc);
    }

    [Fact]
    public async Task RunAsync_TrialWithoutEndDate_MovesToFreePlan()
    {
        // Arrange
        var organization = await _organizationRepository.GetByIdAsync(TrialOrganizationId);
        Assert.NotNull(organization);
        organization.TrialEndsUtc = null;
        await _organizationRepository.SaveAsync(organization, options => options.ImmediateConsistency());

        // Act
        await _job.RunAsync(TestCancellationToken);

        // Assert
        var expiredOrganization = await _organizationRepository.GetByIdAsync(TrialOrganizationId);
        Assert.NotNull(expiredOrganization);
        Assert.Equal(_billingPlans.FreePlan.Id, expiredOrganization.PlanId);
        Assert.Empty(_mailer.OrganizationTrialEndingCalls);
    }

    [Fact]
    public async Task RunAsync_StaleExpiredTrialInCache_PreservesExtendedTrial()
    {
        // Arrange
        TimeProvider.Advance(TimeSpan.FromDays(_billingOptions.TrialDays + 1));
        await _organizationRepository.GetByIdAsync(TrialOrganizationId, options => options.Cache());
        var cache = GetService<ICacheClient>();
        string cacheKey = $"Organization:{TrialOrganizationId}";
        var cached = await cache.GetAsync<ICollection<FindHit<Organization>>>(cacheKey);
        Assert.True(cached.HasValue);

        var extended = await _organizationRepository.GetByIdAsync(TrialOrganizationId, options => options.Cache(false));
        Assert.NotNull(extended);
        _billingManager.ApplyTrialPlan(extended);
        await _organizationRepository.SaveAsync(extended, options => options.ImmediateConsistency().Cache());

        // Model an older cache fill arriving after the extension was saved.
        await cache.SetAsync(cacheKey, cached.Value);

        // Act
        await _job.RunAsync(TestCancellationToken);

        // Assert
        var actual = await _organizationRepository.GetByIdAsync(TrialOrganizationId, options => options.Cache(false));
        Assert.NotNull(actual);
        Assert.Equal(_billingPlans.TrialPlan.Id, actual.PlanId);
        Assert.Equal(extended.TrialEndsUtc, actual.TrialEndsUtc);
        Assert.Empty(_mailer.OrganizationTrialEndingCalls);
    }
}
