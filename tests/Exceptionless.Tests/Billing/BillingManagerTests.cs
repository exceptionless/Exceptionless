using Exceptionless.Core.Billing;
using Exceptionless.Core.Configuration;
using Exceptionless.Core.Models;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Exceptionless.Tests.Billing;

public class BillingManagerTests : TestWithServices
{
    private readonly BillingManager _billingManager;
    private readonly BillingPlans _plans;

    public BillingManagerTests(ITestOutputHelper output) : base(output)
    {
        _billingManager = GetService<BillingManager>();
        _plans = GetService<BillingPlans>();
    }

    [Fact]
    public void GetBillingPlan()
    {
        Assert.Equal(_plans.FreePlan.Id, _billingManager.GetBillingPlan(_plans.FreePlan.Id)?.Id);
    }

    [Fact]
    public void GetBillingPlanByUpsellingRetentionPeriod()
    {
        var plan = _billingManager.GetBillingPlanByUpsellingRetentionPeriod(_plans.FreePlan.RetentionDays);
        Assert.NotNull(plan);
        Assert.Equal(_plans.SmallPlan.Id, plan.Id);
        Assert.Equal(_plans.SmallPlan.RetentionDays, plan.RetentionDays);

        plan = _billingManager.GetBillingPlanByUpsellingRetentionPeriod(_plans.SmallPlan.RetentionDays);
        Assert.NotNull(plan);
        Assert.Equal(_plans.MediumPlan.Id, plan.Id);
        Assert.Equal(_plans.MediumPlan.RetentionDays, plan.RetentionDays);

        plan = _billingManager.GetBillingPlanByUpsellingRetentionPeriod(_plans.MediumPlan.RetentionDays);
        Assert.NotNull(plan);
        Assert.Equal(_plans.LargePlan.Id, plan.Id);
        Assert.Equal(_plans.LargePlan.RetentionDays, plan.RetentionDays);

        plan = _billingManager.GetBillingPlanByUpsellingRetentionPeriod(_plans.LargePlan.RetentionDays);
        Assert.Null(plan);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task TryAcquireOrganizationLockAsync_InvalidOrganizationId_Throws(string? organizationId)
    {
        // Arrange
        Func<Task> acquireLock = async () =>
        {
            _ = await _billingManager.TryAcquireOrganizationLockAsync(organizationId!);
        };

        // Act
        var exception = await Record.ExceptionAsync(acquireLock);

        // Assert
        Assert.IsAssignableFrom<ArgumentException>(exception);
    }

    [Fact]
    public void SetStripeSubscriptionId_ChangedOwner_ResetsEventWatermark()
    {
        // Arrange
        var organization = new Organization
        {
            StripeSubscriptionId = "sub_old",
            StripeSubscriptionEventDate = DateTime.UtcNow
        };

        // Act
        _billingManager.SetStripeSubscriptionId(organization, "sub_new");

        // Assert
        Assert.Equal("sub_new", organization.StripeSubscriptionId);
        Assert.Null(organization.StripeSubscriptionEventDate);
    }

    [Fact]
    public void SetStripeSubscriptionId_UnchangedOwner_PreservesEventWatermark()
    {
        // Arrange
        var eventWatermarkUtc = DateTime.UtcNow;
        var organization = new Organization
        {
            StripeSubscriptionId = "sub_current",
            StripeSubscriptionEventDate = eventWatermarkUtc
        };

        // Act
        _billingManager.SetStripeSubscriptionId(organization, "sub_current");

        // Assert
        Assert.Equal(eventWatermarkUtc, organization.StripeSubscriptionEventDate);
    }

    [Fact]
    public void ReadFromConfiguration_NoTrialSettings_UsesFourteenDayTrialWithThreeDayReminder()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();

        // Act
        var options = BillingOptions.ReadFromConfiguration(configuration);

        // Assert
        Assert.Equal(14, options.TrialDays);
        Assert.Equal(3, options.TrialReminderDays);
    }

    [Fact]
    public void ApplyTrialPlan_DefaultOptions_EndsTrialInFourteenDays()
    {
        // Arrange
        var utcNow = new DateTime(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);
        TimeProvider.SetUtcNow(utcNow);
        var organization = new Organization { TrialReminderSentUtc = utcNow };

        // Act
        _billingManager.ApplyTrialPlan(organization);

        // Assert
        Assert.Equal(_plans.TrialPlan.Id, organization.PlanId);
        Assert.Equal(utcNow.AddDays(14), organization.TrialEndsUtc);
        Assert.Null(organization.TrialReminderSentUtc);
    }

    [Fact]
    public void ReadFromConfiguration_HostedBilling_EnablesTrialsWithoutAdditionalConfiguration()
    {
        // Arrange
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["BaseURL"] = "http://localhost",
            ["StripeApiKey"] = "sk_test_local",
            ["AppMode"] = "Staging"
        }).Build();

        // Act
        var options = Exceptionless.Core.AppOptions.ReadFromConfiguration(config);

        // Assert
        Assert.True(options.StripeOptions.EnableBilling);
        Assert.Equal(14, options.BillingOptions.TrialDays);
    }
}
