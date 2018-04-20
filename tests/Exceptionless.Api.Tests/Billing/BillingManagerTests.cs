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
    }
}