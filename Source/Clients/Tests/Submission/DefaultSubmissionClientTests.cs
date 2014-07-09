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
using Exceptionless;
using Exceptionless.Api;
using Exceptionless.Core;
using Exceptionless.Models;
using Exceptionless.Models.Data;
using Exceptionless.Serializer;
using Exceptionless.Submission;
using Microsoft.Owin.Hosting;
using Xunit;

namespace Client.Tests.Submission {
    public class DefaultSubmissionClientTests {
        private ExceptionlessClient GetClient() {
            return new ExceptionlessClient(c => {
                c.ApiKey = "e3d51ea621464280bbcb79c11fd6483e";
                c.ServerUrl = Settings.Current.BaseURL;
                c.EnableSSL = false;
                c.UseDebugLogger();
            });
        }

        [Fact]
        public void PostEvents() {
            using (WebApp.Start(Settings.Current.BaseURL, app => AppBuilder.BuildWithContainer(app, AppBuilder.CreateContainer(), false))) {
                var events = new List<Event> { new Event { Message = "Testing" } };
                var configuration = GetClient().Configuration;
                var serializer = new DefaultJsonSerializer();

                var client = new DefaultSubmissionClient();
                var response = client.PostEvents(events, configuration, serializer);
                Assert.True(response.Success, response.Message);
                Assert.Null(response.Message);
            }
        }

        [Fact]
        public void PostUserDescription() {
            using (WebApp.Start(Settings.Current.BaseURL, app => AppBuilder.BuildWithContainer(app, AppBuilder.CreateContainer(), false))) {
                const string referenceId = "fda94ff32921425ebb08b73df1d1d34c";
                var events = new List<Event> { new Event { Message = "Testing", ReferenceId = referenceId } };
                var configuration = GetClient().Configuration;
                var serializer = new DefaultJsonSerializer();

                var client = new DefaultSubmissionClient();
                var response = client.PostEvents(events, configuration, serializer);
                Assert.True(response.Success, response.Message);
                Assert.Null(response.Message);
                response = client.PostUserDescription(referenceId, new UserDescription { EmailAddress = "test@noreply.com", Description = "Some description." }, configuration, serializer);
                Assert.True(response.Success, response.Message);
                Assert.Null(response.Message);
            }
        }

        [Fact]
        public void GetSettings() {
            using (WebApp.Start(Settings.Current.BaseURL, app => AppBuilder.BuildWithContainer(app, AppBuilder.CreateContainer(), false))) {
                var configuration = GetClient().Configuration;
                var serializer = new DefaultJsonSerializer();

                var client = new DefaultSubmissionClient();
                var response = client.GetSettings(configuration, serializer);
                Assert.True(response.Success, response.Message);
                Assert.NotEqual(-1, response.SettingsVersion);
                Assert.NotNull(response.Settings);
                Assert.Null(response.Message);
            }
        }
    }
}