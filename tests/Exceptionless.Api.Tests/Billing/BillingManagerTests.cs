using System;
using Exceptionless.Core.Billing;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Api.Tests.Billing {
    public class BillingManagerTests : TestBase {
        public BillingManagerTests(ITestOutputHelper output) : base(output) {
            Initialize();
        }

        [Fact]
        public void GetBillingPlan() {
            Assert.Equal(BillingManager.FreePlan.Id, BillingManager.GetBillingPlan(BillingManager.FreePlan.Id).Id);
        }

        [Fact]
        public void GetBillingPlanByUpsellingRetentionPeriod() {
            var plan = BillingManager.GetBillingPlanByUpsellingRetentionPeriod(BillingManager.FreePlan.RetentionDays);
            Assert.NotNull(plan);
            Assert.Equal(BillingManager.SmallPlan.Id, plan.Id);
            Assert.Equal(BillingManager.SmallPlan.RetentionDays, plan.RetentionDays);

            plan = BillingManager.GetBillingPlanByUpsellingRetentionPeriod(BillingManager.SmallPlan.RetentionDays);
            Assert.NotNull(plan);
            Assert.Equal(BillingManager.MediumPlan.Id, plan.Id);
            Assert.Equal(BillingManager.MediumPlan.RetentionDays, plan.RetentionDays);

            plan = BillingManager.GetBillingPlanByUpsellingRetentionPeriod(BillingManager.MediumPlan.RetentionDays);
            Assert.NotNull(plan);
            Assert.Equal(BillingManager.LargePlan.Id, plan.Id);
            Assert.Equal(BillingManager.LargePlan.RetentionDays, plan.RetentionDays);

            plan = BillingManager.GetBillingPlanByUpsellingRetentionPeriod(BillingManager.LargePlan.RetentionDays);
            Assert.Null(plan);
        }
    }
}