using Exceptionless.Core.Billing;
using Exceptionless.Core.Configuration;
using Exceptionless.Core.Models;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Exceptionless.Tests.Billing;

public class BillingManagerTests : TestWithServices
{
    public BillingManagerTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void GetBillingPlan()
    {
        var billingManager = GetService<BillingManager>();
        var plans = GetService<BillingPlans>();
        Assert.Equal(plans.FreePlan.Id, billingManager.GetBillingPlan(plans.FreePlan.Id)?.Id);
    }

    [Fact]
    public void GetBillingPlanByUpsellingRetentionPeriod()
    {
        var billingManager = GetService<BillingManager>();
        var plans = GetService<BillingPlans>();

        var plan = billingManager.GetBillingPlanByUpsellingRetentionPeriod(plans.FreePlan.RetentionDays);
        Assert.NotNull(plan);
        Assert.Equal(plans.SmallPlan.Id, plan.Id);
        Assert.Equal(plans.SmallPlan.RetentionDays, plan.RetentionDays);

        plan = billingManager.GetBillingPlanByUpsellingRetentionPeriod(plans.SmallPlan.RetentionDays);
        Assert.NotNull(plan);
        Assert.Equal(plans.MediumPlan.Id, plan.Id);
        Assert.Equal(plans.MediumPlan.RetentionDays, plan.RetentionDays);

        plan = billingManager.GetBillingPlanByUpsellingRetentionPeriod(plans.MediumPlan.RetentionDays);
        Assert.NotNull(plan);
        Assert.Equal(plans.LargePlan.Id, plan.Id);
        Assert.Equal(plans.LargePlan.RetentionDays, plan.RetentionDays);

        plan = billingManager.GetBillingPlanByUpsellingRetentionPeriod(plans.LargePlan.RetentionDays);
        Assert.Null(plan);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task TryAcquireOrganizationLockAsync_InvalidOrganizationId_Throws(string? organizationId)
    {
        // Arrange
        var billingManager = GetService<BillingManager>();

        // Act
        var exception = await Record.ExceptionAsync(async () =>
        {
            _ = await billingManager.TryAcquireOrganizationLockAsync(organizationId!);
        });

        // Assert
        Assert.IsAssignableFrom<ArgumentException>(exception);
    }

    [Fact]
    public void SetStripeSubscriptionId_ChangedOwner_ResetsEventWatermark()
    {
        // Arrange
        var billingManager = GetService<BillingManager>();
        var organization = new Organization
        {
            StripeSubscriptionId = "sub_old",
            StripeSubscriptionEventDate = DateTime.UtcNow
        };

        // Act
        billingManager.SetStripeSubscriptionId(organization, "sub_new");

        // Assert
        Assert.Equal("sub_new", organization.StripeSubscriptionId);
        Assert.Null(organization.StripeSubscriptionEventDate);
    }

    [Fact]
    public void SetStripeSubscriptionId_UnchangedOwner_PreservesEventWatermark()
    {
        // Arrange
        var billingManager = GetService<BillingManager>();
        var eventWatermarkUtc = DateTime.UtcNow;
        var organization = new Organization
        {
            StripeSubscriptionId = "sub_current",
            StripeSubscriptionEventDate = eventWatermarkUtc
        };

        // Act
        billingManager.SetStripeSubscriptionId(organization, "sub_current");

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
        var billingManager = GetService<BillingManager>();
        var plans = GetService<BillingPlans>();
        var utcNow = new DateTime(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);
        TimeProvider.SetUtcNow(utcNow);
        var organization = new Organization { TrialReminderSentUtc = utcNow };

        // Act
        billingManager.ApplyTrialPlan(organization);

        // Assert
        Assert.Equal(plans.TrialPlan.Id, organization.PlanId);
        Assert.Equal(utcNow.AddDays(14), organization.TrialEndsUtc);
        Assert.Null(organization.TrialReminderSentUtc);
    }
}
