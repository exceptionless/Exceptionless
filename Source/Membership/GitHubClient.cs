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
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Web;
using DotNetOpenAuth.AspNet.Clients;
using DotNetOpenAuth.Messaging;
using Newtonsoft.Json;

namespace Exceptionless.Membership {
    public class GitHubClient : OAuth2Client {
        private const string AUTHORIZATION_ENDPOINT = "https://github.com/login/oauth/authorize";
        private const string TOKEN_ENDPOINT = "https://github.com/login/oauth/access_token";
        private const string USER_ENDPOINT = "https://api.github.com/user";

        private readonly string _appId;
        private readonly string _appSecret;

        public GitHubClient(string appId, string appSecret) : base("github") {
            if (string.IsNullOrWhiteSpace(appId))
                throw new ArgumentNullException("appId");

            if (string.IsNullOrWhiteSpace(appSecret))
                throw new ArgumentNullException("appSecret");

            _appId = appId;
            _appSecret = appSecret;
        }

        protected override Uri GetServiceLoginUrl(Uri returnUrl) {
            var builder = new UriBuilder(AUTHORIZATION_ENDPOINT);

            builder.AppendQueryArgument("client_id", _appId);
            builder.AppendQueryArgument("redirect_uri", returnUrl.AbsoluteUri);
            builder.AppendQueryArgument("scope", "user:email");

            return builder.Uri;
        }

        protected override IDictionary<string, string> GetUserData(string accessToken) {
            var request = (HttpWebRequest)WebRequest.Create(USER_ENDPOINT + "?access_token=" + accessToken);
            request.UserAgent = GetType().FullName;
            using (WebResponse response = request.GetResponse()) {
                using (Stream responseStream = response.GetResponseStream()) {
                    using (var reader = new StreamReader(responseStream)) {
                        string json = reader.ReadToEnd();
                        return JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                    }
                }
            }
        }

        protected override string QueryAccessToken(Uri returnUrl, string authorizationCode) {
            var builder = new UriBuilder(TOKEN_ENDPOINT);

            builder.AppendQueryArgument("client_id", _appId);
            builder.AppendQueryArgument("redirect_uri", returnUrl.AbsoluteUri);
            builder.AppendQueryArgument("client_secret", _appSecret);
            builder.AppendQueryArgument("code", authorizationCode);

            using (var client = new WebClient()) {
                string data = client.DownloadString(builder.Uri);

                if (string.IsNullOrEmpty(data))
                    return null;

                NameValueCollection parsedQueryString = HttpUtility.ParseQueryString(data);
                return parsedQueryString["access_token"];
            }
        }
    }
}