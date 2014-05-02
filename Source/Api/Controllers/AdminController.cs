using System;
using System.Web.Http;
using Exceptionless.Api.Hubs;
using Exceptionless.Core;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Controllers;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models.Billing;
using Exceptionless.Models;

namespace Exceptionless.Api.Controllers {
    [RoutePrefix(API_PREFIX + "admin")]
    [Authorize(Roles = AuthorizationRoles.GlobalAdmin)]
    public class AdminController : ExceptionlessApiController {
        private readonly IOrganizationRepository _repository;
        private readonly BillingManager _billingManager;
        private readonly NotificationSender _notificationSender;

        public AdminController(IOrganizationRepository repository, BillingManager billingManager, NotificationSender notificationSender) {
            _repository = repository;
            _billingManager = billingManager;
            _notificationSender = notificationSender;
        }

        [HttpPost]
        [Route("change-plan")]
        public IHttpActionResult ChangePlan(string organizationId, string planId) {
            if (String.IsNullOrEmpty(organizationId) || !User.CanAccessOrganization(organizationId))
                return Ok(new { Success = false, Message = "Invalid Organization Id." });

            Organization organization = _repository.GetById(organizationId);
            if (organization == null)
                return Ok(new { Success = false, Message = "Invalid Organization Id." });

            BillingPlan plan = _billingManager.GetBillingPlan(planId);
            if (plan == null)
                return Ok(new { Success = false, Message = "Invalid PlanId." });

            organization.BillingStatus = !String.Equals(plan.Id, BillingManager.FreePlan.Id) ? BillingStatus.Active : BillingStatus.Trialing;
            organization.RemoveSuspension();
            _billingManager.ApplyBillingPlan(organization, plan, User.GetUser(), false);

            _repository.Update(organization);
            _notificationSender.PlanChanged(organization.Id);

            return Ok(new { Success = true });
        }
    }
}