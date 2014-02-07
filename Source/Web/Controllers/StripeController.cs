#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.IO;
using System.Net;
using System.Web.Mvc;
using Exceptionless.Core.Billing;
using NLog.Fluent;
using Stripe;

namespace Exceptionless.Web.Controllers {
    public class StripeController : Controller {
        private readonly StripeEventHandler _stripeEventHandler;

        public StripeController(StripeEventHandler stripeEventHandler) {
            _stripeEventHandler = stripeEventHandler;
        }

        [HttpPost]
        public ActionResult Index() {
            Stream req = Request.InputStream;
            req.Seek(0, SeekOrigin.Begin);

            string json = new StreamReader(req).ReadToEnd();
            StripeEvent stripeEvent = null;
            try {
                stripeEvent = StripeEventUtility.ParseEvent(json);
            } catch (Exception ex) {
                Log.Error().Exception(ex).Message("Unable to parse incoming event.").Report(b => b.AddObject(json, "Event")).Write();
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "Unable to parse incoming event");
            }

            if (stripeEvent == null) {
                Log.Warn().Message("Null stripe event.").Write();
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "Incoming event empty");
            }

            _stripeEventHandler.HandleEvent(stripeEvent);

            return new HttpStatusCodeResult(HttpStatusCode.OK);
        }
    }
}