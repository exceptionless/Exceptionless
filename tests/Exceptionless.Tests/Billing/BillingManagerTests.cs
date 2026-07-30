using Exceptionless.Core.Billing;
using Exceptionless.Core.Models;
using Xunit;

namespace Exceptionless.Tests.Billing;

public class BillingManagerTests : TestWithServices
{
    public BillingManagerTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void ApplyBillingPlan_ExistingBonus_CreatesPreviousPlanAnchorWithBonus()
    {
        // Arrange
        var billingManager = GetService<BillingManager>();
        var plans = GetService<BillingPlans>();
        TimeProvider.SetUtcNow(new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc));
        var organization = new Organization
        {
            CreatedUtc = new DateTime(2026, 4, 15, 0, 0, 0, DateTimeKind.Utc),
            PlanId = plans.SmallPlan.Id,
            MaxEventsPerMonth = plans.SmallPlan.MaxEventsPerMonth,
            BonusEventsPerMonth = 5_000,
            BonusExpiration = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        // Act
        billingManager.ApplyBillingPlan(organization, plans.MediumPlan);

        // Assert
        Assert.Equal(plans.SmallPlan.MaxEventsPerMonth + organization.BonusEventsPerMonth,
            organization.Usage.Single(u => u.Date == new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc)).Limit);
    }

    [Fact]
    public void ApplyBillingPlan_ExistingPlanWithoutUsage_CreatesPreviousPlanAnchor()
    {
        // Arrange
        var billingManager = GetService<BillingManager>();
        var plans = GetService<BillingPlans>();
        var utcNow = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc);
        TimeProvider.SetUtcNow(utcNow);
        var organization = new Organization
        {
            CreatedUtc = new DateTime(2026, 4, 15, 0, 0, 0, DateTimeKind.Utc),
            PlanId = plans.SmallPlan.Id,
            MaxEventsPerMonth = plans.SmallPlan.MaxEventsPerMonth
        };

        // Act
        billingManager.ApplyBillingPlan(organization, plans.MediumPlan);

        // Assert
        Assert.Equal(2, organization.Usage.Count);
        Assert.DoesNotContain(organization.Usage, usage => usage.Date < new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc));
        Assert.Equal(plans.SmallPlan.MaxEventsPerMonth, organization.Usage.Single(u => u.Date == new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc)).Limit);
        Assert.Equal(plans.MediumPlan.MaxEventsPerMonth, organization.Usage.Single(u => u.Date == new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc)).Limit);
    }

    [Fact]
    public void ApplyBillingPlan_MissingStoredLimit_ResolvesPreviousPlanLimit()
    {
        // Arrange
        var billingManager = GetService<BillingManager>();
        var plans = GetService<BillingPlans>();
        TimeProvider.SetUtcNow(new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc));
        var organization = new Organization
        {
            CreatedUtc = new DateTime(2026, 4, 15, 0, 0, 0, DateTimeKind.Utc),
            PlanId = plans.SmallPlan.Id
        };

        // Act
        billingManager.ApplyBillingPlan(organization, plans.MediumPlan);

        // Assert
        Assert.Equal(plans.SmallPlan.MaxEventsPerMonth, organization.Usage.Single(u => u.Date == new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc)).Limit);
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
    public void ApplyBillingPlan_OrganizationCreatedThisMonth_DoesNotInventPreviousPlanHistory()
    {
        // Arrange
        var billingManager = GetService<BillingManager>();
        var plans = GetService<BillingPlans>();
        TimeProvider.SetUtcNow(new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc));
        var organization = new Organization
        {
            CreatedUtc = new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc),
            PlanId = plans.FreePlan.Id,
            MaxEventsPerMonth = plans.FreePlan.MaxEventsPerMonth
        };

        // Act
        billingManager.ApplyBillingPlan(organization, plans.SmallPlan);

        // Assert
        var usage = Assert.Single(organization.Usage);
        Assert.Equal(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), usage.Date);
        Assert.Equal(plans.SmallPlan.MaxEventsPerMonth, usage.Limit);
    }

    [Fact]
    public void ApplyBillingPlan_UnknownPreviousPlan_DoesNotInventPreviousPlanHistory()
    {
        // Arrange
        var billingManager = GetService<BillingManager>();
        var plans = GetService<BillingPlans>();
        TimeProvider.SetUtcNow(new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc));
        var organization = new Organization
        {
            CreatedUtc = new DateTime(2026, 4, 15, 0, 0, 0, DateTimeKind.Utc),
            PlanId = "missing-plan"
        };

        // Act
        billingManager.ApplyBillingPlan(organization, plans.SmallPlan);

        // Assert
        var usage = Assert.Single(organization.Usage);
        Assert.Equal(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), usage.Date);
        Assert.Equal(plans.SmallPlan.MaxEventsPerMonth, usage.Limit);
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
