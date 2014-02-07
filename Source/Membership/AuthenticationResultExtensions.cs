#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using DotNetOpenAuth.AspNet;
using Exceptionless.Models;

namespace Exceptionless.Membership {
    public static class AuthenticationResultExtensions {
        public static OAuthAccount ToOAuthAccount(this AuthenticationResult result) {
            var account = new OAuthAccount {
                Provider = result.Provider,
                ProviderUserId = result.ProviderUserId,
                Username = result.UserName
            };

            foreach (string k in result.ExtraData.Keys)
                account.ExtraData.Add(k, result.ExtraData[k]);

            return account;
        }
    }
}