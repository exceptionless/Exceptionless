﻿using System;
using System.Diagnostics;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Results;
using Exceptionless.Api.Controllers;
using Exceptionless.Api.Tests.Utility;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Models;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Helpers;
using Exceptionless.Tests.Utility;
using Foundatio.Metrics;
using Foundatio.Queues;
using Microsoft.Owin;
using Nest;
using Newtonsoft.Json;
using Xunit;

namespace Exceptionless.Api.Tests.Controllers {
    public class EventControllerTests : CaptureFixture {
        private static bool _databaseReset;
        private static bool _sampleOrganizationsAdded;
        private static bool _sampleProjectsAdded;

        private readonly IElasticClient _client = IoC.GetInstance<IElasticClient>();
        private readonly EventController _eventController = IoC.GetInstance<EventController>();
        private readonly IEventRepository _eventRepository = IoC.GetInstance<IEventRepository>();
        private readonly IQueue<EventPost> _eventQueue = IoC.GetInstance<IQueue<EventPost>>();
        private readonly IOrganizationRepository _organizationRepository = IoC.GetInstance<IOrganizationRepository>();
        private readonly IProjectRepository _projectRepository = IoC.GetInstance<IProjectRepository>();

        [Fact]
        public async Task CanPostStringAsync() {
            await ResetAsync();

            try {
                _eventController.Request = CreateRequestMessage(new ClaimsPrincipal(new User { EmailAddress = TestConstants.UserEmail, Id = TestConstants.UserId, OrganizationIds = new[] { TestConstants.OrganizationId }, Roles = new[] { AuthorizationRoles.Client }}.ToIdentity(TestConstants.ProjectId)), false, false);

                var metricsClient = IoC.GetInstance<IMetricsClient>() as InMemoryMetricsClient;
                Assert.NotNull(metricsClient);

                Assert.True(await metricsClient.WaitForCounterAsync("eventpost.enqueued", work: async () => {
                    var actionResult = await _eventController.PostAsync(Encoding.UTF8.GetBytes("simple string"));
                    Assert.IsType<StatusCodeResult>(actionResult);
                }));

                Assert.Equal(1, (await _eventQueue.GetQueueStatsAsync()).Enqueued);
                Assert.Equal(0, (await _eventQueue.GetQueueStatsAsync()).Completed);

                var processEventsJob = IoC.GetInstance<EventPostsJob>();
                await processEventsJob.RunAsync();

                Assert.Equal(1, (await _eventQueue.GetQueueStatsAsync()).Completed);
                Assert.Equal(1, await EventCountAsync());
            } finally {
                await RemoveAllEventsAsync();
            }
        }

        [Fact]
        public async Task CanPostCompressedStringAsync() {
            await ResetAsync();

            try {
                _eventController.Request = CreateRequestMessage(new ClaimsPrincipal(new User { EmailAddress = TestConstants.UserEmail, Id = TestConstants.UserId, OrganizationIds = new[] { TestConstants.OrganizationId }, Roles = new[] { AuthorizationRoles.Client } }.ToIdentity(TestConstants.ProjectId)), true, false);
                var actionResult = await _eventController.PostAsync(await Encoding.UTF8.GetBytes("simple string").CompressAsync());
                Assert.IsType<StatusCodeResult>(actionResult);
                Assert.Equal(1, (await _eventQueue.GetQueueStatsAsync()).Enqueued);
                Assert.Equal(0, (await _eventQueue.GetQueueStatsAsync()).Completed);

                var processEventsJob = IoC.GetInstance<EventPostsJob>();
                await processEventsJob.RunAsync();

                Assert.Equal(1, (await _eventQueue.GetQueueStatsAsync()).Completed);
                Assert.Equal(1, await EventCountAsync());
            } finally {
                await RemoveAllEventsAsync();
            }
        }

        [Fact]
        public async Task CanPostSingleEventAsync() {
            await ResetAsync();

            try {
                _eventController.Request = CreateRequestMessage(new ClaimsPrincipal(new User { EmailAddress = TestConstants.UserEmail, Id = TestConstants.UserId, OrganizationIds = new[] { TestConstants.OrganizationId }, Roles = new[] { AuthorizationRoles.Client } }.ToIdentity(TestConstants.ProjectId)), true, false);
                var actionResult = await _eventController.PostAsync(await Encoding.UTF8.GetBytes("simple string").CompressAsync());
                Assert.IsType<StatusCodeResult>(actionResult);
                Assert.Equal(1, (await _eventQueue.GetQueueStatsAsync()).Enqueued);
                Assert.Equal(0, (await _eventQueue.GetQueueStatsAsync()).Completed);

                var processEventsJob = IoC.GetInstance<EventPostsJob>();
                await processEventsJob.RunAsync();

                Assert.Equal(1, (await _eventQueue.GetQueueStatsAsync()).Completed);
                Assert.Equal(1, await EventCountAsync());
            } finally {
                await RemoveAllEventsAsync();
            }
        }

        [Fact]
        public async Task CanPostManyEventsAsync() {
            await ResetAsync();

            const int batchSize = 250;
            const int batchCount = 10;

            try {
                await Run.InParallelAsync(batchCount, async i => {
                    _eventController.Request = CreateRequestMessage(new ClaimsPrincipal(new User { EmailAddress = TestConstants.UserEmail, Id = TestConstants.UserId, OrganizationIds = new[] { TestConstants.OrganizationId }, Roles = new[] { AuthorizationRoles.Client } }.ToIdentity(TestConstants.ProjectId)), true, false);
                    var events = new RandomEventGenerator().Generate(batchSize);
                    var compressedEvents = await Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(events)).CompressAsync();
                    var actionResult = await _eventController.PostAsync(compressedEvents, version: 2, userAgent: "exceptionless/2.0.0.0");
                    Assert.IsType<StatusCodeResult>(actionResult);
                });

                await _client.RefreshAsync();
                Assert.Equal(batchCount, (await _eventQueue.GetQueueStatsAsync()).Enqueued);
                Assert.Equal(0, (await _eventQueue.GetQueueStatsAsync()).Completed);

                var processEventsJob = IoC.GetInstance<EventPostsJob>();
                var sw = Stopwatch.StartNew();
                await processEventsJob.RunUntilEmptyAsync();
                sw.Stop();
                Trace.WriteLine(sw.Elapsed);

                await _client.RefreshAsync();
                var stats = await _eventQueue.GetQueueStatsAsync();
                Assert.Equal(batchCount, stats.Completed);
                var minimum = batchSize * batchCount;
                Assert.InRange(await EventCountAsync(), minimum, minimum * 2);
            } finally {
                await _eventQueue.DeleteQueueAsync();
            }
        }

        private HttpRequestMessage CreateRequestMessage(ClaimsPrincipal user, bool isCompressed, bool isJson, string charset = "utf-8") {
            var request = new HttpRequestMessage();

            var context = new OwinContext();
            context.Request.User = Thread.CurrentPrincipal = user;
            request.SetOwinContext(context);
            request.SetConfiguration(new HttpConfiguration());
            request.Content = new HttpMessageContent(new HttpRequestMessage(HttpMethod.Post, "/api/v2/events"));
            if (isCompressed)
                request.Content.Headers.ContentEncoding.Add("gzip");
            request.Content.Headers.ContentType.MediaType = isJson ? "application/json" : "text/plain";
            request.Content.Headers.ContentType.CharSet = charset;

            return request;
        }

        private bool _isReset;
        private async Task ResetAsync() {
            if (!_isReset) {
                _isReset = true;
                await ResetDatabaseAsync();
                await AddSamplesAsync();
            }

            await _eventQueue.DeleteQueueAsync();
            await RemoveAllEventsAsync();
        }

        private async Task ResetDatabaseAsync(bool force = false) {
            if (_databaseReset && !force)
                return;

            await RemoveAllEventsAsync();
            await RemoveAllProjectsAsync();
            await RemoveAllOrganizationsAsync();

            _databaseReset = true;
        }

        public async Task RemoveAllOrganizationsAsync() {
            await _client.RefreshAsync();
            await _organizationRepository.RemoveAllAsync();
            await _client.RefreshAsync();
            _sampleOrganizationsAdded = false;
        }

        public async Task RemoveAllProjectsAsync() {
            await _client.RefreshAsync();
            await _projectRepository.RemoveAllAsync();
            await _client.RefreshAsync();
            _sampleProjectsAdded = false;
        }

        public async Task RemoveAllEventsAsync() {
            await _client.RefreshAsync();
            await _eventRepository.RemoveAllAsync();
            await _client.RefreshAsync();
        }

        public async Task<long> EventCountAsync() {
            await _client.RefreshAsync();
            return await _eventRepository.CountAsync();
        }

        public async Task AddSampleProjectsAsync() {
            if (_sampleProjectsAdded)
                return;

            await _projectRepository.AddAsync(ProjectData.GenerateSampleProjects());
            await _client.RefreshAsync();
            _sampleProjectsAdded = true;
        }

        public async Task AddSampleOrganizationsAsync() {
            if (_sampleOrganizationsAdded)
                return;

            await _organizationRepository.AddAsync(OrganizationData.GenerateSampleOrganizations());
            await _client.RefreshAsync();
            _sampleOrganizationsAdded = true;
        }

        public async Task AddSamplesAsync() {
            await AddSampleProjectsAsync();
            await AddSampleOrganizationsAsync();
        }
    }
}
