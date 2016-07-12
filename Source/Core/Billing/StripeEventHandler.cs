﻿using System;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Models;
using Foundatio.Logging;
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

        public async Task HandleEventAsync(StripeEvent stripeEvent) {
            switch (stripeEvent.Type) {
                case "customer.subscription.updated": {
                    StripeSubscription stripeSubscription = Mapper<StripeSubscription>.MapFromJson(stripeEvent.Data.Object.ToString());
                    await SubscriptionUpdatedAsync(stripeSubscription).AnyContext();
                    break;
                }
                case "customer.subscription.deleted": {
                    StripeSubscription stripeSubscription = Mapper<StripeSubscription>.MapFromJson(stripeEvent.Data.Object.ToString());
                    await SubscriptionDeletedAsync(stripeSubscription).AnyContext();
                    break;
                }
                case "invoice.payment_succeeded": {
                    StripeInvoice stripeInvoice = Mapper<StripeInvoice>.MapFromJson(stripeEvent.Data.Object.ToString());
                    await InvoicePaymentSucceededAsync(stripeInvoice).AnyContext();
                    break;
                }
                case "invoice.payment_failed": {
                    StripeInvoice stripeInvoice = Mapper<StripeInvoice>.MapFromJson(stripeEvent.Data.Object.ToString());
                    await InvoicePaymentFailedAsync(stripeInvoice).AnyContext();
                    break;
                }
                default: {
                        _logger.Trace("Unhandled stripe webhook called. Type: {0} Id: {1} UserId: {2}", stripeEvent.Type, stripeEvent.Id, stripeEvent.UserId);
                    break;
                }
            }
        }

        private async Task SubscriptionUpdatedAsync(StripeSubscription sub) {
            var org = await _organizationRepository.GetByStripeCustomerIdAsync(sub.CustomerId).AnyContext();
            if (org == null) {
                _logger.Error("Unknown customer id in updated subscription: {0}", sub.CustomerId);
                return;
            }

            _logger.Info("Stripe subscription updated. Customer: {0} Org: {1} Org Name: {2}", sub.CustomerId, org.Id, org.Name);

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
            org.BillingChangeDate = DateTime.Now;
            if (status.Value == BillingStatus.Unpaid || status.Value == BillingStatus.Canceled) {
                org.IsSuspended = true;
                org.SuspensionDate = DateTime.UtcNow;
                org.SuspensionCode = SuspensionCode.Billing;
                org.SuspensionNotes = $"Stripe subscription status changed to \"{status.Value}\".";
                org.SuspendedByUserId = "Stripe";
            } else if (status.Value == BillingStatus.Active || status.Value == BillingStatus.Trialing) {
                org.RemoveSuspension();
            }

            await _organizationRepository.SaveAsync(org, true).AnyContext();
        }

        private async Task SubscriptionDeletedAsync(StripeSubscription sub) {
            var org = await _organizationRepository.GetByStripeCustomerIdAsync(sub.CustomerId).AnyContext();
            if (org == null) {
                _logger.Error("Unknown customer id in deleted subscription: {0}", sub.CustomerId);
                return;
            }

            _logger.Info("Stripe subscription deleted. Customer: {0} Org: {1} Org Name: {2}", sub.CustomerId, org.Id, org.Name);

            org.BillingStatus = BillingStatus.Canceled;
            org.IsSuspended = true;
            org.SuspensionDate = DateTime.UtcNow;
            org.SuspensionCode = SuspensionCode.Billing;
            org.SuspensionNotes = "Stripe subscription deleted.";
            org.SuspendedByUserId = "Stripe";

            org.BillingChangeDate = DateTime.Now;
            await _organizationRepository.SaveAsync(org, true).AnyContext();
        }

        private async Task InvoicePaymentSucceededAsync(StripeInvoice inv) {
            var org = await _organizationRepository.GetByStripeCustomerIdAsync(inv.CustomerId).AnyContext();
            if (org == null) {
                _logger.Error("Unknown customer id in payment succeeded notification: {0}", inv.CustomerId);
                return;
            }

            var user = await _userRepository.GetByIdAsync(org.BillingChangedByUserId).AnyContext();
            if (user == null) {
                _logger.Error("Unable to find billing user: {0}", org.BillingChangedByUserId);
                return;
            }

            _logger.Info("Stripe payment succeeded. Customer: {0} Org: {1} Org Name: {2}", inv.CustomerId, org.Id, org.Name);
        }

        private async Task InvoicePaymentFailedAsync(StripeInvoice inv) {
            var org = await _organizationRepository.GetByStripeCustomerIdAsync(inv.CustomerId).AnyContext();
            if (org == null) {
                _logger.Error("Unknown customer id in payment failed notification: {0}", inv.CustomerId);
                return;
            }

            var user = await _userRepository.GetByIdAsync(org.BillingChangedByUserId).AnyContext();
            if (user == null) {
                _logger.Error("Unable to find billing user: {0}", org.BillingChangedByUserId);
                return;
            }

            _logger.Info("Stripe payment failed. Customer: {0} Org: {1} Org Name: {2} Email: {3}", inv.CustomerId, org.Id, org.Name, user.EmailAddress);
            await _mailer.SendPaymentFailedAsync(user, org).AnyContext();
        }
    }
}
