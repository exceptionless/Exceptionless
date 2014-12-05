using System;
using System.Collections.Specialized;
using OAuth2.Client;
using OAuth2.Models;

namespace Exceptionless.Api.Extensions {
    public static class OAuth2Extensions {
        public static UserInfo GetUserInfo(this OAuth2Client client, string code) {
            return client.GetUserInfo(new NameValueCollection { { "code", code} });
        }

        public static string GetFullName(this UserInfo user) {
            string name = (user.FirstName + " " + user.LastName).Trim();
            return !String.IsNullOrEmpty(name) ? name : user.Email;
        }
    }
}