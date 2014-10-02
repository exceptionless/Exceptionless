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

namespace Exceptionless.App.Utility {
    public class MvcRequireHttpsAttribute : RequireHttpsAttribute {
        public override void OnAuthorization(AuthorizationContext context) {
            if (context == null)
                throw new ArgumentNullException("context");

            if (Settings.Current.EnableSSL && !context.HttpContext.Request.IsSecureConnection)
                HandleNonHttpsRequest(context);
        }
    }
}