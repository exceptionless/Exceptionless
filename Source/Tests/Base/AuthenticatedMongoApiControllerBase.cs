#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Net.Http;
using System.Web.Http;
using System.Web.Mvc;
using Exceptionless.Core;
using Exceptionless.Tests.Utility;

namespace Exceptionless.Tests.Controllers.Base {
    public abstract class AuthenticatedMongoApiControllerBase<TModel, TResponseModel, TRepository> : MongoApiControllerBase<TModel, TResponseModel, TRepository>
        where TModel : class, new()
        where TResponseModel : class
        where TRepository : class, IRepository<TModel> {
        public AuthenticatedMongoApiControllerBase(TRepository repository, bool tearDownOnExit) : base(repository, tearDownOnExit) {
            SetApiKey(TestConstants.ApiKey);
        }

        protected override HttpClient CreateClient(HttpServer server, HttpVerbs verb) {
            HttpClient client = base.CreateClient(server, verb);
            client.AddBasicAuthentication(Username, Password);

            return client;
        }

        protected override void TearDown() {
            SetApiKey(TestConstants.ApiKey);

            base.TearDown();
        }

        protected void SetApiKey(string apiKey) {
            Username = "client";
            Password = apiKey;
        }

        protected void SetValidApiKey() {
            SetApiKey(TestConstants.ApiKey);
        }

        protected void SetInvalidApiKey() {
            SetApiKey(TestConstants.InvalidApiKey);
        }

        protected void SetSuspendedApiKey() {
            SetApiKey(TestConstants.SuspendedApiKey);
        }

        protected void SetUserWithNoRoles() {
            Username = TestConstants.UserEmailWithNoRoles;
            Password = TestConstants.UserPassword;
        }

        protected void SetUserWithAllRoles() {
            Username = TestConstants.UserEmail;
            Password = TestConstants.UserPassword;
        }

        protected string Username { get; set; }
        protected string Password { get; set; }
    }
}