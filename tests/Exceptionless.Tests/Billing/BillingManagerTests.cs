using Exceptionless.Core.Billing;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Tests.Billing {
    public class BillingManagerTests : TestWithServices {
        public BillingManagerTests(ITestOutputHelper output) : base(output) {}

        [Fact]
        public void GetBillingPlan() {
            var billingManager = GetService<BillingManager>();
            var plans = GetService<BillingPlans>();
            Assert.Equal(plans.FreePlan.Id, billingManager.GetBillingPlan(plans.FreePlan.Id).Id);
        }
        
        [Fact]
        public void GetBillingPlanByUpsellingRetentionPeriod() {
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
}