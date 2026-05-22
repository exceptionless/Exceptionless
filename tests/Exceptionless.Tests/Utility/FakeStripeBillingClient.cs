using Exceptionless.Core.Billing;
using Stripe;

namespace Exceptionless.Tests.Utility;

public sealed class FakeStripeBillingClient : IStripeBillingClient
{
    public Stripe.Invoice? Invoice { get; set; }

    public List<Stripe.Invoice> Invoices { get; } = [];

    public List<Subscription> Subscriptions { get; } = [];

    public Customer CustomerToReturn { get; set; } = new() { Id = "cus_test" };

    public Exception? GetInvoiceException { get; set; }

    public Exception? CreateCustomerException { get; set; }

    public string? LastGetInvoiceId { get; private set; }

    public InvoiceListOptions? LastInvoiceListOptions { get; private set; }

    public SubscriptionListOptions? LastSubscriptionListOptions { get; private set; }

    public CustomerCreateOptions? LastCustomerCreateOptions { get; private set; }

    public List<SubscriptionCreateOptions> CreatedSubscriptionOptions { get; } = [];

    public List<(string CustomerId, CustomerUpdateOptions Options)> UpdatedCustomers { get; } = [];

    public List<(string SubscriptionId, SubscriptionUpdateOptions Options)> UpdatedSubscriptions { get; } = [];

    public List<(string SubscriptionId, SubscriptionCancelOptions Options)> CanceledSubscriptions { get; } = [];

    public List<(string PaymentMethodId, PaymentMethodAttachOptions Options)> AttachedPaymentMethods { get; } = [];

    public void Reset()
    {
        Invoice = null;
        Invoices.Clear();
        Subscriptions.Clear();
        CustomerToReturn = new Customer { Id = "cus_test" };
        GetInvoiceException = null;
        CreateCustomerException = null;
        LastGetInvoiceId = null;
        LastInvoiceListOptions = null;
        LastSubscriptionListOptions = null;
        LastCustomerCreateOptions = null;
        CreatedSubscriptionOptions.Clear();
        UpdatedCustomers.Clear();
        UpdatedSubscriptions.Clear();
        CanceledSubscriptions.Clear();
        AttachedPaymentMethods.Clear();
    }

    public Task<Stripe.Invoice?> GetInvoiceAsync(string id)
    {
        LastGetInvoiceId = id;
        if (GetInvoiceException is not null)
            throw GetInvoiceException;

        return Task.FromResult(Invoice);
    }

    public Task<IReadOnlyCollection<Stripe.Invoice>> ListInvoicesAsync(InvoiceListOptions options)
    {
        LastInvoiceListOptions = options;
        return Task.FromResult<IReadOnlyCollection<Stripe.Invoice>>(Invoices.ToList());
    }

    public Task<Customer> CreateCustomerAsync(CustomerCreateOptions options)
    {
        LastCustomerCreateOptions = options;
        if (CreateCustomerException is not null)
            throw CreateCustomerException;

        return Task.FromResult(CustomerToReturn);
    }

    public Task<Customer> UpdateCustomerAsync(string customerId, CustomerUpdateOptions options)
    {
        UpdatedCustomers.Add((customerId, options));
        return Task.FromResult(new Customer { Id = customerId });
    }

    public Task<Subscription> CreateSubscriptionAsync(SubscriptionCreateOptions options)
    {
        CreatedSubscriptionOptions.Add(options);
        return Task.FromResult(new Subscription { Id = "sub_created" });
    }

    public Task<Subscription> UpdateSubscriptionAsync(string subscriptionId, SubscriptionUpdateOptions options)
    {
        UpdatedSubscriptions.Add((subscriptionId, options));
        return Task.FromResult(new Subscription { Id = subscriptionId });
    }

    public Task<IReadOnlyCollection<Subscription>> ListSubscriptionsAsync(SubscriptionListOptions options)
    {
        LastSubscriptionListOptions = options;
        return Task.FromResult<IReadOnlyCollection<Subscription>>(Subscriptions.ToList());
    }

    public Task<Subscription> CancelSubscriptionAsync(string subscriptionId, SubscriptionCancelOptions options)
    {
        CanceledSubscriptions.Add((subscriptionId, options));
        return Task.FromResult(new Subscription { Id = subscriptionId });
    }

    public Task<PaymentMethod> AttachPaymentMethodAsync(string paymentMethodId, PaymentMethodAttachOptions options)
    {
        AttachedPaymentMethods.Add((paymentMethodId, options));
        return Task.FromResult(new PaymentMethod { Id = paymentMethodId });
    }
}
