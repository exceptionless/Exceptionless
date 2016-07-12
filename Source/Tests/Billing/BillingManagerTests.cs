using System;
using Exceptionless.Core.Billing;
using Xunit;

namespace Exceptionless.Api.Tests.Billing {
    public class BillingManagerTests {
        [Fact]
        public void GetBillingPlan() {
            Assert.Equal(BillingManager.FreePlan.Id, BillingManager.GetBillingPlan(BillingManager.FreePlan.Id).Id);
        }

        [Fact]
        public void GetBillingPlanByUpsellingRetentionPeriod() {
            var plan = BillingManager.GetBillingPlanByUpsellingRetentionPeriod(BillingManager.FreePlan.RetentionDays);
            Assert.NotNull(plan);
            Assert.Equal(plan.Id, BillingManager.SmallPlan.Id);
            Assert.Equal(plan.RetentionDays, BillingManager.SmallPlan.RetentionDays);

            plan = BillingManager.GetBillingPlanByUpsellingRetentionPeriod(BillingManager.SmallPlan.RetentionDays);
            Assert.NotNull(plan);
            Assert.Equal(plan.Id, BillingManager.MediumPlan.Id);
            Assert.Equal(plan.RetentionDays, BillingManager.MediumPlan.RetentionDays);

            plan = BillingManager.GetBillingPlanByUpsellingRetentionPeriod(BillingManager.MediumPlan.RetentionDays);
            Assert.NotNull(plan);
            Assert.Equal(plan.Id, BillingManager.LargePlan.Id);
            Assert.Equal(plan.RetentionDays, BillingManager.LargePlan.RetentionDays);

            plan = BillingManager.GetBillingPlanByUpsellingRetentionPeriod(BillingManager.LargePlan.RetentionDays);
            Assert.Null(plan);
        }
    }
}