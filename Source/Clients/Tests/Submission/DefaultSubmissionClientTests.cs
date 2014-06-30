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
        public void SubmitAsync() {
            using (WebApp.Start(Settings.Current.BaseURL, AppBuilder.Build)) {
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
        public void GetSettingsAsync() {
            using (WebApp.Start(Settings.Current.BaseURL, AppBuilder.Build)) {
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