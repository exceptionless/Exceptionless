using System;
using System.Collections.Specialized;
using System.Threading.Tasks;
using OAuth2.Client;
using OAuth2.Models;

namespace Exceptionless.Web.Extensions {
    public static class OAuth2Extensions {
        public static Task<UserInfo> GetUserInfoAsync(this OAuth2Client client, string code, string redirectUri) {
            return client.GetUserInfoAsync(new NameValueCollection { { "code", code }, { "redirect_uri", redirectUri } });
        }

        public static string GetFullName(this UserInfo user) {
            string name = (user.FirstName + " " + user.LastName).Trim();
            return !String.IsNullOrEmpty(name) ? name : user.Email;
        }
    }
}