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

namespace Exceptionless.App.Controllers {
    public class HomeController : Controller {
        public ActionResult Index() {
            return RedirectToAction("Index", "Project");
        }

        public ActionResult Dashboard() {
            return RedirectToAction("Index", "Project");
        }

        [HttpGet]
        public ViewResult Oops() {
            return View("Error");
        }

        [HttpPost]
        public ActionResult Oops(string identifier, string emailaddress, string description) {
            if (String.IsNullOrEmpty(identifier))
                return RedirectToAction("Index", "Home");

            if (String.IsNullOrEmpty(emailaddress) && String.IsNullOrEmpty(description))
                return RedirectToAction("Index", "Home");

            ExceptionlessClient.Current.UpdateUserEmailAndDescription(identifier, emailaddress, description);

            return View("ErrorSubmitted");
        }

        public ActionResult NotFound(string url = null) {
            ViewBag.Url = url;
            return View();
        }

        [HttpGet]
        public ActionResult Boom() {
            throw new ApplicationException("Boom!");
        }

        [HttpGet]
        public ActionResult Maintenance() {
            return View();
        }
    }
}