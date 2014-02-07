#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Net;
using System.Net.Http;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;

namespace Exceptionless.Core.Web {
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class InvalidModelStateFilterAttribute : ActionFilterAttribute {
        public override void OnActionExecuting(HttpActionContext context) {
            if (context == null)
                throw new ArgumentNullException("context");

            if (!context.ModelState.IsValid)
                context.Response = context.Request.CreateErrorResponse(HttpStatusCode.BadRequest, context.ModelState);
        }
    }
}