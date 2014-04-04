#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Linq;

namespace Exceptionless.Models {
    public class OAuthAccount : IEquatable<OAuthAccount> {
        public OAuthAccount() {
            ExtraData = new SettingsDictionary();
        }

        public string Provider { get; set; }
        public string ProviderUserId { get; set; }
        public string Username { get; set; }

        public SettingsDictionary ExtraData { get; private set; }

        public bool Equals(OAuthAccount other) {
            if (ReferenceEquals(null, other))
                return false;
            if (ReferenceEquals(this, other))
                return true;
            return other.Provider.Equals(Provider) && other.ProviderUserId.Equals(ProviderUserId);
        }

        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj))
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            if (obj.GetType() != typeof(OAuthAccount))
                return false;
            return Equals((OAuthAccount)obj);
        }

        public override int GetHashCode() {
            unchecked {
                int hash = 2153;
                if (Provider != null)
                    hash = hash * 9929 + Provider.GetHashCode();
                if (ProviderUserId != null)
                    hash = hash * 9929 + ProviderUserId.GetHashCode();
                return hash;
            }
        }

        public string EmailAddress() {
            if (!String.IsNullOrEmpty(Username) && Username.Contains("@"))
                return Username;

            foreach (var kvp in ExtraData) {
                if ((String.Equals(kvp.Key, "email") || String.Equals(kvp.Key, "account_email") || String.Equals(kvp.Key, "preferred_email") || String.Equals(kvp.Key, "personal_email")) && !String.IsNullOrEmpty(kvp.Value))
                    return kvp.Value;
            }

            return null;
        }

        public string FullName() {
            foreach (var kvp in ExtraData.Where(kvp => String.Equals(kvp.Key, "name") && !String.IsNullOrEmpty(kvp.Value)))
                return kvp.Value;

            return !String.IsNullOrEmpty(Username) && Username.Contains(" ") ? Username : null;
        }
    }
}