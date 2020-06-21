using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Models;
using Foundatio.Repositories;
using Foundatio.Utility;
using Microsoft.Extensions.Logging;
using Stripe;

namespace Exceptionless.Core.Billing {
    public class StripeEventHandler {
        private readonly ILogger _logger;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IUserRepository _userRepository;
        private readonly IMailer _mailer;

        public StripeEventHandler(IOrganizationRepository organizationRepository, IUserRepository userRepository, IMailer mailer, ILogger<StripeEventHandler> logger) {
            _logger = logger;
            _organizationRepository = organizationRepository;
            _userRepository = userRepository;
            _mailer = mailer;
        }

        public async Task HandleEventAsync(Stripe.Event stripeEvent) {
            switch (stripeEvent.Type) {
                case "customer.subscription.updated": {
                    await SubscriptionUpdatedAsync((Subscription)stripeEvent.Data.Object).AnyContext();
                    break;
                }
                case "customer.subscription.deleted": {
                    await SubscriptionDeletedAsync((Subscription)stripeEvent.Data.Object).AnyContext();
                    break;
                }
                case "invoice.payment_succeeded": {
                    await InvoicePaymentSucceededAsync((Invoice)stripeEvent.Data.Object).AnyContext();
                    break;
                }
                case "invoice.payment_failed": {
                    await InvoicePaymentFailedAsync((Invoice)stripeEvent.Data.Object).AnyContext();
                    break;
                }
                default: {
                    _logger.LogTrace("Unhandled stripe webhook called. Type: {Type} Id: {Id} Account: {Account}", stripeEvent.Type, stripeEvent.Id, stripeEvent.Account);
                    break;
                }
            }
        }

        private async Task SubscriptionUpdatedAsync(Subscription sub) {
            var org = await _organizationRepository.GetByStripeCustomerIdAsync(sub.CustomerId).AnyContext();
            if (org == null) {
                _logger.LogError("Unknown customer id in updated subscription: {CustomerId}", sub.CustomerId);
                return;
            }

            _logger.LogInformation("Stripe subscription updated. Customer: {CustomerId} Org: {organization} Org Name: {OrganizationName}", sub.CustomerId, org.Id, org.Name);

            BillingStatus? status = null;
            switch (sub.Status) {
                case "trialing": {
                    status = BillingStatus.Trialing;
                    break;
                }
                case "active": {
                    status = BillingStatus.Active;
                    break;
                }
                case "past_due": {
                    status = BillingStatus.PastDue;
                    break;
                }
                case "canceled": {
                    status = BillingStatus.Canceled;
                    break;
                }
                case "unpaid": {
                    status = BillingStatus.Unpaid;
                    break;
                }
            }

            if (!status.HasValue || status.Value == org.BillingStatus)
                return;

            org.BillingStatus = status.Value;
            org.BillingChangeDate = SystemClock.UtcNow;
            if (status.Value == BillingStatus.Unpaid || status.Value == BillingStatus.Canceled) {
                org.IsSuspended = true;
                org.SuspensionDate = SystemClock.UtcNow;
                org.SuspensionCode = SuspensionCode.Billing;
                org.SuspensionNotes = $"Stripe subscription status changed to \"{status.Value}\".";
                org.SuspendedByUserId = "Stripe";
            } else if (status.Value == BillingStatus.Active || status.Value == BillingStatus.Trialing) {
                org.RemoveSuspension();
            }

            await _organizationRepository.SaveAsync(org, o => o.Cache()).AnyContext();
        }

        private async Task SubscriptionDeletedAsync(Subscription sub) {
            var org = await _organizationRepository.GetByStripeCustomerIdAsync(sub.CustomerId).AnyContext();
            if (org == null) {
                _logger.LogError("Unknown customer id in deleted subscription: {CustomerId}", sub.CustomerId);
                return;
            }

            _logger.LogInformation("Stripe subscription deleted. Customer: {CustomerId} Org: {organization} Org Name: {OrganizationName}", sub.CustomerId, org.Id, org.Name);

            org.BillingStatus = BillingStatus.Canceled;
            org.IsSuspended = true;
            org.SuspensionDate = SystemClock.UtcNow;
            org.SuspensionCode = SuspensionCode.Billing;
            org.SuspensionNotes = "Stripe subscription deleted.";
            org.SuspendedByUserId = "Stripe";

            org.BillingChangeDate = SystemClock.UtcNow;
            await _organizationRepository.SaveAsync(org, o => o.Cache()).AnyContext();
        }

        private async Task InvoicePaymentSucceededAsync(Invoice inv) {
            var org = await _organizationRepository.GetByStripeCustomerIdAsync(inv.CustomerId).AnyContext();
            if (org == null) {
                _logger.LogError("Unknown customer id in payment succeeded notification: {CustomerId}", inv.CustomerId);
                return;
            }

            var user = await _userRepository.GetByIdAsync(org.BillingChangedByUserId).AnyContext();
            if (user == null) {
                _logger.LogError("Unable to find billing user: {user}", org.BillingChangedByUserId);
                return;
            }

            _logger.LogInformation("Stripe payment succeeded. Customer: {CustomerId} Org: {organization} Org Name: {OrganizationName}", inv.CustomerId, org.Id, org.Name);
        }

        private async Task InvoicePaymentFailedAsync(Invoice inv) {
            var org = await _organizationRepository.GetByStripeCustomerIdAsync(inv.CustomerId).AnyContext();
            if (org == null) {
                _logger.LogError("Unknown customer id in payment failed notification: {CustomerId}", inv.CustomerId);
                return;
            }

            var user = await _userRepository.GetByIdAsync(org.BillingChangedByUserId).AnyContext();
            if (user == null) {
                _logger.LogError("Unable to find billing user: {0}", org.BillingChangedByUserId);
                return;
            }

            _logger.LogInformation("Stripe payment failed. Customer: {CustomerId} Org: {organization} Org Name: {OrganizationName} Email: {EmailAddress}", inv.CustomerId, org.Id, org.Name, user.EmailAddress);
            await _mailer.SendOrganizationPaymentFailedAsync(user, org).AnyContext();
        }
    }
}
