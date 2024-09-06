using System;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Foundatio.Repositories;
using Microsoft.Extensions.Logging;
using Stripe;

namespace Exceptionless.Core.Billing;

public class StripeEventHandler
{
    private readonly ILogger _logger;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IUserRepository _userRepository;
    private readonly IMailer _mailer;
    private readonly TimeProvider _timeProvider;

    public StripeEventHandler(IOrganizationRepository organizationRepository, IUserRepository userRepository, IMailer mailer,
        TimeProvider timeProvider, ILogger<StripeEventHandler> logger)
    {
        _logger = logger;
        _organizationRepository = organizationRepository;
        _userRepository = userRepository;
        _mailer = mailer;
        _timeProvider = timeProvider;
    }

    public async Task HandleEventAsync(Stripe.Event stripeEvent)
    {
        switch (stripeEvent.Type)
        {
            case "customer.subscription.updated":
                {
                    await SubscriptionUpdatedAsync((Subscription)stripeEvent.Data.Object);
                    break;
                }
            case "customer.subscription.deleted":
                {
                    await SubscriptionDeletedAsync((Subscription)stripeEvent.Data.Object);
                    break;
                }
            case "invoice.payment_succeeded":
                {
                    await InvoicePaymentSucceededAsync((Invoice)stripeEvent.Data.Object);
                    break;
                }
            case "invoice.payment_failed":
                {
                    await InvoicePaymentFailedAsync((Invoice)stripeEvent.Data.Object);
                    break;
                }
            default:
                {
                    _logger.LogTrace("Unhandled stripe webhook called. Type: {Type} Id: {Id} Account: {Account}", stripeEvent.Type, stripeEvent.Id, stripeEvent.Account);
                    break;
                }
        }
    }

    private async Task SubscriptionUpdatedAsync(Subscription sub)
    {
        var org = await _organizationRepository.GetByStripeCustomerIdAsync(sub.CustomerId);
        if (org is null)
        {
            _logger.LogError("Unknown customer id in updated subscription: {CustomerId}", sub.CustomerId);
            return;
        }

        _logger.LogInformation("Stripe subscription updated. Customer: {CustomerId} Org: {Organization} Org Name: {OrganizationName}", sub.CustomerId, org.Id, org.Name);

        BillingStatus? status = null;
        switch (sub.Status)
        {
            case "trialing":
                {
                    status = BillingStatus.Trialing;
                    break;
                }
            case "active":
                {
                    status = BillingStatus.Active;
                    break;
                }
            case "past_due":
                {
                    status = BillingStatus.PastDue;
                    break;
                }
            case "canceled":
                {
                    status = BillingStatus.Canceled;
                    break;
                }
            case "unpaid":
                {
                    status = BillingStatus.Unpaid;
                    break;
                }
        }

        if (!status.HasValue || status.Value == org.BillingStatus)
            return;

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        org.BillingStatus = status.Value;
        org.BillingChangeDate = utcNow;
        if (status.Value == BillingStatus.Unpaid || status.Value == BillingStatus.Canceled)
        {
            org.IsSuspended = true;
            org.SuspensionDate = utcNow;
            org.SuspensionCode = SuspensionCode.Billing;
            org.SuspensionNotes = $"Stripe subscription status changed to \"{status.Value}\".";
            org.SuspendedByUserId = "Stripe";
        }
        else if (status.Value == BillingStatus.Active || status.Value == BillingStatus.Trialing)
        {
            org.RemoveSuspension();
        }

        await _organizationRepository.SaveAsync(org, o => o.Cache().Originals());
    }

    private async Task SubscriptionDeletedAsync(Subscription sub)
    {
        var org = await _organizationRepository.GetByStripeCustomerIdAsync(sub.CustomerId);
        if (org is null)
        {
            _logger.LogError("Unknown customer id in deleted subscription: {CustomerId}", sub.CustomerId);
            return;
        }

        _logger.LogInformation("Stripe subscription deleted. Customer: {CustomerId} Org: {Organization} Org Name: {OrganizationName}", sub.CustomerId, org.Id, org.Name);

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        org.BillingChangeDate = utcNow;
        org.BillingStatus = BillingStatus.Canceled;
        org.IsSuspended = true;
        org.SuspensionDate = utcNow;
        org.SuspensionCode = SuspensionCode.Billing;
        org.SuspensionNotes = "Stripe subscription deleted.";
        org.SuspendedByUserId = "Stripe";

        await _organizationRepository.SaveAsync(org, o => o.Cache().Originals());
    }

    private async Task InvoicePaymentSucceededAsync(Invoice invoice)
    {
        var org = await _organizationRepository.GetByStripeCustomerIdAsync(invoice.CustomerId);
        if (org is null)
        {
            _logger.LogError("Unknown customer id in payment succeeded notification: {CustomerId}", invoice.CustomerId);
            return;
        }

        var user = await _userRepository.GetByIdAsync(org.BillingChangedByUserId);
        if (user is null)
        {
            _logger.LogError("Unable to find billing user: {User}", org.BillingChangedByUserId);
            return;
        }

        _logger.LogInformation("Stripe payment succeeded. Customer: {CustomerId} Org: {Organization} Org Name: {OrganizationName}", invoice.CustomerId, org.Id, org.Name);
    }

    private async Task InvoicePaymentFailedAsync(Invoice invoice)
    {
        var org = await _organizationRepository.GetByStripeCustomerIdAsync(invoice.CustomerId);
        if (org is null)
        {
            _logger.LogError("Unknown customer id in payment failed notification: {CustomerId}", invoice.CustomerId);
            return;
        }

        var user = await _userRepository.GetByIdAsync(org.BillingChangedByUserId);
        if (user is null)
        {
            _logger.LogError("Unable to find billing user: {UserId}", org.BillingChangedByUserId);
            return;
        }

        _logger.LogInformation("Stripe payment failed. Customer: {CustomerId} Org: {Organization} Org Name: {OrganizationName} Email: {EmailAddress}", invoice.CustomerId, org.Id, org.Name, user.EmailAddress);
        await _mailer.SendOrganizationPaymentFailedAsync(user, org);
    }
}
