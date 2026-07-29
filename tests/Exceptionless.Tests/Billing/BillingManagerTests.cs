using Exceptionless.Core.Billing;
using Exceptionless.Core.Models;
using Xunit;

namespace Exceptionless.Tests.Billing;

public class BillingManagerTests : TestWithServices
{
    public BillingManagerTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void ApplyBillingPlan_ExistingPlanWithoutUsage_BackfillsPreviousPlanLimit()
    {
        // Arrange
        var billingManager = GetService<BillingManager>();
        var plans = GetService<BillingPlans>();
        var utcNow = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc);
        TimeProvider.SetUtcNow(utcNow);
        var organization = new Organization
        {
            PlanId = plans.SmallPlan.Id,
            MaxEventsPerMonth = plans.SmallPlan.MaxEventsPerMonth
        };

        // Act
        billingManager.ApplyBillingPlan(organization, plans.MediumPlan);

        // Assert
        Assert.Equal(12, organization.Usage.Count);
        Assert.All(organization.Usage.Where(u => u.Date < new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc)),
            usage => Assert.Equal(plans.SmallPlan.MaxEventsPerMonth, usage.Limit));
        Assert.Equal(plans.MediumPlan.MaxEventsPerMonth, organization.Usage.Single(u => u.Date == new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc)).Limit);
    }

    [Fact]
    public void ApplyBillingPlan_NewOrganization_DoesNotInventPreviousPlanHistory()
    {
        // Arrange
        var billingManager = GetService<BillingManager>();
        var plans = GetService<BillingPlans>();
        var utcNow = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc);
        TimeProvider.SetUtcNow(utcNow);
        var organization = new Organization();

        // Act
        billingManager.ApplyBillingPlan(organization, plans.FreePlan);

        // Assert
        var usage = Assert.Single(organization.Usage);
        Assert.Equal(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), usage.Date);
        Assert.Equal(plans.FreePlan.MaxEventsPerMonth, usage.Limit);
    }

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
}
