using Exceptionless.Core;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Models.Billing;
using Exceptionless.Web.Assistant;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Exceptionless.Tests.Assistant;

public sealed class AssistantAccessServiceTests
{
    [Fact]
    public void ReadFromConfiguration_DefaultsToDisabled()
    {
        var options = CreateOptions();

        Assert.False(options.AssistantOptions.Enabled);
        Assert.False(options.AssistantOptions.IsConfigured);
        Assert.False(options.AssistantOptions.IsAvailable);
        Assert.Equal(AssistantAccessReason.Disabled, AssistantAccessService.EvaluateConfiguration(options)?.Reason);
    }

    [Fact]
    public void ReadFromConfiguration_EnabledAndConfigured_IsAvailable()
    {
        var options = CreateOptions(new Dictionary<string, string?>
        {
            ["Assistant:Enabled"] = "true",
            ["Assistant:ApiKey"] = "test-key"
        });

        Assert.True(options.AssistantOptions.Enabled);
        Assert.True(options.AssistantOptions.IsConfigured);
        Assert.True(options.AssistantOptions.IsAvailable);
        Assert.Null(AssistantAccessService.EvaluateConfiguration(options));
    }

    [Fact]
    public void ReadFromConfiguration_ApiKeyOnly_DefaultsToEnabled()
    {
        var options = CreateOptions(new Dictionary<string, string?> { ["Assistant:ApiKey"] = "test-key" });

        Assert.True(options.AssistantOptions.Enabled);
        Assert.True(options.AssistantOptions.IsAvailable);
        Assert.Null(AssistantAccessService.EvaluateConfiguration(options));
    }

    [Fact]
    public void ReadFromConfiguration_ApiKeyAndExplicitDisable_RemainsDisabled()
    {
        var options = CreateOptions(new Dictionary<string, string?>
        {
            ["Assistant:ApiKey"] = "test-key",
            ["Assistant:Enabled"] = "false"
        });

        Assert.False(options.AssistantOptions.Enabled);
        Assert.False(options.AssistantOptions.IsAvailable);
        Assert.Equal(AssistantAccessReason.Disabled, AssistantAccessService.EvaluateConfiguration(options)?.Reason);
    }

    [Fact]
    public void EvaluateConfiguration_EnabledWithoutApiKey_IsHidden()
    {
        var options = CreateOptions(new Dictionary<string, string?> { ["Assistant:Enabled"] = "true" });

        var access = AssistantAccessService.EvaluateConfiguration(options);

        Assert.NotNull(access);
        Assert.False(access.Enabled);
        Assert.Equal(AssistantAccessReason.NotConfigured, access.Reason);
    }

    [Fact]
    public void EvaluatePlan_UnlimitedPlanAllowsAccess()
    {
        var billingPlans = new BillingPlans(CreateOptions());

        var access = AssistantAccessService.EvaluatePlan(billingPlans.UnlimitedPlan.Assistant);

        Assert.True(access.HasAccess);
        Assert.False(access.UpgradeRequired);
    }

    [Fact]
    public void EvaluatePlan_MissingPlanOptionsRequiresUpgrade()
    {
        var access = AssistantAccessService.EvaluatePlan(planOptions: null);

        Assert.False(access.HasAccess);
        Assert.True(access.UpgradeRequired);
        Assert.Equal(AssistantAccessReason.UpgradeRequired, access.Reason);
    }

    [Fact]
    public void EvaluatePlan_ProductionMediumOrHigherAllowsAccess()
    {
        var billingPlans = new BillingPlans(CreateOptions());

        var access = AssistantAccessService.EvaluatePlan(billingPlans.MediumPlan.Assistant);

        Assert.True(access.HasAccess);
        Assert.False(access.UpgradeRequired);
        Assert.Same(billingPlans.MediumPlan.Assistant, access.PlanOptions);
    }

    [Theory]
    [InlineData("EX_FREE", false)]
    [InlineData("EX_SMALL", false)]
    [InlineData("EX_SMALL_YEARLY", false)]
    [InlineData("EX_MEDIUM", true)]
    [InlineData("EX_MEDIUM_YEARLY", true)]
    [InlineData("EX_LARGE", true)]
    [InlineData("EX_LARGE_YEARLY", true)]
    [InlineData("EX_XL", true)]
    [InlineData("EX_XL_YEARLY", true)]
    [InlineData("EX_ENT", true)]
    [InlineData("EX_ENT_YEARLY", true)]
    [InlineData("EX_UNLIMITED", true)]
    [InlineData("unknown", false)]
    public void AssistantPlanOptions_ReturnsExpected(string planId, bool expected)
    {
        var billingPlans = new BillingPlans(CreateOptions());

        var assistantOptions = billingPlans.GetPlan(planId)?.Assistant;

        Assert.Equal(expected, assistantOptions is not null);
    }

    [Fact]
    public void AssistantPlanOptions_HaveConfiguredTiers()
    {
        var billingPlans = new BillingPlans(CreateOptions());

        AssertPlan(billingPlans.MediumPlan.Assistant, 2, 10, 25_000_000, 5m);
        Assert.Same(billingPlans.MediumPlan.Assistant, billingPlans.MediumYearlyPlan.Assistant);
        AssertPlan(billingPlans.LargePlan.Assistant, 3, 15, 50_000_000, 10m);
        Assert.Same(billingPlans.LargePlan.Assistant, billingPlans.LargeYearlyPlan.Assistant);
        AssertPlan(billingPlans.ExtraLargePlan.Assistant, 5, 25, 100_000_000, 20m);
        Assert.Same(billingPlans.ExtraLargePlan.Assistant, billingPlans.ExtraLargeYearlyPlan.Assistant);
        AssertPlan(billingPlans.EnterprisePlan.Assistant, 10, 50, 250_000_000, 50m);
        Assert.Same(billingPlans.EnterprisePlan.Assistant, billingPlans.EnterpriseYearlyPlan.Assistant);
        AssertPlan(billingPlans.UnlimitedPlan.Assistant, 20, 100, 500_000_000, 100m);
    }

    private static void AssertPlan(AssistantPlanOptions? options, int concurrentTurns, int turnsPerMinute, long monthlyTokens, decimal monthlyCost)
    {
        Assert.NotNull(options);
        Assert.Equal(concurrentTurns, options.MaximumConcurrentTurns);
        Assert.Equal(turnsPerMinute, options.MaximumTurnsPerMinute);
        Assert.Equal(monthlyTokens, options.MaximumMonthlyTokens);
        Assert.Equal(monthlyCost, options.MaximumMonthlyCostUsd);
    }

    private static AppOptions CreateOptions(Dictionary<string, string?>? values = null)
    {
        var configurationValues = new Dictionary<string, string?>
        {
            ["AppMode"] = AppMode.Production.ToString(),
            ["BaseURL"] = "https://localhost"
        };

        if (values is not null)
        {
            foreach (var pair in values)
                configurationValues[pair.Key] = pair.Value;
        }

        return AppOptions.ReadFromConfiguration(new ConfigurationBuilder()
            .AddInMemoryCollection(configurationValues)
            .Build());
    }
}
