using Exceptionless.Core.Billing;
using Exceptionless.Core.Models;
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

    [Fact]
    public void GetOrganizationLockKey()
    {
        Assert.Equal("Organization:organization-id:billing-plan", BillingManager.GetOrganizationLockKey("organization-id"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void GetOrganizationLockKey_InvalidOrganizationId_Throws(string? organizationId)
    {
        Assert.ThrowsAny<ArgumentException>(() => BillingManager.GetOrganizationLockKey(organizationId!));
    }

    [Fact]
    public void SetStripeSubscriptionId_ChangedOwner_ResetsEventWatermark()
    {
        var billingManager = GetService<BillingManager>();
        var organization = new Organization
        {
            StripeSubscriptionId = "sub_old",
            StripeSubscriptionEventDate = DateTime.UtcNow
        };

        billingManager.SetStripeSubscriptionId(organization, "sub_new");

        Assert.Equal("sub_new", organization.StripeSubscriptionId);
        Assert.Null(organization.StripeSubscriptionEventDate);
    }

    [Fact]
    public void SetStripeSubscriptionId_UnchangedOwner_PreservesEventWatermark()
    {
        var billingManager = GetService<BillingManager>();
        var eventWatermarkUtc = DateTime.UtcNow;
        var organization = new Organization
        {
            StripeSubscriptionId = "sub_current",
            StripeSubscriptionEventDate = eventWatermarkUtc
        };

        billingManager.SetStripeSubscriptionId(organization, "sub_current");

        Assert.Equal(eventWatermarkUtc, organization.StripeSubscriptionEventDate);
    }
}
