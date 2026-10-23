using Exceptionless.Core.Billing;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Billing;
using Exceptionless.DateTimeExtensions;
using Xunit;

namespace Exceptionless.Tests.Billing;

public class BillingManagerTests : TestWithServices
{
    private static readonly DateTime PlanChangeUtc = new(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime PreviousMonthUtc = new(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);

    public BillingManagerTests(ITestOutputHelper output) : base(output) { }

    [Theory]
    [InlineData("bonus without usage", null, 5_000, 15_000)]
    [InlineData("persisted bonus", 20_000, 5_000, 20_000)]
    [InlineData("missing usage", null, 0, 15_000)]
    [InlineData("zero-valued usage", 0, 0, 15_000)]
    public void ApplyBillingPlan_ConfirmedPreviousPlan_CreatesExpectedAnchor(string _, int? persistedLimit, int bonusEvents, int expectedLimit)
    {
        // Arrange
        var billingManager = GetService<BillingManager>();
        var plans = GetService<BillingPlans>();
        TimeProvider.SetUtcNow(PlanChangeUtc);
        var organization = new Organization
        {
            CreatedUtc = new DateTime(2026, 4, 15, 0, 0, 0, DateTimeKind.Utc),
            PlanId = plans.SmallPlan.Id,
            MaxEventsPerMonth = plans.SmallPlan.MaxEventsPerMonth,
            BonusEventsPerMonth = bonusEvents,
            BonusExpiration = bonusEvents > 0 ? PlanChangeUtc.AddMonths(1) : null
        };
        if (persistedLimit.HasValue)
            organization.Usage = [new UsageInfo { Date = PreviousMonthUtc, Limit = persistedLimit.Value }];

        // Act
        billingManager.ApplyBillingPlan(organization, plans.MediumPlan);

        // Assert
        Assert.Equal(2, organization.Usage.Count);
        Assert.DoesNotContain(organization.Usage, usage => usage.Date < new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc));
        Assert.Equal(expectedLimit, organization.Usage.Single(usage => usage.Date == PreviousMonthUtc).Limit);
        Assert.Equal(plans.MediumPlan.MaxEventsPerMonth + bonusEvents,
            organization.Usage.Single(usage => usage.Date == PlanChangeUtc.StartOfMonth()).Limit);
    }

    [Theory]
    [InlineData(PreviousPlanScenario.MissingLimit)]
    [InlineData(PreviousPlanScenario.NewOrganization)]
    [InlineData(PreviousPlanScenario.CreatedThisMonth)]
    [InlineData(PreviousPlanScenario.UnchangedPlan)]
    public void ApplyBillingPlan_WithoutConfirmedPreviousMonth_DoesNotCreateAnchor(PreviousPlanScenario scenario)
    {
        // Arrange
        var billingManager = GetService<BillingManager>();
        var plans = GetService<BillingPlans>();
        TimeProvider.SetUtcNow(PlanChangeUtc);
        var (organization, targetPlan) = scenario switch
        {
            PreviousPlanScenario.MissingLimit => (
                new Organization { CreatedUtc = PlanChangeUtc.AddMonths(-2), PlanId = plans.SmallPlan.Id },
                plans.MediumPlan),
            PreviousPlanScenario.NewOrganization => (new Organization(), plans.FreePlan),
            PreviousPlanScenario.CreatedThisMonth => (
                new Organization
                {
                    CreatedUtc = PlanChangeUtc.AddDays(-5),
                    PlanId = plans.FreePlan.Id,
                    MaxEventsPerMonth = plans.FreePlan.MaxEventsPerMonth
                },
                plans.SmallPlan),
            PreviousPlanScenario.UnchangedPlan => (
                new Organization
                {
                    CreatedUtc = PlanChangeUtc.AddMonths(-2),
                    PlanId = plans.SmallPlan.Id.ToUpperInvariant(),
                    MaxEventsPerMonth = plans.SmallPlan.MaxEventsPerMonth
                },
                plans.SmallPlan),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };

        // Act
        billingManager.ApplyBillingPlan(organization, targetPlan);

        // Assert
        var usage = Assert.Single(organization.Usage);
        Assert.Equal(PlanChangeUtc.StartOfMonth(), usage.Date);
        Assert.Equal(targetPlan.MaxEventsPerMonth, usage.Limit);
    }

    [Fact]
    public void ApplyBillingPlan_UnchangedPlan_UpdatesBillingChangeDate()
    {
        // Arrange
        var billingManager = GetService<BillingManager>();
        var plans = GetService<BillingPlans>();
        TimeProvider.SetUtcNow(new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc));
        var organization = new Organization
        {
            CreatedUtc = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc),
            PlanId = plans.SmallPlan.Id,
            MaxEventsPerMonth = plans.SmallPlan.MaxEventsPerMonth,
            BillingChangeDate = new DateTime(2026, 4, 15, 0, 0, 0, DateTimeKind.Utc)
        };

        // Act
        billingManager.ApplyBillingPlan(organization, plans.SmallPlan);

        // Assert
        Assert.Equal(TimeProvider.GetUtcNow().UtcDateTime, organization.BillingChangeDate);
    }

    [Fact]
    public void ApplyBillingPlan_SamePlanWithChangedLimit_CreatesOutgoingAnchor()
    {
        // Arrange
        var billingManager = GetService<BillingManager>();
        var plans = GetService<BillingPlans>();
        TimeProvider.SetUtcNow(PlanChangeUtc);
        var organization = new Organization
        {
            CreatedUtc = PlanChangeUtc.AddMonths(-2),
            PlanId = plans.SmallPlan.Id,
            MaxEventsPerMonth = plans.SmallPlan.MaxEventsPerMonth
        };
        BillingPlan updatedPlan = plans.SmallPlan with { MaxEventsPerMonth = plans.SmallPlan.MaxEventsPerMonth * 2 };

        // Act
        billingManager.ApplyBillingPlan(organization, updatedPlan);

        // Assert
        Assert.Equal(plans.SmallPlan.MaxEventsPerMonth,
            organization.Usage.Single(usage => usage.Date == PreviousMonthUtc).Limit);
        Assert.Equal(updatedPlan.MaxEventsPerMonth,
            organization.Usage.Single(usage => usage.Date == PlanChangeUtc.StartOfMonth()).Limit);
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

    public enum PreviousPlanScenario
    {
        MissingLimit,
        NewOrganization,
        CreatedThisMonth,
        UnchangedPlan
    }
}
