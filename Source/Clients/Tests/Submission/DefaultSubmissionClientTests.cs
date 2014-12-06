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
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless;
using Exceptionless.Api;
using Exceptionless.Core;
using Exceptionless.Core.AppStats;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.Models;
using Exceptionless.Models.Data;
using Exceptionless.Serializer;
using Exceptionless.Submission;
using Microsoft.Owin.Hosting;
using Nest;
using SimpleInjector;
using Xunit;
using Exceptionless.Core.Queues;
using Exceptionless.Core.Queues.Models;

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
            var container = AppBuilder.CreateContainer();
            using (WebApp.Start(Settings.Current.BaseURL, app => AppBuilder.BuildWithContainer(app, container, false))) {
                EnsureSampleData(container);

                var events = new List<Event> { new Event { Message = "Testing" } };
                var configuration = GetClient().Configuration;
                var serializer = new DefaultJsonSerializer();

                var client = new DefaultSubmissionClient();
                var response = client.PostEvents(events, configuration, serializer);
                Assert.True(response.Success, response.Message);
                Assert.Null(response.Message);
            }
        }

        [Fact(Skip="Flakey, need a better way to test this")]
        public async Task PostUserDescription() {
            var container = AppBuilder.CreateContainer();
            using (WebApp.Start(Settings.Current.BaseURL, app => AppBuilder.BuildWithContainer(app, container, false))) {
                var repository = container.GetInstance<IEventRepository>();
                repository.RemoveAll();

                const string referenceId = "fda94ff32921425ebb08b73df1d1d34c";
                const string badReferenceId = "zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz";

                var statsCounter = container.GetInstance<IAppStatsClient>() as InMemoryAppStatsClient;
                var descQueue = container.GetInstance<IQueue<EventUserDescription>>() as InMemoryQueue<EventUserDescription>;

                Assert.NotNull(statsCounter);

                EnsureSampleData(container);

                var events = new List<Event> { new Event { Message = "Testing", ReferenceId = referenceId } };
                var configuration = GetClient().Configuration;
                var serializer = new DefaultJsonSerializer();

                var client = new DefaultSubmissionClient();
                var description = new UserDescription { EmailAddress = "test@noreply.com", Description = "Some description." };
                Debug.WriteLine("Before Submit Description");
                statsCounter.DisplayStats();
                Assert.True(statsCounter.WaitForCounter(StatNames.EventsUserDescriptionErrors, work: () => {
                    var response = client.PostUserDescription(referenceId, description, configuration, serializer);
                    Debug.WriteLine("After Submit Description");
                    Assert.True(response.Success, response.Message);
                    Assert.Null(response.Message);
                }));
                statsCounter.DisplayStats();
                Debug.WriteLine(descQueue.GetQueueCount());

                Debug.WriteLine("Before Post Event");
                Assert.True(statsCounter.WaitForCounter(StatNames.EventsProcessed, work: () => {
                    var response = client.PostEvents(events, configuration, serializer);
                    Debug.WriteLine("After Post Event");
                    Assert.True(response.Success, response.Message);
                    Assert.Null(response.Message);
                }));
                statsCounter.DisplayStats();
                if (statsCounter.GetCount(StatNames.EventsUserDescriptionProcessed) == 0)
                    Assert.True(statsCounter.WaitForCounter(StatNames.EventsUserDescriptionProcessed));

                container.GetInstance<IElasticClient>().Refresh();
                var ev = repository.GetByReferenceId("537650f3b77efe23a47914f4", referenceId).FirstOrDefault();
                Assert.NotNull(ev);
                Assert.NotNull(ev.GetUserDescription());
                Assert.Equal(description.ToJson(), ev.GetUserDescription().ToJson());

                Assert.InRange(statsCounter.GetCount(StatNames.EventsUserDescriptionErrors), 1, 5);
                Assert.True(statsCounter.WaitForCounter(StatNames.EventsUserDescriptionErrors, work: () => {
                    var response = client.PostUserDescription(badReferenceId, description, configuration, serializer);
                    Assert.True(response.Success, response.Message);
                    Assert.Null(response.Message);
                }));
                statsCounter.DisplayStats();

                Assert.InRange(statsCounter.GetCount(StatNames.EventsUserDescriptionErrors), 2, 10);
            }
        }

        [Fact]
        public void GetSettings() {
            var container = AppBuilder.CreateContainer();
            using (WebApp.Start(Settings.Current.BaseURL, app => AppBuilder.BuildWithContainer(app, container, false))) {
                EnsureSampleData(container);

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

        private void EnsureSampleData(Container container) {
            var dataHelper = container.GetInstance<DataHelper>();
            var userRepository = container.GetInstance<IUserRepository>();
            var user = userRepository.GetByEmailAddress("test@test.com");
            if (user == null)
                user = userRepository.Add(new User { FullName = "Test User", EmailAddress = "test@test.com", VerifyEmailAddressToken = Guid.NewGuid().ToString(), VerifyEmailAddressTokenExpiration = DateTime.MaxValue });
            dataHelper.CreateSampleOrganizationAndProject(user.Id);
        }
    }
}