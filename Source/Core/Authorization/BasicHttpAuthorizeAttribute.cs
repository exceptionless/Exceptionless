#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Principal;
using System.Web.Http;
using System.Web.Http.Controllers;
using Exceptionless.Core.Extensions;

namespace Exceptionless.Core.Authorization {
    public abstract class BasicHttpAuthorizeAttribute : AuthorizeAttribute {
        protected override bool IsAuthorized(HttpActionContext context) {
            if (context == null)
                throw new ArgumentNullException("context");

            string userName, password;
            if (!context.Request.TryGetLoginInformation(out userName, out password))
                return base.IsAuthorized(context);

            IPrincipal principal;
            if (!TryCreatePrincipal(userName, password, out principal))
                return base.IsAuthorized(context);

            context.Request.SetUserPrincipal(principal);
            CheckForActionOverride(context);

            return IsPrincipalAllowed(principal);
        }

        protected bool IsPrincipalAllowed(IPrincipal principal) {
            return principal.Identity.IsAuthenticated && CheckRoles(principal) && CheckUsers(principal);
        }

        protected void CheckForActionOverride(HttpActionContext context) {
            // Override roles and users if attribute is defined on action.
            ExceptionlessAuthorizeAttribute attr = context.ActionDescriptor.GetCustomAttributes<ExceptionlessAuthorizeAttribute>().FirstOrDefault();
            if (attr == null)
                return;

            Roles = attr.Roles;
            Users = attr.Users;
        }

        protected string AuthDeniedReason { get; set; }

        protected override void HandleUnauthorizedRequest(HttpActionContext actionContext) {
            if (actionContext == null)
                throw new ArgumentNullException("actionContext");

            string authDeniedMessage = "Authorization has been denied for this request.";
            if (!String.IsNullOrEmpty(AuthDeniedReason))
                authDeniedMessage = String.Concat(authDeniedMessage, " (", AuthDeniedReason, ")");
            actionContext.Response = actionContext.ControllerContext.Request.CreateErrorResponse(HttpStatusCode.Unauthorized, authDeniedMessage);
            actionContext.Response.Headers.Add(HttpResponseHeader.WwwAuthenticate.ToString(), ExceptionlessHeaders.Basic);
        }

        protected abstract bool TryCreatePrincipal(string userName, string password, out IPrincipal principal);

        protected bool CheckUsers(IPrincipal principal) {
            string[] users = SplitStrings(Users);
            return users.Length == 0 || users.Any(u => principal.Identity.Name == u);
        }

        protected bool CheckRoles(IPrincipal principal) {
            string[] roles = SplitStrings(Roles);
            return roles.Length == 0 || roles.Any(principal.IsInRole);
        }

        private static string[] SplitStrings(string input) {
            if (String.IsNullOrWhiteSpace(input))
                return new string[0];

            IEnumerable<string> result = input.Split(',').Where(s => !String.IsNullOrWhiteSpace(s.Trim()));

            return result.Select(s => s.Trim()).ToArray();
        }
    }
}