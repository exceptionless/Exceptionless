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
    }
}