using Exceptionless.Core.Billing;
using Exceptionless.Core.Configuration;
using Xunit;

namespace Exceptionless.Tests.Billing;

public sealed class StripeBillingClientTests
{
    [Fact]
    public void Constructor_WhenStripeApiKeyIsMissing_DoesNotThrow()
    {
        _ = new StripeBillingClient(new StripeOptions());
    }

    [Fact]
    public async Task GetInvoiceAsync_WhenStripeApiKeyIsMissing_Throws()
    {
        var client = new StripeBillingClient(new StripeOptions());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetInvoiceAsync("in_test"));
        Assert.Equal("Stripe API key is not configured.", ex.Message);
    }
}
