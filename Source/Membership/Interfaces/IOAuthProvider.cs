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
using DotNetOpenAuth.AspNet;
using Exceptionless.Models;

namespace Exceptionless.Membership {
    public interface IOAuthProvider {
        bool OAuthLogin(OAuthAccount account, bool remember);

        void RequestOAuthAuthentication(string provider, string returnUrl);

        AuthenticationResult VerifyOAuthAuthentication(string action);

        AuthenticationClientData GetOAuthClientData(string provider);

        ICollection<AuthenticationClientData> RegisteredClientData { get; }

        IEnumerable<OAuthAccount> GetOAuthAccountsFromEmailAddress(string emailAddress);

        bool DeleteOAuthAccount(string provider, string providerUserId);

        User CreateOAuthAccount(OAuthAccount account, User user);
    }
}