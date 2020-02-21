using Exceptionless.Core.Billing;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Tests.Billing {
    public class BillingManagerTests : TestWithServices {
        public BillingManagerTests(ServicesFixture fixture, ITestOutputHelper output) : base(fixture, output) {}

        [Fact]
        public void GetBillingPlan() {
            var billingManager = GetService<BillingManager>();
            var plans = GetService<BillingPlans>();
            Assert.Equal(plans.FreePlan.Id, billingManager.GetBillingPlan(plans.FreePlan.Id).Id);
        }
    }
}