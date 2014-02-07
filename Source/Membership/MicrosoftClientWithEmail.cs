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
using System.IO;
using System.Net;
using CodeSmith.Core.Extensions;
using DotNetOpenAuth.AspNet.Clients;
using Newtonsoft.Json;

namespace Exceptionless.Membership {
    public class MicrosoftClientWithEmail : MicrosoftClient {
        private const string AUTHORIZATION_ENDPOINT = "https://login.live.com/oauth20_authorize.srf";

        public MicrosoftClientWithEmail(string appId, string appSecret) : base(appId, appSecret) {}

        protected MicrosoftClientWithEmail(string providerName, string appId, string appSecret) : base(providerName, appId, appSecret) {}

        protected override Uri GetServiceLoginUrl(Uri returnUrl) {
            var builder = new UriBuilder(AUTHORIZATION_ENDPOINT);
            builder.AppendQueryArgs(
                                    new Dictionary<string, string> {
                                        { "client_id", AppId },
                                        { "scope", "wl.basic, wl.emails" },
                                        { "response_type", "code" },
                                        { "redirect_uri", returnUrl.AbsoluteUri },
                                    });

            return builder.Uri;
        }

        protected override IDictionary<string, string> GetUserData(string accessToken) {
            MicrosoftClientUserDataWithEmail graph;
            WebRequest request = WebRequest.Create("https://apis.live.net/v5.0/me?access_token=" + UriExtensions.EscapeUriDataStringRfc3986(accessToken));
            using (WebResponse response = request.GetResponse()) {
                using (Stream responseStream = response.GetResponseStream()) {
                    string json = new StreamReader(responseStream).ReadToEnd();
                    graph = JsonConvert.DeserializeObject<MicrosoftClientUserDataWithEmail>(json);
                }
            }

            var userData = new Dictionary<string, string>();
            userData.AddItemIfNotEmpty("id", graph.Id);
            userData.AddItemIfNotEmpty("username", graph.Name);
            userData.AddItemIfNotEmpty("name", graph.Name);
            userData.AddItemIfNotEmpty("link", graph.Link == null ? null : graph.Link.AbsoluteUri);
            userData.AddItemIfNotEmpty("gender", graph.Gender);
            userData.AddItemIfNotEmpty("firstname", graph.FirstName);
            userData.AddItemIfNotEmpty("lastname", graph.LastName);
            userData.AddItemIfNotEmpty("preferred_email", graph.Emails.Preferred);
            userData.AddItemIfNotEmpty("account_email", graph.Emails.Account);
            userData.AddItemIfNotEmpty("personal_email", graph.Emails.Personal);
            userData.AddItemIfNotEmpty("business_email", graph.Emails.Business);
            return userData;
        }
    }

    public class MicrosoftClientUserDataWithEmail : MicrosoftClientUserData {
        public MicrosoftClientUserDataWithEmail() {
            Emails = new EmailInfo();
        }

        [JsonProperty("emails")]
        public EmailInfo Emails { get; set; }
    }

    public class EmailInfo {
        [JsonProperty("preferred")]
        public string Preferred { get; set; }

        [JsonProperty("account")]
        public string Account { get; set; }

        [JsonProperty("personal")]
        public string Personal { get; set; }

        [JsonProperty("business")]
        public string Business { get; set; }
    }
}