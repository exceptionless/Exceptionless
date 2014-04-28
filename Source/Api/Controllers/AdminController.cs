using System;
using System.Web.Http;
using Exceptionless.Api.Hubs;
using Exceptionless.Core;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models.Billing;
using Exceptionless.Models;

namespace Exceptionless.Api.Controllers {
    [RoutePrefix(API_PREFIX + "admin")]
    [Authorize(Roles = AuthorizationRoles.GlobalAdmin)]
    public class AdminController : ApiController {
        private const string API_PREFIX = "api/v{version:int=1}/";
        private readonly IOrganizationRepository _repository;
        private readonly BillingManager _billingManager;
        private readonly NotificationSender _notificationSender;

        public AdminController(IOrganizationRepository repository, BillingManager billingManager, NotificationSender notificationSender) {
            _repository = repository;
            _billingManager = billingManager;
            _notificationSender = notificationSender;
        }

        [HttpPost]
        public IHttpActionResult ChangePlan(string organizationId, string planId) {
            if (String.IsNullOrEmpty(organizationId) || !User.CanAccessOrganization(organizationId))
                throw new ArgumentException("Invalid organization id.", "organizationId"); // TODO: These should probably throw http Response exceptions.

            Organization organization = _repository.GetById(organizationId);
            if (organization == null)
                return Json(new { Success = false, Message = "Invalid OrganizationId." });

            BillingPlan plan = _billingManager.GetBillingPlan(planId);
            if (plan == null)
                return Json(new { Success = false, Message = "Invalid PlanId." });

            organization.BillingStatus = !String.Equals(plan.Id, BillingManager.FreePlan.Id) ? BillingStatus.Active : BillingStatus.Trialing;
            organization.RemoveSuspension();
            _billingManager.ApplyBillingPlan(organization, plan, User.UserEntity, false);

            _repository.Update(organization);
            _notificationSender.PlanChanged(organization.Id);

            return Ok(new { Success = true });
        }
    }
}