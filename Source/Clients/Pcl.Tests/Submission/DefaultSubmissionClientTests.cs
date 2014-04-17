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
using Exceptionless.Submission;
using Microsoft.Owin.Hosting;
using Xunit;

namespace Pcl.Tests.Submission {
    public class DefaultSubmissionClientTests {
        [Fact]
        public void SubmitAsync() {
            using (WebApp.Start(Settings.Current.BaseURL, AppBuilder.Build)) {
                var events = new List<Event> { new Event { Message = "Testing" } };
                var configuration = new Configuration {
                    ServerUrl = String.Concat(Settings.Current.BaseURL, "/api/v1/"),
                    ApiKey = "e3d51ea621464280bbcb79c11fd6483e"
                };

                var client = new DefaultSubmissionClient();
                var response = client.SubmitAsync(events, configuration).Result;
                Assert.True(response.Success);
                Assert.NotEqual(-1, response.SettingsVersion);
                Assert.Null(response.ErrorMessage);
            }
        }

        [Fact]
        public void GetSettingsAsync() {
            using (WebApp.Start(Settings.Current.BaseURL, AppBuilder.Build)) {
                var configuration = new Configuration {
                    ServerUrl = String.Concat(Settings.Current.BaseURL, "/api/v1/"),
                    ApiKey = "e3d51ea621464280bbcb79c11fd6483e"
                };

                var client = new DefaultSubmissionClient();
                var response = client.GetSettingsAsync(configuration).Result;
                Assert.True(response.Success);
                Assert.NotEqual(-1, response.SettingsVersion);
                Assert.NotNull(response.Settings);
                Assert.Null(response.ErrorMessage);
            }
        }
    }
}