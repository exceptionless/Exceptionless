#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Web.Mvc;
using Exceptionless.Core;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models.Billing;
using Exceptionless.Models;
using Exceptionless.Web.Hubs;
using Exceptionless.Web.Models.Organization;
using NLog.Fluent;
using Stripe;

namespace Exceptionless.Web.Controllers {
    [Authorize]
    public class OrganizationController : ExceptionlessController {
        private readonly IOrganizationRepository _repository;
        private readonly BillingManager _billingManager;
        private readonly NotificationSender _notificationSender;

        public OrganizationController(IOrganizationRepository repository, BillingManager billingManager, NotificationSender notificationSender) {
            _repository = repository;
            _billingManager = billingManager;
            _notificationSender = notificationSender;
        }

        [HttpGet]
        public ActionResult List() {
            return View();
        }

        [HttpGet]
        public ActionResult Manage(string id) {
            if (String.IsNullOrEmpty(id) || !User.CanAccessOrganization(id))
                throw new ArgumentException("Invalid organization id.", "id"); // TODO: These should probably throw http Response exceptions.

            Organization organization = _repository.GetById(id);
            if (String.IsNullOrEmpty(id) || organization == null)
                return RedirectToAction("List");

            return View(organization);
        }

        [HttpPost]
        public JsonResult ChangePlan(string organizationId, string planId, string stripeToken, string last4) {
            if (String.IsNullOrEmpty(organizationId) || !User.CanAccessOrganization(organizationId))
                throw new ArgumentException("Invalid organization id.", "organizationId"); // TODO: These should probably throw http Response exceptions.

            if (!Settings.Current.EnableBilling)
                return Json(new { Success = false, Message = "Plans cannot be changed while billing is disabled." });

            Organization organization = _repository.GetById(organizationId);
            if (organization == null)
                return Json(new { Success = false, Message = "Invalid OrganizationId." });

            BillingPlan plan = _billingManager.GetBillingPlan(planId);
            if (plan == null)
                return Json(new { Success = false, Message = "Invalid PlanId." });

            if (String.Equals(organization.PlanId, plan.Id) && String.Equals(BillingManager.FreePlan.Id, plan.Id))
                return Json(new { Success = true, Message = "Your plan was not changed as you were already on the free plan." });

            // Only see if they can downgrade a plan if the plans are different.
            string message;
            if (!String.Equals(organization.PlanId, plan.Id) && !_billingManager.CanDownGrade(organization, plan, User.UserEntity, out message))
                return Json(new { Success = false, Message = message });

            var customerService = new StripeCustomerService();

            try {
                // If they are on a paid plan and then downgrade to a free plan then cancel their stripe subscription.
                if (!String.Equals(organization.PlanId, BillingManager.FreePlan.Id) && String.Equals(plan.Id, BillingManager.FreePlan.Id)) {
                    if (!String.IsNullOrEmpty(organization.StripeCustomerId))
                        customerService.CancelSubscription(organization.StripeCustomerId);

                    organization.BillingStatus = BillingStatus.Trialing;
                    organization.RemoveSuspension();
                } else if (String.IsNullOrEmpty(organization.StripeCustomerId)) {
                    if (String.IsNullOrEmpty(stripeToken))
                        return Json(new { Success = false, Message = "Billing information was not set." });

                    organization.SubscribeDate = DateTime.Now;

                    StripeCustomer customer = customerService.Create(new StripeCustomerCreateOptions {
                        TokenId = stripeToken,
                        PlanId = planId,
                        Description = organization.Name
                    });

                    organization.BillingStatus = BillingStatus.Active;
                    organization.RemoveSuspension();
                    organization.StripeCustomerId = customer.Id;
                    if (customer.StripeCardList.StripeCards.Count > 0)
                        organization.CardLast4 = customer.StripeCardList.StripeCards[0].Last4;
                } else {
                    var update = new StripeCustomerUpdateSubscriptionOptions {
                        PlanId = planId
                    };
                    bool cardUpdated = false;

                    if (!String.IsNullOrEmpty(stripeToken)) {
                        update.TokenId = stripeToken;
                        cardUpdated = true;
                    }

                    customerService.UpdateSubscription(organization.StripeCustomerId, update);
                    if (cardUpdated)
                        organization.CardLast4 = last4;

                    organization.BillingStatus = BillingStatus.Active;
                    organization.RemoveSuspension();
                }

                _billingManager.ApplyBillingPlan(organization, plan, User.UserEntity);
                _repository.Update(organization);

                _notificationSender.PlanChanged(organization.Id);
            } catch (Exception e) {
                Log.Error().Exception(e).Message("An error occurred while trying to update your billing plan: " + e.Message).Report().Write();
                return Json(new { Success = false, Message = e.Message });
            }

            return Json(new { Success = true });
        }

        [HttpGet]
        public ActionResult Payment(string id) {
            if (String.IsNullOrEmpty(id))
                return HttpNotFound();

            if (!id.StartsWith("in_"))
                id = "in_" + id;

            var invoiceService = new StripeInvoiceService();
            StripeInvoice invoice = invoiceService.Get(id);
            if (invoice == null)
                return HttpNotFound();

            Organization org = _repository.GetByStripeCustomerId(invoice.CustomerId);
            if (org == null)
                return HttpNotFound();

            if (!User.CanAccessOrganization(org.Id))
                return HttpNotFound();

            return View(new InvoiceModel { Invoice = invoice, Organization = org });
        }

        [HttpGet]
        public ActionResult Suspended(string id = null) {
            if (String.IsNullOrEmpty(id) || !User.CanAccessOrganization(id))
                return View();

            Organization organization = _repository.GetByIdCached(id);
            if (organization.IsSuspended)
                return View(organization);

            return RedirectToAction("Index", "Project");
        }
    }
}