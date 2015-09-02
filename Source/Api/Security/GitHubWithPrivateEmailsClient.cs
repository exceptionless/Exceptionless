using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
                var cnt = JObject.Parse(content);
                var names = (cnt["name"].SafeGet(x => x.Value<string>()) ?? string.Empty).Split(new[] { " " }, StringSplitOptions.RemoveEmptyEntries).ToList();
                const string avatarUriTemplate = "{0}&s={1}";
                var avatarUri = cnt["avatar_url"].Value<string>();
                var result = new UserInfo {
                    Email = cnt["email"].SafeGet(x => x.Value<string>()),
                    ProviderName = this.Name,
                    Id = cnt["id"].Value<string>(),
                    FirstName = names.Count > 0 ? names.First() : cnt["login"].Value<string>(),
                    LastName = names.Count > 1 ? names.Last() : string.Empty,
                    AvatarUri = {
                        Small = !string.IsNullOrWhiteSpace(avatarUri) ? string.Format(avatarUriTemplate, avatarUri, 36) : string.Empty,
                        Normal = avatarUri,
                        Large = !string.IsNullOrWhiteSpace(avatarUri) ? string.Format(avatarUriTemplate, avatarUri, 300) : string.Empty
                    }
                };

                return result;
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