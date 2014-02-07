#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Web;
using System.Web.Security;
using DotNetOpenAuth.AspNet;

namespace Exceptionless.Membership {
    public class AspnetEnvironment : IApplicationEnvironment {
        public void IssueAuthTicket(string username, bool remember) {
            if (HttpContext.Current != null)
                FormsAuthentication.SetAuthCookie(username, remember);
        }

        public void RevokeAuthTicket() {
            FormsAuthentication.SignOut();
        }

        public HttpContextBase AcquireContext() {
            return new HttpContextWrapper(HttpContext.Current);
        }

        public void RequestAuthentication(IAuthenticationClient client, IOpenAuthDataProvider provider, string returnUrl) {
            var securityManager = new OpenAuthSecurityManager(new HttpContextWrapper(HttpContext.Current), client, provider);
            securityManager.RequestAuthentication(returnUrl);
        }

        public string GetOAuthPoviderName() {
            var context = new HttpContextWrapper(HttpContext.Current);
            return OpenAuthSecurityManager.GetProviderName(context);
        }

        public AuthenticationResult VerifyAuthentication(IAuthenticationClient client, IOpenAuthDataProvider provider, string returnUrl) {
            var context = new HttpContextWrapper(HttpContext.Current);
            var securityManager = new OpenAuthSecurityManager(context, client, provider);
            return securityManager.VerifyAuthentication(returnUrl);
        }
    }
}