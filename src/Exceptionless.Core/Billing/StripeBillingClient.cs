using Exceptionless.Core.Configuration;
using Stripe;

namespace Exceptionless.Core.Billing;

public sealed class StripeBillingClient : IStripeBillingClient
{
    private readonly Lazy<StripeClient> _client;

    public StripeBillingClient(StripeOptions stripeOptions)
    {
        _client = new Lazy<StripeClient>(() =>
            new StripeClient(stripeOptions.StripeApiKey ?? throw new InvalidOperationException("Stripe API key is not configured.")));
    }

    public Task<Stripe.Invoice?> GetInvoiceAsync(string id)
    {
        var service = new InvoiceService(Client);
        return service.GetAsync(id);
    }

    public async Task<IReadOnlyCollection<Stripe.Invoice>> ListInvoicesAsync(InvoiceListOptions options)
    {
        var service = new InvoiceService(Client);
        return (await service.ListAsync(options)).ToList();
    }

    public Task<Customer> CreateCustomerAsync(CustomerCreateOptions options)
    {
        var service = new CustomerService(Client);
        return service.CreateAsync(options);
    }

    public Task<Customer> UpdateCustomerAsync(string customerId, CustomerUpdateOptions options)
    {
        var service = new CustomerService(Client);
        return service.UpdateAsync(customerId, options);
    }

    public Task<Subscription> CreateSubscriptionAsync(SubscriptionCreateOptions options)
    {
        var service = new SubscriptionService(Client);
        return service.CreateAsync(options);
    }

    public Task<Subscription> UpdateSubscriptionAsync(string subscriptionId, SubscriptionUpdateOptions options)
    {
        var service = new SubscriptionService(Client);
        return service.UpdateAsync(subscriptionId, options);
    }

    public async Task<IReadOnlyCollection<Subscription>> ListSubscriptionsAsync(SubscriptionListOptions options)
    {
        var service = new SubscriptionService(Client);
        return (await service.ListAsync(options)).ToList();
    }

    public Task<Subscription> CancelSubscriptionAsync(string subscriptionId, SubscriptionCancelOptions options)
    {
        var service = new SubscriptionService(Client);
        return service.CancelAsync(subscriptionId, options);
    }

    public Task<PaymentMethod> AttachPaymentMethodAsync(string paymentMethodId, PaymentMethodAttachOptions options)
    {
        var service = new PaymentMethodService(Client);
        return service.AttachAsync(paymentMethodId, options);
    }

    private StripeClient Client => _client.Value;
}
