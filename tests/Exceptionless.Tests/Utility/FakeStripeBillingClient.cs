using Exceptionless.Core.Billing;
using Stripe;

namespace Exceptionless.Tests.Utility;

public sealed class FakeStripeBillingClient : IStripeBillingClient
{
    public Stripe.Invoice? Invoice { get; set; }

    public List<Stripe.Invoice> Invoices { get; } = [];

    public List<Subscription> Subscriptions { get; } = [];

    public Dictionary<string, Subscription> CanceledSubscriptionResults { get; } = [];

    public List<string> Calls { get; } = [];

    public Customer CustomerToReturn { get; set; } = new() { Id = "cus_test" };

    public Stripe.Invoice FinalizedInvoiceToReturn { get; set; } = new() { Id = "in_finalized", Status = "paid", EndingBalance = 0 };

    public Exception? GetInvoiceException { get; set; }

    public Exception? FinalizeInvoiceException { get; set; }

    public Exception? CreateCustomerException { get; set; }

    public Exception? UpdateCustomerException { get; set; }

    public Exception? CreateSubscriptionException { get; set; }

    public Exception? GetSubscriptionException { get; set; }

    public Exception? UpdateSubscriptionException { get; set; }

    public Exception? ListSubscriptionsException { get; set; }

    public Exception? CancelSubscriptionException { get; set; }

    public Exception? AttachPaymentMethodException { get; set; }

    public string? LastGetInvoiceId { get; private set; }

    public string? LastGetSubscriptionId { get; private set; }

    public InvoiceListOptions? LastInvoiceListOptions { get; private set; }

    public InvoiceListOptions? LastAutoPagingInvoiceListOptions { get; private set; }

    public List<(string InvoiceId, InvoiceFinalizeOptions Options, string IdempotencyKey)> FinalizedInvoices { get; } = [];

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
        CanceledSubscriptionResults.Clear();
        Calls.Clear();
        CustomerToReturn = new Customer { Id = "cus_test" };
        FinalizedInvoiceToReturn = new Stripe.Invoice { Id = "in_finalized", Status = "paid", EndingBalance = 0 };
        GetInvoiceException = null;
        FinalizeInvoiceException = null;
        CreateCustomerException = null;
        UpdateCustomerException = null;
        CreateSubscriptionException = null;
        GetSubscriptionException = null;
        UpdateSubscriptionException = null;
        ListSubscriptionsException = null;
        CancelSubscriptionException = null;
        AttachPaymentMethodException = null;
        LastGetInvoiceId = null;
        LastGetSubscriptionId = null;
        LastInvoiceListOptions = null;
        LastAutoPagingInvoiceListOptions = null;
        FinalizedInvoices.Clear();
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

        return Task.FromResult(Invoices.FirstOrDefault(invoice => String.Equals(invoice.Id, id, StringComparison.Ordinal)) ?? Invoice);
    }

    public Task<IReadOnlyCollection<Stripe.Invoice>> ListInvoicesAsync(InvoiceListOptions options)
    {
        LastInvoiceListOptions = options;
        return Task.FromResult<IReadOnlyCollection<Stripe.Invoice>>(Invoices.ToList());
    }

    public async IAsyncEnumerable<Stripe.Invoice> ListInvoicesAutoPagingAsync(InvoiceListOptions options)
    {
        LastAutoPagingInvoiceListOptions = options;

        foreach (var invoice in Invoices)
            yield return invoice;
    }

    public Task<Stripe.Invoice> FinalizeInvoiceAsync(string id, InvoiceFinalizeOptions options, string idempotencyKey)
    {
        FinalizedInvoices.Add((id, options, idempotencyKey));
        Calls.Add($"finalize:{id}");
        if (FinalizeInvoiceException is not null)
        {
            throw FinalizeInvoiceException;
        }

        FinalizedInvoiceToReturn.Id = id;
        return Task.FromResult(FinalizedInvoiceToReturn);
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
        if (UpdateCustomerException is not null)
            throw UpdateCustomerException;

        return Task.FromResult(new Customer { Id = customerId });
    }

    public Task<Subscription> CreateSubscriptionAsync(SubscriptionCreateOptions options)
    {
        CreatedSubscriptionOptions.Add(options);
        Calls.Add("create-subscription");
        if (CreateSubscriptionException is not null)
            throw CreateSubscriptionException;

        return Task.FromResult(new Subscription { Id = "sub_created" });
    }

    public Task<Subscription> GetSubscriptionAsync(string id)
    {
        LastGetSubscriptionId = id;
        if (GetSubscriptionException is not null)
            throw GetSubscriptionException;

        return Task.FromResult(Subscriptions.FirstOrDefault(subscription => String.Equals(subscription.Id, id, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"Subscription {id} was not found."));
    }

    public Task<Subscription> UpdateSubscriptionAsync(string subscriptionId, SubscriptionUpdateOptions options)
    {
        UpdatedSubscriptions.Add((subscriptionId, options));
        Calls.Add($"update-subscription:{subscriptionId}");
        if (UpdateSubscriptionException is not null)
            throw UpdateSubscriptionException;

        return Task.FromResult(new Subscription { Id = subscriptionId });
    }

    public Task<IReadOnlyCollection<Subscription>> ListSubscriptionsAsync(SubscriptionListOptions options)
    {
        LastSubscriptionListOptions = options;
        if (ListSubscriptionsException is not null)
            throw ListSubscriptionsException;

        return Task.FromResult<IReadOnlyCollection<Subscription>>(Subscriptions.ToList());
    }

    public Task<Subscription> CancelSubscriptionAsync(string subscriptionId, SubscriptionCancelOptions options)
    {
        CanceledSubscriptions.Add((subscriptionId, options));
        Calls.Add($"cancel-subscription:{subscriptionId}");
        if (CancelSubscriptionException is not null)
            throw CancelSubscriptionException;

        return Task.FromResult(CanceledSubscriptionResults.TryGetValue(subscriptionId, out var subscription)
            ? subscription
            : new Subscription { Id = subscriptionId });
    }

    public Task<PaymentMethod> AttachPaymentMethodAsync(string paymentMethodId, PaymentMethodAttachOptions options)
    {
        AttachedPaymentMethods.Add((paymentMethodId, options));
        if (AttachPaymentMethodException is not null)
            throw AttachPaymentMethodException;

        return Task.FromResult(new PaymentMethod { Id = paymentMethodId });
    }
}
