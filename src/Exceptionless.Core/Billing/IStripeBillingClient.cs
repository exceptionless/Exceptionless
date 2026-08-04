using Stripe;

namespace Exceptionless.Core.Billing;

public interface IStripeBillingClient
{
    Task<Stripe.Invoice?> GetInvoiceAsync(string id);

    Task<IReadOnlyCollection<Stripe.Invoice>> ListInvoicesAsync(InvoiceListOptions options);

    IAsyncEnumerable<Stripe.Invoice> ListInvoicesAutoPagingAsync(InvoiceListOptions options);

    Task<Stripe.Invoice> FinalizeInvoiceAsync(string id, InvoiceFinalizeOptions options, string idempotencyKey);

    Task<Customer> CreateCustomerAsync(CustomerCreateOptions options);

    Task<Customer> UpdateCustomerAsync(string customerId, CustomerUpdateOptions options);

    Task<Subscription> CreateSubscriptionAsync(SubscriptionCreateOptions options);

    Task<Subscription> GetSubscriptionAsync(string id);

    Task<Subscription> UpdateSubscriptionAsync(string subscriptionId, SubscriptionUpdateOptions options);

    Task<IReadOnlyCollection<Subscription>> ListSubscriptionsAsync(SubscriptionListOptions options);

    Task<Subscription> CancelSubscriptionAsync(string subscriptionId, SubscriptionCancelOptions options);

    Task<PaymentMethod> AttachPaymentMethodAsync(string paymentMethodId, PaymentMethodAttachOptions options);
}
