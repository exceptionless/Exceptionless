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

namespace Exceptionless.Membership {
    public class AuthenticationClientData {
        public AuthenticationClientData(IAuthenticationClient authenticationClient, string displayName, IDictionary<string, object> extraData) {
            if (authenticationClient == null)
                throw new ArgumentNullException("authenticationClient");

            AuthenticationClient = authenticationClient;
            DisplayName = displayName;
            ExtraData = extraData;
        }

        public IAuthenticationClient AuthenticationClient { get; private set; }
        public string DisplayName { get; private set; }
        public IDictionary<string, object> ExtraData { get; private set; }
    }
}