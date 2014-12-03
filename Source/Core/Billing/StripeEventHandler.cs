#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Repositories;
using Exceptionless.Models;
using NLog.Fluent;
using Stripe;

namespace Exceptionless.Core.Billing {
    public class StripeEventHandler {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IUserRepository _userRepository;
        private readonly IMailer _mailer;

        public StripeEventHandler(IOrganizationRepository organizationRepository, IUserRepository userRepository, IMailer mailer) {
            _organizationRepository = organizationRepository;
            _userRepository = userRepository;
            _mailer = mailer;
        }

        public void HandleEvent(StripeEvent stripeEvent) {
            switch (stripeEvent.Type) {
                case "customer.subscription.updated": {
                    StripeSubscription stripeSubscription = Mapper<StripeSubscription>.MapFromJson(stripeEvent.Data.Object.ToString());
                    SubscriptionUpdated(stripeSubscription);
                    break;
                }
                case "customer.subscription.deleted": {
                    StripeSubscription stripeSubscription = Mapper<StripeSubscription>.MapFromJson(stripeEvent.Data.Object.ToString());
                    SubscriptionDeleted(stripeSubscription);
                    break;
                }
                case "invoice.payment_succeeded": {
                    StripeInvoice stripeInvoice = Mapper<StripeInvoice>.MapFromJson(stripeEvent.Data.Object.ToString());
                    InvoicePaymentSucceeded(stripeInvoice);
                    break;
                }
                case "invoice.payment_failed": {
                    StripeInvoice stripeInvoice = Mapper<StripeInvoice>.MapFromJson(stripeEvent.Data.Object.ToString());
                    InvoicePaymentFailed(stripeInvoice);
                    break;
                }
                default: {
                    Log.Trace().Message("Unhandled stripe webhook called. Type: {0} Id: {1} UserId: {2}", stripeEvent.Type, stripeEvent.Id, stripeEvent.UserId).Write();
                    break;
                }
            }
        }

        private void SubscriptionUpdated(StripeSubscription sub) {
            var org = _organizationRepository.GetByStripeCustomerId(sub.CustomerId);
            if (org == null) {
                Log.Error().Message("Unknown customer id in updated subscription: {0}", sub.CustomerId).Write();
                return;
            }

            Log.Info().Message("Stripe subscription updated. Customer: {0} Org: {1} Org Name: {2}", sub.CustomerId, org.Id, org.Name).Write();

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
                org.SuspensionDate = DateTime.Now;
                org.SuspensionCode = SuspensionCode.Billing;
                org.SuspensionNotes = String.Format("Stripe subscription status changed to \"{0}\".", status.Value);
                org.SuspendedByUserId = "Stripe";
            } else if (status.Value == BillingStatus.Active || status.Value == BillingStatus.Trialing) {
                org.RemoveSuspension();
            }
            _organizationRepository.Save(org);
        }

        private void SubscriptionDeleted(StripeSubscription sub) {
            var org = _organizationRepository.GetByStripeCustomerId(sub.CustomerId);
            if (org == null) {
                Log.Error().Message("Unknown customer id in deleted subscription: {0}", sub.CustomerId).Write();
                return;
            }

            Log.Info().Message("Stripe subscription deleted. Customer: {0} Org: {1} Org Name: {2}", sub.CustomerId, org.Id, org.Name).Write();

            org.BillingStatus = BillingStatus.Canceled;
            org.IsSuspended = true;
            org.SuspensionDate = DateTime.Now;
            org.SuspensionCode = SuspensionCode.Billing;
            org.SuspensionNotes = String.Format("Stripe subscription deleted.");
            org.SuspendedByUserId = "Stripe";

            org.BillingChangeDate = DateTime.Now;
            _organizationRepository.Save(org);
        }

        private void InvoicePaymentSucceeded(StripeInvoice inv) {
            var org = _organizationRepository.GetByStripeCustomerId(inv.CustomerId);
            if (org == null) {
                Log.Error().Message("Unknown customer id in payment failed notification: {0}", inv.CustomerId).Write();
                return;
            }

            var user = _userRepository.GetById(org.BillingChangedByUserId);
            if (user == null) {
                Log.Error().Message("Unable to find billing user: {0}", org.BillingChangedByUserId).Write();
                return;
            }

            Log.Info().Message("Stripe payment succeeded. Customer: {0} Org: {1} Org Name: {2}", inv.CustomerId, org.Id, org.Name).Write();

            // TODO: Should we send an email here?
            //_mailer.SendPaymentSuccessAsync(user, org);
        }

        private void InvoicePaymentFailed(StripeInvoice inv) {
            var org = _organizationRepository.GetByStripeCustomerId(inv.CustomerId);
            if (org == null) {
                Log.Error().Message("Unknown customer id in payment failed notification: {0}", inv.CustomerId).Write();
                return;
            }

            var user = _userRepository.GetById(org.BillingChangedByUserId);
            if (user == null) {
                Log.Error().Message("Unable to find billing user: {0}", org.BillingChangedByUserId).Write();
                return;
            }

            Log.Info().Message("Stripe payment failed. Customer: {0} Org: {1} Org Name: {2} Email: {3}", inv.CustomerId, org.Id, org.Name, user.EmailAddress).Write();

            _mailer.SendPaymentFailed(user, org);
        }
    }
}