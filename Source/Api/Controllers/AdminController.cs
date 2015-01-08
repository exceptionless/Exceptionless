using System;
using System.Web.Http;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Messaging;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Models;

namespace Exceptionless.Api.Controllers {
    [RoutePrefix(API_PREFIX + "/admin")]
    [Authorize(Roles = AuthorizationRoles.GlobalAdmin)]
    public class AdminController : ExceptionlessApiController {
        private readonly IOrganizationRepository _repository;
        private readonly BillingManager _billingManager;
        private readonly IMessagePublisher _messagePublisher;

        public AdminController(IOrganizationRepository repository, BillingManager billingManager, IMessagePublisher messagePublisher) {
            _repository = repository;
            _billingManager = billingManager;
            _messagePublisher = messagePublisher;
        }

        [HttpPost]
        [Route("change-plan")]
        public IHttpActionResult ChangePlan(string organizationId, string planId) {
            if (String.IsNullOrEmpty(organizationId) || !CanAccessOrganization(organizationId))
                return Ok(new { Success = false, Message = "Invalid Organization Id." });

            var organization = _repository.GetById(organizationId);
            if (organization == null)
                return Ok(new { Success = false, Message = "Invalid Organization Id." });

            var plan = BillingManager.GetBillingPlan(planId);
            if (plan == null)
                return Ok(new { Success = false, Message = "Invalid PlanId." });

            organization.BillingStatus = !String.Equals(plan.Id, BillingManager.FreePlan.Id) ? BillingStatus.Active : BillingStatus.Trialing;
            organization.RemoveSuspension();
            BillingManager.ApplyBillingPlan(organization, plan, ExceptionlessUser, false);

            _repository.Save(organization);
            _messagePublisher.Publish(new PlanChanged {
                OrganizationId = organization.Id
            });

            return Ok(new { Success = true });
        }

        [HttpPost]
        [Route("set-bonus")]
        public IHttpActionResult ChangePlan(string organizationId, int bonusEvents, DateTime? expires = null) {
            if (String.IsNullOrEmpty(organizationId) || !CanAccessOrganization(organizationId))
                return Ok(new { Success = false, Message = "Invalid Organization Id." });

            var organization = _repository.GetById(organizationId);
            if (organization == null)
                return Ok(new { Success = false, Message = "Invalid Organization Id." });

            organization.BonusEventsPerMonth = bonusEvents;
            organization.BonusExpiration = expires;
            _repository.Save(organization);

            return Ok(new { Success = true });
        }
    }
}