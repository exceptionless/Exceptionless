using Exceptionless.Core;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Jobs.WorkItemHandlers;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Repositories;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Tests.Mail;
using Exceptionless.Tests.Utility;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Exceptionless.Tests.Jobs.WorkItemHandlers;

public class OrganizationNotificationWorkItemHandlerIntegrationTests : IntegrationTestsBase
{
    private const string PrimaryOrganizationId = "664ec4c1f12e4f2b7a0d1001";
    private const string SecondaryOrganizationId = "664ec4c1f12e4f2b7a0d1002";
    private const string MissingOrganizationId = "664ec4c1f12e4f2b7a0d1003";
    private const string PrimaryUserId = "664ec4c1f12e4f2b7a0d2001";
    private const string SecondaryUserId = "664ec4c1f12e4f2b7a0d2002";
    private const string UnverifiedUserId = "664ec4c1f12e4f2b7a0d2003";
    private const string DisabledNotificationsUserId = "664ec4c1f12e4f2b7a0d2004";

    public OrganizationNotificationWorkItemHandlerIntegrationTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory) { }

    private CountingMailer Mailer => GetService<CountingMailer>();
    private ICacheClient CacheClient => GetService<ICacheClient>();
    private OrganizationNotificationWorkItemHandler Handler => GetService<OrganizationNotificationWorkItemHandler>();
    private IOrganizationRepository OrganizationRepository => GetService<IOrganizationRepository>();
    private IUserRepository UserRepository => GetService<IUserRepository>();
    private BillingManager BillingManager => GetService<BillingManager>();
    private BillingPlans BillingPlans => GetService<BillingPlans>();
    private OrganizationData OrganizationData => GetService<OrganizationData>();
    private UserData UserData => GetService<UserData>();

    private Task SetMonthlySentMarkerAsync(string organizationId)
    {
        return CacheClient.SetAsync(
            OrganizationNotificationWorkItemHandler.GetNotificationSentKey(organizationId, isOverMonthlyLimit: true),
            true,
            TimeProvider.GetUtcNow().UtcDateTime.EndOfMonth());
    }

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

        var organizations = new[]
        {
            OrganizationData.GenerateOrganization(BillingManager, BillingPlans, id: PrimaryOrganizationId, name: "Primary Organization", plan: BillingPlans.SmallPlan),
            OrganizationData.GenerateOrganization(BillingManager, BillingPlans, id: SecondaryOrganizationId, name: "Secondary Organization", plan: BillingPlans.SmallPlan)
        };

        foreach (var organization in organizations)
            organization.GetCurrentUsage(TimeProvider).Total = organization.GetMaxEventsPerMonthWithBonus(TimeProvider);

        await OrganizationRepository.AddAsync(organizations, options => options.ImmediateConsistency());

        var primaryUser = UserData.GenerateUser(id: PrimaryUserId, organizationId: PrimaryOrganizationId, emailAddress: "primary-owner@example.org");
        primaryUser.FullName = "Primary Owner";
        primaryUser.IsEmailAddressVerified = true;

        var secondaryUser = UserData.GenerateUser(id: SecondaryUserId, organizationId: SecondaryOrganizationId, emailAddress: "secondary-owner@example.org");
        secondaryUser.FullName = "Secondary Owner";
        secondaryUser.IsEmailAddressVerified = true;

        await UserRepository.AddAsync([primaryUser, secondaryUser], options => options.ImmediateConsistency());
    }

    [Fact]
    public async Task HandleItemAsync_WhenDuplicateMonthlyNotificationsAreProcessedAcrossHours_ShouldSendOneEmail()
    {
        // Arrange
        // Act
        await HandleWorkItemAsync(CreateMonthlyNotificationWorkItem(PrimaryOrganizationId));

        TimeProvider.Advance(TimeSpan.FromHours(1).Add(TimeSpan.FromMinutes(1)));
        await HandleWorkItemAsync(CreateMonthlyNotificationWorkItem(PrimaryOrganizationId));

        TimeProvider.Advance(TimeSpan.FromHours(1).Add(TimeSpan.FromMinutes(1)));
        await HandleWorkItemAsync(CreateMonthlyNotificationWorkItem(PrimaryOrganizationId));

        // Assert
        Assert.Equal(1, Mailer.OrganizationNoticeCount);
    }

    [Fact]
    public async Task HandleItemAsync_WhenMonthlyNotificationsTargetDifferentOrganizations_ShouldSendOneEmailPerOrganization()
    {
        // Arrange
        // Act
        for (int i = 0; i < 3; i++)
        {
            await HandleWorkItemAsync(CreateMonthlyNotificationWorkItem(PrimaryOrganizationId));
            await HandleWorkItemAsync(CreateMonthlyNotificationWorkItem(SecondaryOrganizationId));
        }

        // Assert
        var callsByOrganization = Mailer.OrganizationNoticeCalls
            .GroupBy(call => call.OrganizationId)
            .ToDictionary(group => group.Key, group => group.Count());

        Assert.Equal(2, callsByOrganization.Count);
        Assert.Equal(1, callsByOrganization[PrimaryOrganizationId]);
        Assert.Equal(1, callsByOrganization[SecondaryOrganizationId]);
    }

    [Fact]
    public async Task HandleItemAsync_WhenTwentyFourHoursPass_ShouldNotSendAnotherMonthlyNotification()
    {
        // Arrange
        TimeProvider.SetUtcNow(new DateTime(2026, 5, 15, 12, 0, 0, DateTimeKind.Utc));
        await SetOrganizationOverMonthlyLimitAsync(PrimaryOrganizationId);

        // Act
        await HandleWorkItemAsync(CreateMonthlyNotificationWorkItem(PrimaryOrganizationId));

        TimeProvider.Advance(TimeSpan.FromHours(25));
        await HandleWorkItemAsync(CreateMonthlyNotificationWorkItem(PrimaryOrganizationId));

        // Assert
        Assert.Equal(1, Mailer.OrganizationNoticeCount);
    }

    [Fact]
    public async Task HandleItemAsync_WhenUtcMonthEndsAndOrganizationIsStillOverMonthlyLimit_ShouldAllowAnotherMonthlyNotification()
    {
        // Arrange
        TimeProvider.SetUtcNow(new DateTime(2026, 5, 31, 23, 50, 0, DateTimeKind.Utc));
        await SetOrganizationOverMonthlyLimitAsync(PrimaryOrganizationId);

        // Act
        await HandleWorkItemAsync(CreateMonthlyNotificationWorkItem(PrimaryOrganizationId));

        TimeProvider.Advance(TimeSpan.FromMinutes(20));
        await SetOrganizationOverMonthlyLimitAsync(PrimaryOrganizationId);
        await HandleWorkItemAsync(CreateMonthlyNotificationWorkItem(PrimaryOrganizationId));

        // Assert
        Assert.Equal(2, Mailer.OrganizationNoticeCount);
    }

    /// <summary>
    /// Regression test: returning null from GetWorkItemLockAsync causes WorkItemJob to call
    /// AbandonAsync instead of HandleItemAsync, creating an infinite retry loop for hourly items.
    /// The fix returns EmptyLock (via base) so the item is completed normally.
    /// </summary>
    [Fact]
    public async Task GetWorkItemLockAsync_WhenWorkItemIsHourly_ShouldReturnNonNullLockSoItemIsNotAbandoned()
    {
        // Arrange
        var workItem = CreateHourlyNotificationWorkItem(PrimaryOrganizationId);

        // Act
        // null → WorkItemJob calls AbandonAsync (infinite retry loop)
        // non-null → WorkItemJob calls HandleItemAsync and completes the entry
        await using var workItemLock = await Handler.GetWorkItemLockAsync(workItem, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(workItemLock);
    }

    [Fact]
    public async Task GetWorkItemLockAsync_WhenMonthlyLockIsAlreadyHeld_ShouldReturnNullSoConcurrentItemIsAbandoned()
    {
        // Arrange
        var workItem = CreateMonthlyNotificationWorkItem(PrimaryOrganizationId);

        // Act
        await using var firstLock = await Handler.GetWorkItemLockAsync(workItem, TestContext.Current.CancellationToken);
        await using var secondLock = await Handler.GetWorkItemLockAsync(workItem, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(firstLock);   // First worker acquires the lock
        Assert.Null(secondLock);     // Concurrent duplicate is correctly abandoned
    }

    /// <summary>
    /// Regression test for the <see cref="HandleWorkItemAsync"/> helper bug:
    /// the helper must NOT call <see cref="OrganizationNotificationWorkItemHandler.HandleItemAsync"/>
    /// when the lock is null, because in production WorkItemJob calls <c>AbandonAsync</c> instead.
    /// Without this guard a concurrent item could bypass the lock, find no sent marker, and send
    /// a duplicate email — a bug that would be invisible to all other integration tests.
    /// </summary>
    [Fact]
    public async Task HandleItemAsync_WhenConcurrentWorkerHoldsMonthlyLock_ShouldNotSendEmail()
    {
        // Arrange: a concurrent worker already holds the monthly notification lock
        var workItem = CreateMonthlyNotificationWorkItem(PrimaryOrganizationId);
        await using var concurrentWorkerLock = await Handler.GetWorkItemLockAsync(workItem, TestContext.Current.CancellationToken);
        Assert.NotNull(concurrentWorkerLock);

        // Act: a second worker attempts to process the same item while the lock is held;
        // GetWorkItemLockAsync returns null so WorkItemJob abandons the item (never calls HandleItemAsync)
        await HandleWorkItemAsync(workItem);

        // Assert: no email was sent because the item was abandoned, not processed
        Assert.Equal(0, Mailer.OrganizationNoticeCount);
    }

    [Fact]
    public async Task HandleItemAsync_WhenOnlyAnHourlyOverageIsProcessed_ShouldNotSendEmail()
    {
        // Arrange
        // Act
        await HandleWorkItemAsync(CreateHourlyNotificationWorkItem(PrimaryOrganizationId));

        // Assert
        Assert.Equal(0, Mailer.OrganizationNoticeCount);
    }

    [Fact]
    public async Task HandleItemAsync_WhenTheMonthlySentMarkerAlreadyExists_ShouldNotSendEmail()
    {
        // Arrange
        await SetMonthlySentMarkerAsync(PrimaryOrganizationId);

        // Act
        await HandleWorkItemAsync(CreateMonthlyNotificationWorkItem(PrimaryOrganizationId));

        // Assert
        Assert.Equal(0, Mailer.OrganizationNoticeCount);
    }

    [Fact]
    public async Task HandleItemAsync_WhenMonthlyEmailIsSent_ShouldWriteSentMarker()
    {
        // Arrange
        var workItem = CreateMonthlyNotificationWorkItem(PrimaryOrganizationId);

        // Act
        await HandleWorkItemAsync(workItem);

        // Assert
        var sentMarkerExists = await CacheClient.ExistsAsync(
            OrganizationNotificationWorkItemHandler.GetNotificationSentKey(PrimaryOrganizationId, isOverMonthlyLimit: true));
        Assert.True(sentMarkerExists);
    }

    [Fact]
    public async Task HandleItemAsync_WhenMonthlyWorkItemIsStaleAndOrganizationIsNoLongerOverMonthlyLimit_ShouldNotSendEmailOrWriteSentMarker()
    {
        // Arrange
        var organization = await OrganizationRepository.GetByIdAsync(PrimaryOrganizationId, options => options.Cache());
        Assert.NotNull(organization);

        organization.GetCurrentUsage(TimeProvider).Total = 0;
        await OrganizationRepository.SaveAsync(organization, options => options.ImmediateConsistency().Cache());

        // Act
        await HandleWorkItemAsync(CreateMonthlyNotificationWorkItem(PrimaryOrganizationId));

        // Assert
        var sentMarkerExists = await CacheClient.ExistsAsync(
            OrganizationNotificationWorkItemHandler.GetNotificationSentKey(PrimaryOrganizationId, isOverMonthlyLimit: true));
        Assert.False(sentMarkerExists);
        Assert.Equal(0, Mailer.OrganizationNoticeCount);
    }

    [Fact]
    public async Task HandleItemAsync_WhenOrganizationNoLongerExists_ShouldNotWriteSentMarkerOrSendEmail()
    {
        // Arrange
        var workItem = CreateMonthlyNotificationWorkItem(MissingOrganizationId);

        // Act
        await HandleWorkItemAsync(workItem);

        // Assert
        var sentMarkerExists = await CacheClient.ExistsAsync(
            OrganizationNotificationWorkItemHandler.GetNotificationSentKey(MissingOrganizationId, isOverMonthlyLimit: true));
        Assert.False(sentMarkerExists);
        Assert.Equal(0, Mailer.OrganizationNoticeCount);
    }

    [Fact]
    public async Task HandleItemAsync_WhenUsersAreUnverifiedOrDisabled_ShouldOnlySendToEligibleUsers()
    {
        // Arrange
        var unverifiedUser = UserData.GenerateUser(id: UnverifiedUserId, organizationId: PrimaryOrganizationId, emailAddress: "unverified-owner@example.org");
        unverifiedUser.FullName = "Unverified Owner";
        unverifiedUser.IsEmailAddressVerified = false;
        unverifiedUser.EmailNotificationsEnabled = true;
        unverifiedUser.ResetVerifyEmailAddressTokenAndExpiration(TimeProvider);

        var disabledNotificationsUser = UserData.GenerateUser(id: DisabledNotificationsUserId, organizationId: PrimaryOrganizationId, emailAddress: "disabled-owner@example.org");
        disabledNotificationsUser.FullName = "Disabled Owner";
        disabledNotificationsUser.IsEmailAddressVerified = true;
        disabledNotificationsUser.EmailNotificationsEnabled = false;

        await UserRepository.AddAsync([unverifiedUser, disabledNotificationsUser], options => options.ImmediateConsistency());

        // Act
        await HandleWorkItemAsync(CreateMonthlyNotificationWorkItem(PrimaryOrganizationId));

        // Assert
        var call = Assert.Single(Mailer.OrganizationNoticeCalls);
        Assert.Equal(PrimaryUserId, call.UserId);
        Assert.Equal(PrimaryOrganizationId, call.OrganizationId);
        Assert.True(call.IsOverMonthlyLimit);
        Assert.False(call.IsOverHourlyLimit);
    }

    [Fact]
    public async Task HandleItemAsync_WhenHourlyOveragePrecedesMonthlyOverage_ShouldSendTheMonthlyEmail()
    {
        // Arrange
        // Act
        await HandleWorkItemAsync(CreateHourlyNotificationWorkItem(PrimaryOrganizationId));
        await HandleWorkItemAsync(CreateMonthlyNotificationWorkItem(PrimaryOrganizationId));

        // Assert
        Assert.Equal(1, Mailer.OrganizationNoticeCount);
    }

    [Fact]
    public async Task HandleItemAsync_WhenHourlyOverageArrivesAfterMonthlyEmail_ShouldNotSendAnotherEmail()
    {
        // Arrange
        // Act
        await HandleWorkItemAsync(CreateMonthlyNotificationWorkItem(PrimaryOrganizationId));

        TimeProvider.Advance(TimeSpan.FromHours(1).Add(TimeSpan.FromMinutes(1)));
        await HandleWorkItemAsync(CreateHourlyNotificationWorkItem(PrimaryOrganizationId));

        // Assert
        var call = Assert.Single(Mailer.OrganizationNoticeCalls);
        Assert.True(call.IsOverMonthlyLimit);
        Assert.False(call.IsOverHourlyLimit);
    }

    [Fact]
    public async Task GetWorkItemLockAsync_WhenMonthlyWorkerReachesWorkItemTimeout_ShouldKeepConcurrentItemAbandoned()
    {
        // Arrange
        var workItem = CreateMonthlyNotificationWorkItem(PrimaryOrganizationId);
        await using var workerALock = await Handler.GetWorkItemLockAsync(workItem, TestContext.Current.CancellationToken);
        Assert.NotNull(workerALock);

        TimeProvider.Advance(TimeSpan.FromMinutes(61));

        // Act
        await using var workerBLock = await Handler.GetWorkItemLockAsync(workItem, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(workerBLock);
    }

    [Fact]
    public async Task HandleItemAsync_WhenEmailSendFails_ShouldNotWriteSentMarkerSoRetryCanSend()
    {
        // Arrange
        var workItem = CreateMonthlyNotificationWorkItem(PrimaryOrganizationId);
        Mailer.ShouldThrow = true;

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(() => HandleWorkItemAsync(workItem));

        var sentMarkerExists = await CacheClient.ExistsAsync(
            OrganizationNotificationWorkItemHandler.GetNotificationSentKey(PrimaryOrganizationId, isOverMonthlyLimit: true));

        Mailer.ShouldThrow = false;
        await HandleWorkItemAsync(workItem);

        // Assert
        Assert.False(sentMarkerExists);
        Assert.Equal(1, Mailer.OrganizationNoticeCount);
    }

    private async Task HandleWorkItemAsync(OrganizationNotificationWorkItem workItem)
    {
        await using var workItemLock = await Handler.GetWorkItemLockAsync(workItem, TestContext.Current.CancellationToken);

        // Mirror production WorkItemJob semantics: when GetWorkItemLockAsync returns null,
        // WorkItemJob calls AbandonAsync and never calls HandleItemAsync. Omitting this guard
        // would let tests call HandleItemAsync without the lock, masking concurrent-access bugs.
        if (workItemLock is null)
            return;

        var context = new WorkItemContext(workItem, "test-job", workItemLock, TestContext.Current.CancellationToken, static (_, _) => Task.CompletedTask);
        await Handler.HandleItemAsync(context);
    }

    private async Task SetOrganizationOverMonthlyLimitAsync(string organizationId)
    {
        var organization = await OrganizationRepository.GetByIdAsync(organizationId, options => options.Cache());
        Assert.NotNull(organization);

        organization.GetCurrentUsage(TimeProvider).Total = organization.GetMaxEventsPerMonthWithBonus(TimeProvider);
        await OrganizationRepository.SaveAsync(organization, options => options.ImmediateConsistency().Cache());
    }

    private static OrganizationNotificationWorkItem CreateMonthlyNotificationWorkItem(string organizationId)
    {
        return new OrganizationNotificationWorkItem
        {
            OrganizationId = organizationId,
            IsOverHourlyLimit = false,
            IsOverMonthlyLimit = true
        };
    }

    private static OrganizationNotificationWorkItem CreateHourlyNotificationWorkItem(string organizationId)
    {
        return new OrganizationNotificationWorkItem
        {
            OrganizationId = organizationId,
            IsOverHourlyLimit = true,
            IsOverMonthlyLimit = false
        };
    }
}
