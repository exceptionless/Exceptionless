#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Net.Http;
using System.Web.Http.Filters;
using Exceptionless.Core.Authorization;

namespace Exceptionless.Core.Web {
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class ConfigurationResponseFilterAttribute : ActionFilterAttribute {
        public override void OnActionExecuted(HttpActionExecutedContext context) {
            if (context == null)
                throw new ArgumentNullException("context");

            if (context.Response == null)
                return;

            var principal = context.Request.GetRequestContext().Principal as ExceptionlessPrincipal;
            if (principal == null || principal.Project == null)
                return;

            // add the current configuration version to the response headers so the client will know if it should update its config.
            context.Response.Headers.Add(ExceptionlessHeaders.ConfigurationVersion, principal.Project.Configuration.Version.ToString());
        }
    }
}