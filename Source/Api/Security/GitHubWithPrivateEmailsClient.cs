using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using NLog.Fluent;
using OAuth2.Client;
using OAuth2.Client.Impl;
using OAuth2.Configuration;
using OAuth2.Infrastructure;
using OAuth2.Models;
using RestSharp;

namespace Exceptionless.Api.Security {
    public class GitHubWithPrivateEmailsClient : GitHubClient {
        private readonly IRequestFactory _factory;

        public GitHubWithPrivateEmailsClient(IRequestFactory factory, IClientConfiguration configuration) : base(factory, configuration) {
            _factory = factory;
        }

        protected override UserInfo GetUserInfo() {
            var userInfo = base.GetUserInfo();
            if (userInfo == null)
                return null;

            if (!String.IsNullOrEmpty(userInfo.Email))
                return userInfo;

            var client = _factory.CreateClient(UserEmailServiceEndpoint);
            client.Authenticator = new OAuth2UriQueryParameterAuthenticator(AccessToken);
            var request = _factory.CreateRequest(UserEmailServiceEndpoint);

            BeforeGetUserInfo(new BeforeAfterRequestArgs {
                Client = client,
                Request = request,
                Configuration = Configuration
            });

            var response = client.ExecuteAndVerify(request);
            var userEmails = ParseEmailAddresses(response.Content);
            userInfo.Email = userEmails.First(u => u.Primary).Email;
            return userInfo;
        }

        protected override UserInfo ParseUserInfo(string content) {
            try {
                return base.ParseUserInfo(content);
            } catch (Exception ex) {
                Log.Error().Exception(ex).Critical().Tag("GitHub").Property("Content", content).Write();
                throw;
            }
        }

        protected virtual List<UserEmails> ParseEmailAddresses(string content) {
            try {
                return JsonConvert.DeserializeObject<List<UserEmails>>(content);
            } catch (Exception ex) {
                Log.Error().Exception(ex).Critical().Message("Error while parsing email addresses. Message: {0}", content).Tag("GitHub").Property("Content", content).Write();
                throw;
            }
        }

        protected virtual Endpoint UserEmailServiceEndpoint {
            get { return new Endpoint { BaseUri = "https://api.github.com/", Resource = "/user/emails" }; }
        }

        protected class UserEmails {
            public string Email { get; set; }
            public bool Primary { get; set; }
            public bool Verified { get; set; }
        }
    }
}