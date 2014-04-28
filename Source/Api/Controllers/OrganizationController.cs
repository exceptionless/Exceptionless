using System;
using System.Web.Http;
using Exceptionless.Api.Hubs;
using Exceptionless.Core;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models.Billing;
using Exceptionless.Models;
using NLog.Fluent;
using Stripe;

namespace Exceptionless.Api.Controllers {
    [RoutePrefix(API_PREFIX + "organization")]
    [Authorize(Roles = AuthorizationRoles.User)]
    public class OrganizationController : ApiController {
        private const string API_PREFIX = "api/v{version:int=1}/";
        private readonly IOrganizationRepository _organizationRepository;
        private readonly BillingManager _billingManager;
        private readonly NotificationSender _notificationSender;

        public OrganizationController(IOrganizationRepository organizationRepository, BillingManager billingManager, NotificationSender notificationSender) {
            _organizationRepository = organizationRepository;
            _billingManager = billingManager;
            _notificationSender = notificationSender;
        }

        [HttpPost]
        [Route("change-plan")]
        public IHttpActionResult ChangePlan(string organizationId, string planId, string stripeToken, string last4) {
            if (String.IsNullOrEmpty(organizationId) || !Request.CanAccessOrganization(organizationId))
                throw new ArgumentException("Invalid organization id.", "organizationId"); // TODO: These should probably throw http Response exceptions.

            if (!Settings.Current.EnableBilling)
                return Ok(new { Success = false, Message = "Plans cannot be changed while billing is disabled." });

            Organization organization = _organizationRepository.GetById(organizationId);
            if (organization == null)
                return Ok(new { Success = false, Message = "Invalid OrganizationId." });

            BillingPlan plan = _billingManager.GetBillingPlan(planId);
            if (plan == null)
                return Ok(new { Success = false, Message = "Invalid PlanId." });

            if (String.Equals(organization.PlanId, plan.Id) && String.Equals(BillingManager.FreePlan.Id, plan.Id))
                return Ok(new { Success = true, Message = "Your plan was not changed as you were already on the free plan." });

            // Only see if they can downgrade a plan if the plans are different.
            string message;
            if (!String.Equals(organization.PlanId, plan.Id) && !_billingManager.CanDownGrade(organization, plan, Request.GetUser(), out message))
                return Ok(new { Success = false, Message = message });

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
                        return Ok(new { Success = false, Message = "Billing information was not set." });

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
                    var update = new StripeCustomerUpdateSubscriptionOptions { PlanId = planId };
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

                _billingManager.ApplyBillingPlan(organization, plan, Request.GetUser());
                _organizationRepository.Update(organization);

                _notificationSender.PlanChanged(organization.Id);
            } catch (Exception e) {
                Log.Error().Exception(e).Message("An error occurred while trying to update your billing plan: " + e.Message).Report(r => r.MarkAsCritical()).Write();
                return Ok(new { Success = false, Message = e.Message });
            }

            return Ok(new { Success = true });
        }
    }
}