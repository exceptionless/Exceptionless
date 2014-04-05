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
using Exceptionless.Core.Web;
using Exceptionless.Models;
using Exceptionless.Web.Controllers;

namespace Exceptionless.App.Controllers {
    [Authorize]
    [ProjectRequiredActionFilter]
    public class StackController : ExceptionlessController {
        private readonly IStackRepository _repository;

        public StackController(IStackRepository repository) {
            _repository = repository;
        }

        [NotSuspended]
        public ActionResult Index(string id) {
            if (String.IsNullOrEmpty(id))
                return RedirectToAction("Index", "Project");

            Stack stack = _repository.GetById(id);
            if (stack == null || !User.CanAccessOrganization(stack.OrganizationId))
                return HttpNotFound("An error stack with this id was not found.");

            RouteData.SetOrganizationId(stack.OrganizationId);
            return View(stack);
        }

        [ActionName("mark-fixed")]
        public ActionResult MarkFixed(string id) {
            // TODO: We should probably be setting error notifications when the stack is not found.
            if (String.IsNullOrEmpty(id))
                return RedirectToAction("Index", "Project");

            Stack stack = _repository.GetById(id);
            if (stack == null || !User.CanAccessOrganization(stack.OrganizationId))
                return RedirectToAction("Index", "Project");

            stack.DateFixed = DateTime.UtcNow;
            //stack.FixedInVersion = ""; // TODO: Implement this.
            stack.IsRegressed = false;

            // TODO: Add a log entry.
            _repository.Update(stack);

            return RedirectToAction("Index", "Stack", new { id = stack.Id, notification = "mark-fixed" });
        }

        [ActionName("stop-notifications")]
        public ActionResult StopNotifications(string id) {
            // TODO: We should probably be setting error notifications when the stack is not found.
            if (String.IsNullOrEmpty(id))
                return RedirectToAction("Index", "Project");

            Stack stack = _repository.GetById(id);
            if (stack == null || !User.CanAccessOrganization(stack.OrganizationId))
                return RedirectToAction("Index", "Project");

            stack.DisableNotifications = true;
            // TODO: Add a log entry.
            _repository.Update(stack);

            return RedirectToAction("Index", "Stack", new { id = stack.Id, notification = "stop-notifications" });
        }
    }
}