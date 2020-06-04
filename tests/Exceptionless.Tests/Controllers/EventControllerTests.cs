using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Exceptionless.Tests.Extensions;
using Exceptionless.Web.Utility;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Models;
using Exceptionless.Core.Plugins.EventParser;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Utility;
using Exceptionless.Helpers;
using Exceptionless.Tests.Utility;
using Foundatio.Jobs;
using Foundatio.Queues;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;
using Run = Exceptionless.Tests.Utility.Run;

namespace Exceptionless.Tests.Controllers {
    public class EventControllerTests : IntegrationTestsBase {
        private readonly IEventRepository _eventRepository;
        private readonly IQueue<EventPost> _eventQueue;
        private readonly IQueue<EventUserDescription> _eventUserDescriptionQueue;

        public EventControllerTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory) {
            Log.MinimumLevel = LogLevel.Warning;

            _eventRepository = GetService<IEventRepository>();
            _eventQueue = GetService<IQueue<EventPost>>();
            _eventUserDescriptionQueue = GetService<IQueue<EventUserDescription>>();
        }

        protected override async Task ResetDataAsync() {
            await base.ResetDataAsync();
            await _eventQueue.DeleteQueueAsync();
            
            var service = GetService<SampleDataService>();
            await service.CreateDataAsync();
        }

        [Fact]
        public async Task CanPostUserDescriptionAsync() {
            await SendRequestAsync(r => r
               .Post()
               .AsClientUser()
               .AppendPath("events/by-ref/TestReferenceId/user-description")
               .Content(new EventUserDescription { Description = "Test Description", EmailAddress = TestConstants.UserEmail })
               .StatusCodeShouldBeAccepted()
            );

            var stats = await _eventUserDescriptionQueue.GetQueueStatsAsync();
            Assert.Equal(1, stats.Enqueued);
            Assert.Equal(0, stats.Completed);

            var userDescriptionJob = GetService<EventUserDescriptionsJob>();
            await userDescriptionJob.RunAsync();

            stats = await _eventUserDescriptionQueue.GetQueueStatsAsync();
            Assert.Equal(1, stats.Dequeued);
            Assert.Equal(1, stats.Abandoned); // Event doesn't exist
        }

        [Fact]
        public async Task CanPostStringAsync() {
            const string message = "simple string";
            await SendRequestAsync(r => r
                .Post()
                .AsClientUser()
                .AppendPath("events")
                .Content(message, "text/plain")
                .StatusCodeShouldBeAccepted()
            );

            var stats = await _eventQueue.GetQueueStatsAsync();
            Assert.Equal(1, stats.Enqueued);
            Assert.Equal(0, stats.Completed);

            var processEventsJob = GetService<EventPostsJob>();
            await processEventsJob.RunAsync();
            await RefreshDataAsync();

            stats = await _eventQueue.GetQueueStatsAsync();
            Assert.Equal(1, stats.Completed);

            var ev = (await _eventRepository.GetAllAsync()).Documents.Single();
            Assert.Equal(message, ev.Message);
        }

        [Fact]
        public async Task CanPostCompressedStringAsync() {
            const string message = "simple string";

            byte[] data = Encoding.UTF8.GetBytes(message);
            var ms = new MemoryStream();
            using (var gzip = new GZipStream(ms, CompressionMode.Compress, true))
                gzip.Write(data, 0, data.Length);
            ms.Position = 0;

            var content = new StreamContent(ms);
            content.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
            content.Headers.ContentEncoding.Add("gzip");
            _httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + TestConstants.ApiKey);
            var response = await _httpClient.PostAsync("events", content);
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
            Assert.True(response.Headers.Contains(Headers.ConfigurationVersion));

            var stats = await _eventQueue.GetQueueStatsAsync();
            Assert.Equal(1, stats.Enqueued);
            Assert.Equal(0, stats.Completed);

            var processEventsJob = GetService<EventPostsJob>();
            await processEventsJob.RunAsync();
            await RefreshDataAsync();

            stats = await _eventQueue.GetQueueStatsAsync();
            Assert.Equal(1, stats.Completed);

            var ev = (await _eventRepository.GetAllAsync()).Documents.Single();
            Assert.Equal(message, ev.Message);
        }

        [Fact]
        public async Task CanPostEventAsync() {
            var ev = new RandomEventGenerator().GeneratePersistent(false);
            if (String.IsNullOrEmpty(ev.Message))
                ev.Message = "Generated message.";

            await SendRequestAsync(r => r
                .Post()
                .AsClientUser()
                .AppendPath("events")
                .Content(ev)
                .StatusCodeShouldBeAccepted()
            );

            var stats = await _eventQueue.GetQueueStatsAsync();
            Assert.Equal(1, stats.Enqueued);
            Assert.Equal(0, stats.Completed);

            var processEventsJob = GetService<EventPostsJob>();
            await processEventsJob.RunAsync();
            await RefreshDataAsync();

            stats = await _eventQueue.GetQueueStatsAsync();
            Assert.Equal(1, stats.Completed);

            var actual = await _eventRepository.GetAllAsync();
            Assert.Single(actual.Documents);
            Assert.Equal(ev.Message, actual.Documents.Single().Message);
        }

        [Fact]
        public async Task CanPostManyEventsAsync() {
            const int batchSize = 50;
            const int batchCount = 10;

            await Run.InParallelAsync(batchCount, async i => {
                var events = new RandomEventGenerator().Generate(batchSize, false);
                await SendRequestAsync(r => r
                   .Post()
                   .AsClientUser()
                   .AppendPath("events")
                   .Content(events)
                   .StatusCodeShouldBeAccepted()
                );
            });

            await RefreshDataAsync();
            var stats = await _eventQueue.GetQueueStatsAsync();
            Assert.Equal(batchCount, stats.Enqueued);
            Assert.Equal(0, stats.Completed);

            var processEventsJob = GetService<EventPostsJob>();
            var sw = Stopwatch.StartNew();
            await processEventsJob.RunUntilEmptyAsync();
            sw.Stop();
            _logger.LogInformation("{Duration:g}", sw.Elapsed);

            await RefreshDataAsync();
            stats = await _eventQueue.GetQueueStatsAsync();
            Assert.Equal(batchCount, stats.Completed);
            Assert.Equal(batchSize * batchCount, await _eventRepository.CountAsync());
        }

        [Fact]
        public async Task CanGetMostFrequentStackMode() {
            await CreateStacksAndEventsAsync();
            
            // return await GetAllByTermsAsync("cardinality:user sum:count~1 min:date max:date", sf, ti, filter, mode, page, limit);

            var results = await SendRequestAsAsync<List<StackSummaryModel>>(r => r
                .AsGlobalAdminUser()
                .AppendPath("events")
                .QueryString("filter", "status:open")
                .QueryString("sort", "-date")
                .QueryString("mode", "stack")
                .StatusCodeShouldBeOk()
            );

            Assert.NotEmpty(results);
        }
        
        [Fact]
        public async Task CanGetNewStackMode() {
            await CreateStacksAndEventsAsync();
            
            // sort:-first
            var results = await SendRequestAsAsync<List<StackSummaryModel>>(r => r
                .AsGlobalAdminUser()
                .AppendPath("events")
                .QueryString("filter", "status:open")
                .QueryString("sort", "-date")
                .QueryString("mode", "stack")
                .StatusCodeShouldBeOk()
            );

            Assert.NotEmpty(results);
        }
        
        [Fact]
        public async Task GetRecentStackMode() {
            await CreateStacksAndEventsAsync();
            Log.MinimumLevel = LogLevel.Trace;

            // sort:-last
            var results = await SendRequestAsAsync<List<StackSummaryModel>>(r => r
                .AsGlobalAdminUser()
                .AppendPath("events")
                .QueryString("filter", "status:open")
                .QueryString("sort", "-date")
                .QueryString("mode", "stack")
                .StatusCodeShouldBeOk()
            );

            Assert.Single(results);
        }

        [Fact]
        public async Task GetUsersStackMode() {
            await CreateStacksAndEventsAsync();
            
            //    return await GetAllByTermsAsync("-cardinality:user sum:count~1 min:date max:date", sf, ti, filter, mode, page, limit);

            /* 
                var systemFilter = new RepositoryQuery<PersistentEvent>().AppFilter(ShouldApplySystemFilter(sf, filter) ? sf : null).DateRange(ti.Range.UtcStart, ti.Range.UtcEnd, (PersistentEvent e) => e.Date).Index(ti.Range.UtcStart, ti.Range.UtcEnd);
                var stackTerms = (await _eventRepository.CountByQueryAsync(q => q.SystemFilter(systemFilter).FilterExpression(filter).AggregationsExpression($"terms:(stack_id~{GetSkip(page + 1, limit) + 1} {aggregations})"))).Aggregations.Terms<string>("terms_stack_id");
                if (stackTerms == null || stackTerms.Buckets.Count == 0)
                    return Ok(EmptyModels);

                string[] stackIds = stackTerms.Buckets.Skip(skip).Take(limit + 1).Select(t => t.Key).ToArray();
                var stacks = (await _stackRepository.GetAsync(stackIds)).Select(s => s.ApplyOffset(ti.Offset)).ToList();

                if (!String.IsNullOrEmpty(mode) && String.Equals(mode, "summary", StringComparison.OrdinalIgnoreCase)) {
                    var summaries = await GetStackSummariesAsync(stacks, stackTerms.Buckets, sf, ti);
                    return OkWithResourceLinks(summaries.Take(limit).ToList(), summaries.Count > limit, page);
                }
*/
            var results = await SendRequestAsAsync<List<StackSummaryModel>>(r => r
                .AsGlobalAdminUser()
                .AppendPath("events")
                .QueryString("filter", "status:open")
                .QueryString("sort", "-date")
                .QueryString("mode", "stack")
                .StatusCodeShouldBeOk()
            );

            Assert.NotEmpty(results);
        }

        private async Task CreateStacksAndEventsAsync() {
            await StackData.CreateSearchDataAsync(GetService<IStackRepository>(), GetService<JsonSerializer>(), true);
            await EventData.CreateSearchDataAsync(GetService<ExceptionlessElasticConfiguration>(), _eventRepository, GetService<EventParserPluginManager>(), true);
        }
    }
}
