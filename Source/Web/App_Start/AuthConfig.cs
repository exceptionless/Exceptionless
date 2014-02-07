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
using DotNetOpenAuth.AspNet.Clients;
using Exceptionless.Core;
using Exceptionless.Membership;

namespace Exceptionless.Web {
    public static class AuthConfig {
        public static void RegisterAuth() {
            if (!String.IsNullOrEmpty(Settings.Current.MicrosoftAppId)) {
                MembershipProvider.RegisterClient(
                    new MicrosoftClientWithEmail(Settings.Current.MicrosoftAppId, Settings.Current.MicrosoftAppSecret),
                    "Microsoft", new Dictionary<string, object>());
            }

            MembershipProvider.RegisterClient(
                new GoogleOpenIdClient(),
                "Google", new Dictionary<string, object>());

            if (!String.IsNullOrEmpty(Settings.Current.FacebookAppId)) {
                MembershipProvider.RegisterClient(
                    new FacebookClient(Settings.Current.FacebookAppId, Settings.Current.FacebookAppSecret),
                    "Facebook", new Dictionary<string, object>());
            }

            if (!String.IsNullOrEmpty(Settings.Current.GitHubAppId)) {
                MembershipProvider.RegisterClient(
                    new GitHubClient(Settings.Current.GitHubAppId, Settings.Current.GitHubAppSecret),
                    "GitHub", new Dictionary<string, object>());
            }
        }
    }
}