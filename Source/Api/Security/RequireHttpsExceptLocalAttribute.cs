#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Web.Http.Controllers;

namespace Exceptionless.Api.Security {
    public class RequireHttpsExceptLocalAttribute : RequireHttpsAttribute {
        protected override void HandleNonHttpsRequest(HttpActionContext context) {
            if (context == null)
                throw new ArgumentNullException("context");

            if (HostIsLocal(context.Request.RequestUri.Host))
                return;

            base.HandleNonHttpsRequest(context);
        }

        private bool HostIsLocal(string hostName) {
            return hostName.Contains("localtest.me") || hostName.Equals("localhost", StringComparison.OrdinalIgnoreCase);
        }
    }
}