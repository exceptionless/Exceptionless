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
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models.Billing;
using Exceptionless.Models;
using Exceptionless.Web.Hubs;

namespace Exceptionless.Web.Controllers {
    [Authorize(Roles = AuthorizationRoles.GlobalAdmin)]
    public class AdminController : ExceptionlessController {
        private readonly IOrganizationRepository _repository;
        private readonly BillingManager _billingManager;
        private readonly NotificationSender _notificationSender;

        public AdminController(IOrganizationRepository repository, BillingManager billingManager, NotificationSender notificationSender) {
            _repository = repository;
            _billingManager = billingManager;
            _notificationSender = notificationSender;
        }

        public ActionResult Index() {
            return View();
        }

        [HttpPost]
        public JsonResult ChangePlan(string organizationId, string planId) {
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

            return Json(new { Success = true });
        }
    }
}