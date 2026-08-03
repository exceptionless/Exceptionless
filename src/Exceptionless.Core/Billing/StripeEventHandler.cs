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
    private const string STRIPE_USER_ID = "000000000000000000000000";
    private readonly ILogger _logger;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IUserRepository _userRepository;
    private readonly IMailer _mailer;
    private readonly BillingManager _billingManager;
    private readonly BillingPlans _plans;
    private readonly TimeProvider _timeProvider;

    public StripeEventHandler(IOrganizationRepository organizationRepository, IUserRepository userRepository, IMailer mailer,
        BillingManager billingManager, BillingPlans plans, TimeProvider timeProvider, ILogger<StripeEventHandler> logger)
    {
        _logger = logger;
        _organizationRepository = organizationRepository;
        _userRepository = userRepository;
        _mailer = mailer;
        _billingManager = billingManager;
        _plans = plans;
        _timeProvider = timeProvider;
    }

    public async Task HandleEventAsync(Stripe.Event stripeEvent)
    {
        switch (stripeEvent.Type)
        {
            case "customer.subscription.updated":
            {
                await SubscriptionUpdatedAsync((Subscription)stripeEvent.Data.Object, stripeEvent.Id, stripeEvent.Created);
                break;
            }
            case "customer.subscription.deleted":
            {
                await SubscriptionDeletedAsync((Subscription)stripeEvent.Data.Object, stripeEvent.Id, stripeEvent.Created);
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

    private async Task SubscriptionUpdatedAsync(Subscription sub, string eventId, DateTime eventCreatedUtc)
    {
        var organization = await _organizationRepository.GetByStripeCustomerIdAsync(sub.CustomerId);
        if (organization is null)
        {
            _logger.LogError("Unknown customer id in updated subscription. Event: {EventId} Customer: {CustomerId}", eventId, sub.CustomerId);
            return;
        }

        string organizationId = organization.Id;
        await using var billingLock = await _billingManager.AcquireOrganizationLockAsync(organizationId);
        organization = await _organizationRepository.GetByIdAsync(organizationId, o => o.Cache(false));
        if (organization is null || !String.Equals(organization.StripeCustomerId, sub.CustomerId, StringComparison.Ordinal))
        {
            _logger.LogWarning("Stripe customer changed before subscription update could be applied. Event: {EventId} Customer: {CustomerId} Org: {Organization}",
                eventId, sub.CustomerId, organizationId);
            return;
        }

        _logger.LogInformation("Stripe subscription updated. Event: {EventId} Customer: {CustomerId} Org: {Organization} Org Name: {OrganizationName}",
            eventId, sub.CustomerId, organization.Id, organization.Name);

        if (ShouldIgnoreSubscriptionEvent(organization, sub, eventId))
        {
            return;
        }

        if (IsStaleSubscriptionEvent(organization, eventCreatedUtc, out var eventWatermarkUtc))
        {
            _logger.LogInformation("Ignoring stale Stripe subscription update. Event: {EventId} Customer: {CustomerId} Org: {Organization} Event Created: {EventCreatedUtc} Event Watermark: {EventWatermark}",
                eventId, sub.CustomerId, organization.Id, eventCreatedUtc, eventWatermarkUtc);
            return;
        }

        var status = sub.GetBillingStatus();

        if (!status.HasValue)
        {
            _logger.LogWarning("Ignoring Stripe subscription update with unsupported status. Event: {EventId} Customer: {CustomerId} Org: {Organization} Subscription: {SubscriptionId} Status: {Status}",
                eventId, sub.CustomerId, organization.Id, sub.Id, sub.Status);
            return;
        }

        organization.StripeSubscriptionEventDate = eventCreatedUtc;
        if (status.Value == BillingStatus.Canceled)
            organization.StripeSubscriptionId = null;
        else
            organization.StripeSubscriptionId ??= sub.Id;

        if (status.Value == organization.BillingStatus)
        {
            await _organizationRepository.SaveAsync(organization, o => o.ImmediateConsistency().Cache().Originals());
            return;
        }

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        organization.BillingStatus = status.Value;
        organization.BillingChangeDate = utcNow;
        if (status.Value == BillingStatus.Unpaid || status.Value == BillingStatus.Canceled)
        {
            organization.IsSuspended = true;
            organization.SuspensionDate = utcNow;
            organization.SuspensionCode = SuspensionCode.Billing;
            organization.SuspensionNotes = $"Stripe subscription status changed to \"{status.Value}\".";
            organization.SuspendedByUserId = STRIPE_USER_ID;
        }
        else if (status.Value == BillingStatus.Active || status.Value == BillingStatus.Trialing)
        {
            organization.RemoveSuspension();
        }

        await _organizationRepository.SaveAsync(organization, o => o.ImmediateConsistency().Cache().Originals());
    }

    private async Task SubscriptionDeletedAsync(Subscription sub, string eventId, DateTime eventCreatedUtc)
    {
        var organization = await _organizationRepository.GetByStripeCustomerIdAsync(sub.CustomerId);
        if (organization is null)
        {
            _logger.LogError("Unknown customer id in deleted subscription. Event: {EventId} Customer: {CustomerId}", eventId, sub.CustomerId);
            return;
        }

        string organizationId = organization.Id;
        await using var billingLock = await _billingManager.AcquireOrganizationLockAsync(organizationId);
        organization = await _organizationRepository.GetByIdAsync(organizationId, o => o.Cache(false));
        if (organization is null || !String.Equals(organization.StripeCustomerId, sub.CustomerId, StringComparison.Ordinal))
        {
            _logger.LogWarning("Stripe customer changed before subscription deletion could be applied. Event: {EventId} Customer: {CustomerId} Org: {Organization}",
                eventId, sub.CustomerId, organizationId);
            return;
        }

        _logger.LogInformation("Stripe subscription deleted. Event: {EventId} Customer: {CustomerId} Org: {Organization} Org Name: {OrganizationName}",
            eventId, sub.CustomerId, organization.Id, organization.Name);

        if (ShouldIgnoreSubscriptionEvent(organization, sub, eventId))
        {
            return;
        }

        if (IsStaleSubscriptionEvent(organization, eventCreatedUtc, out var eventWatermarkUtc))
        {
            _logger.LogInformation("Ignoring stale Stripe subscription deletion. Event: {EventId} Customer: {CustomerId} Org: {Organization} Event Created: {EventCreatedUtc} Event Watermark: {EventWatermark}",
                eventId, sub.CustomerId, organization.Id, eventCreatedUtc, eventWatermarkUtc);
            return;
        }

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        organization.StripeSubscriptionEventDate = eventCreatedUtc;
        organization.StripeSubscriptionId = null;
        organization.BillingChangeDate = utcNow;
        organization.BillingStatus = BillingStatus.Canceled;
        organization.IsSuspended = true;
        organization.SuspensionDate = utcNow;
        organization.SuspensionCode = SuspensionCode.Billing;
        organization.SuspensionNotes = "Stripe subscription deleted.";
        organization.SuspendedByUserId = STRIPE_USER_ID;

        await _organizationRepository.SaveAsync(organization, o => o.ImmediateConsistency().Cache().Originals());
    }

    private static bool IsStaleSubscriptionEvent(Organization organization, DateTime eventCreatedUtc, out DateTime eventWatermarkUtc)
    {
        eventWatermarkUtc = organization.StripeSubscriptionEventDate ?? organization.BillingChangeDate;
        if (eventWatermarkUtc <= DateTime.MinValue)
            return false;

        if (eventCreatedUtc < eventWatermarkUtc)
            return true;

        return eventCreatedUtc == eventWatermarkUtc &&
            organization.StripeSubscriptionEventDate.HasValue &&
            organization.StripeSubscriptionId is null &&
            organization.BillingStatus == BillingStatus.Canceled;
    }

    private bool ShouldIgnoreSubscriptionEvent(Organization organization, Subscription subscription, string eventId)
    {
        if (String.Equals(organization.PlanId, _plans.FreePlan.Id, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Ignoring Stripe subscription event for free organization. Event: {EventId} Customer: {CustomerId} Org: {Organization} Subscription: {SubscriptionId}",
                eventId, subscription.CustomerId, organization.Id, subscription.Id);
            return true;
        }

        if (!String.IsNullOrEmpty(organization.StripeSubscriptionId) &&
            !String.Equals(organization.StripeSubscriptionId, subscription.Id, StringComparison.Ordinal))
        {
            _logger.LogInformation("Ignoring Stripe subscription event for obsolete subscription. Event: {EventId} Customer: {CustomerId} Org: {Organization} Subscription: {SubscriptionId} Current Subscription: {CurrentSubscriptionId}",
                eventId, subscription.CustomerId, organization.Id, subscription.Id, organization.StripeSubscriptionId);
            return true;
        }

        return false;
    }

    private async Task InvoicePaymentSucceededAsync(Invoice invoice)
    {
        var organization = await _organizationRepository.GetByStripeCustomerIdAsync(invoice.CustomerId);
        if (organization is null)
        {
            _logger.LogError("Unknown customer id in payment succeeded notification: {CustomerId}", invoice.CustomerId);
            return;
        }

        if (String.IsNullOrEmpty(organization.BillingChangedByUserId))
        {
            _logger.LogError("No billing user set for organization: {OrganizationId}", organization.Id);
            return;
        }

        var user = await _userRepository.GetByIdAsync(organization.BillingChangedByUserId);
        if (user is null)
        {
            _logger.LogError("Unable to find billing user: {User}", organization.BillingChangedByUserId);
            return;
        }

        _logger.LogInformation("Stripe payment succeeded. Customer: {CustomerId} Org: {Organization} Org Name: {OrganizationName}", invoice.CustomerId, organization.Id, organization.Name);
    }

    private async Task InvoicePaymentFailedAsync(Invoice invoice)
    {
        var organization = await _organizationRepository.GetByStripeCustomerIdAsync(invoice.CustomerId);
        if (organization is null)
        {
            _logger.LogError("Unknown customer id in payment failed notification: {CustomerId}", invoice.CustomerId);
            return;
        }

        if (String.IsNullOrEmpty(organization.BillingChangedByUserId))
        {
            _logger.LogError("No billing user set for organization: {OrganizationId}", organization.Id);
            return;
        }

        var user = await _userRepository.GetByIdAsync(organization.BillingChangedByUserId);
        if (user is null)
        {
            _logger.LogError("Unable to find billing user: {UserId}", organization.BillingChangedByUserId);
            return;
        }

        _logger.LogInformation("Stripe payment failed. Customer: {CustomerId} Org: {Organization} Org Name: {OrganizationName} Email: {EmailAddress}", invoice.CustomerId, organization.Id, organization.Name, user.EmailAddress);
        await _mailer.SendOrganizationPaymentFailedAsync(user, organization);
    }
}
