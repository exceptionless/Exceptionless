#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Threading;
using System.Web.Mvc;

namespace Exceptionless.SampleMvc.Controllers {
    public class SomeModel {
        public string Test { get; set; }
        public string Blah { get; set; }
    }

    [HandleError(View = "CustomError", ExceptionType = typeof(ArgumentException))]
    public class HomeController : Controller {
        public ActionResult Index() {
            return View();
        }

        [HttpPost]
        public ViewResult Index(SomeModel model) {
            throw new ApplicationException("Error on form submit.");
        }

        [HttpGet]
        public ViewResult Error() {
            return View("Error");
        }

        [HttpGet]
        public ViewResult CustomError() {
            return View("CustomError");
        }

        [HttpPost]
        public JsonResult AjaxMethod(SomeModel model) {
            throw new ApplicationException("Error on AJAX call.");
        }

        [HttpPost]
        public ActionResult Error(string identifier, string emailaddress, string description) {
            if (String.IsNullOrEmpty(identifier))
                return RedirectToAction("Index", "Home");

            if (String.IsNullOrEmpty(emailaddress) && String.IsNullOrEmpty(description))
                return RedirectToAction("Index", "Home");

            ExceptionlessClient.Default.UpdateUserEmailAndDescription(identifier, emailaddress, description);

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
        public ActionResult CustomBoom() {
            throw new ArgumentException("Boom!");
        }

        [HttpGet]
        public ActionResult Boom25() {
            for (int i = 0; i < 25; i++) {
                try {
                    throw new ApplicationException("Boom!");
                } catch (Exception ex) {
                   ex.ToExceptionless()
                        .SetUserIdentity("some@email.com")
                        .AddRecentTraceLogEntries()
                        .AddRequestInfo()
                        .AddObject(new {
                            Blah = "Hello"
                        }, name: "Hello")
                        .AddTags("SomeTag", "AnotherTag")
                        .MarkAsCritical()
                        .Submit();

                    ex.ToExceptionless().Submit();

                    ex.ToExceptionless()
                        .SetUserIdentity("some@email.com")
                        .SetUserDescription("some@email.com", "Some description.")
                        .AddRecentTraceLogEntries()
                        .AddRequestInfo()
                        .AddObject(new {
                            Blah = "Hello"
                        }, name: "Hello", excludedPropertyNames: new[] { "Blah" })
                        .AddTags("SomeTag", "AnotherTag")
                        .MarkAsCritical()
                        .Submit();
                }

                Thread.Sleep(1500);
            }

            return RedirectToAction("Index");
        }

        public ActionResult CreateRequestValidationException(string value) {
            return RedirectToAction("Index");
        }
    }
}