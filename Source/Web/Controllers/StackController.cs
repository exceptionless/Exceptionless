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

namespace Exceptionless.Web.Controllers {
    [Authorize]
    [ProjectRequiredActionFilter]
    public class StackController : ExceptionlessController {
        private readonly IErrorStackRepository _repository;

        public StackController(IErrorStackRepository repository) {
            _repository = repository;
        }

        [NotSuspended]
        public ActionResult Index(string id) {
            if (String.IsNullOrEmpty(id))
                return RedirectToAction("Index", "Project");

            ErrorStack errorStack = _repository.GetById(id);
            if (errorStack == null || !User.CanAccessOrganization(errorStack.OrganizationId))
                return HttpNotFound("An error stack with this id was not found.");

            RouteData.SetOrganizationId(errorStack.OrganizationId);
            return View(errorStack);
        }

        [ActionName("mark-fixed")]
        public ActionResult MarkFixed(string id) {
            // TODO: We should probably be setting error notifications when the stack is not found.
            if (String.IsNullOrEmpty(id))
                return RedirectToAction("Index", "Project");

            ErrorStack stack = _repository.GetById(id);
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

            ErrorStack errorStack = _repository.GetById(id);
            if (errorStack == null || !User.CanAccessOrganization(errorStack.OrganizationId))
                return RedirectToAction("Index", "Project");

            errorStack.DisableNotifications = true;
            // TODO: Add a log entry.
            _repository.Update(errorStack);

            return RedirectToAction("Index", "Stack", new { id = errorStack.Id, notification = "stop-notifications" });
        }
    }
}