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
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Web;

namespace Exceptionless.Web.Controllers {
    [ViewBagDefaults]
    [Core.Web.RequireHttps]
    public class ExceptionlessController : Controller {
        public void SetAttentionAlert(string message) {
            TempData[Alerts.ATTENTION] = message;
        }

        public void SetAttentionAlert(string title, string message) {
            TempData[String.Concat(Alerts.ATTENTION, "-", Alerts.TITLE)] = title;
            TempData[Alerts.ATTENTION] = message;
        }

        public void SetSuccessAlert(string message) {
            TempData[Alerts.SUCCESS] = message;
        }

        public void SetSuccessAlert(string title, string message) {
            TempData[String.Concat(Alerts.SUCCESS, "-", Alerts.TITLE)] = title;
            TempData[Alerts.SUCCESS] = message;
        }

        public void SetInformationAlert(string message) {
            TempData[Alerts.INFORMATION] = message;
        }

        public void SetInformationAlert(string title, string message) {
            TempData[String.Concat(Alerts.INFORMATION, "-", Alerts.TITLE)] = title;
            TempData[Alerts.INFORMATION] = message;
        }

        public void SetErrorAlert(string message) {
            TempData[Alerts.ERROR] = message;
        }

        public void SetErrorAlert(string title, string message) {
            TempData[String.Concat(Alerts.ERROR, "-", Alerts.TITLE)] = title;
            TempData[Alerts.ERROR] = message;
        }
    }

    public static class Alerts {
        public const string SUCCESS = "success";
        public const string ATTENTION = "attention";
        public const string ERROR = "error";
        public const string INFORMATION = "info";
        public const string TITLE = "title";

        public static string[] ALL {
            get {
                return new[] {
                    SUCCESS,
                    ATTENTION,
                    INFORMATION,
                    ERROR
                };
            }
        }
    }
}