#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using Exceptionless.Core;

namespace Exceptionless.App.Controllers {
    public class HomeController : Controller {
        private readonly ICacheClient _cacheClient;
        private readonly IRedisClientsManager _clientsManager;
        private readonly NotificationSender _notificationSender;
        private readonly IUserRepository _userRepository;

        public HomeController(ICacheClient cacheClient, IRedisClientsManager clientsManager, NotificationSender notificationSender, IUserRepository userRepository) {
            _cacheClient = cacheClient;
            _clientsManager = clientsManager;
            _notificationSender = notificationSender;
            _userRepository = userRepository;
        }

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

        public ActionResult Status() {
            try {
                if (_cacheClient.Get<string>("__PING__") != null)
                    return new HttpStatusCodeResult(HttpStatusCode.ServiceUnavailable, "Cache Not Working");
            } catch (Exception ex) {
                return new HttpStatusCodeResult(HttpStatusCode.ServiceUnavailable, "Cache Not Working: " + ex.Message);
            }

            try {
                if (!GlobalApplication.IsDbUpToDate())
                    return new HttpStatusCodeResult(HttpStatusCode.ServiceUnavailable, "Mongo DB Schema Outdated");

                var user = _userRepository.All().Take(1).FirstOrDefault();
            } catch (Exception ex) {
                return new HttpStatusCodeResult(HttpStatusCode.ServiceUnavailable, "Mongo Not Working: " + ex.Message);
            }

            if (!_notificationSender.IsListening())
                return new HttpStatusCodeResult(HttpStatusCode.ServiceUnavailable, "Ping Not Received");

            return new ContentResult { Content = "All Systems Check" };
        }
    }
}