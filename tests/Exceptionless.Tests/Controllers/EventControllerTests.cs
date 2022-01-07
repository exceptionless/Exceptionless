using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
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
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;
using Run = Exceptionless.Tests.Utility.Run;
using Foundatio.Utility;
using Exceptionless.DateTimeExtensions;
using Foundatio.Repositories.Models;
using Exceptionless.Core.Repositories.Queries;

namespace Exceptionless.Tests.Controllers;

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
        const string json = "{\"message\":\"test\",\"reference_id\":\"TestReferenceId\",\"@user\":{\"identity\":\"Test user\",\"name\":null}}";
        await SendRequestAsync(r => r
            .Post()
            .AsTestOrganizationClientUser()
            .AppendPath("events")
            .Content(json, "application/json")
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

        var events = await _eventRepository.GetAllAsync();
        var ev = events.Documents.Single(e => String.Equals(e.Type, Event.KnownTypes.Log));
        Assert.Equal("test", ev.Message);
        Assert.Equal("TestReferenceId", ev.ReferenceId);

        var identity = ev.GetUserIdentity();
        Assert.Equal("Test user", identity.Identity);
        Assert.Null(identity.Name);
        Assert.Null(identity.Name);
        Assert.Null(ev.GetUserDescription());

        // post description
        await _eventUserDescriptionQueue.DeleteQueueAsync();
        await SendRequestAsync(r => r
            .Post()
            .AsTestOrganizationClientUser()
            .AppendPath("events/by-ref/TestReferenceId/user-description")
            .Content(new EventUserDescription { Description = "Test Description", EmailAddress = TestConstants.UserEmail })
            .StatusCodeShouldBeAccepted()
        );

        stats = await _eventUserDescriptionQueue.GetQueueStatsAsync();
        Assert.Equal(1, stats.Enqueued);
        Assert.Equal(0, stats.Completed);

        var userDescriptionJob = GetService<EventUserDescriptionsJob>();
        await userDescriptionJob.RunAsync();

        stats = await _eventUserDescriptionQueue.GetQueueStatsAsync();
        Assert.Equal(1, stats.Dequeued);
        Assert.Equal(0, stats.Abandoned);
        Assert.Equal(1, stats.Completed);

        ev = await _eventRepository.GetByIdAsync(ev.Id);
        identity = ev.GetUserIdentity();
        Assert.Equal("Test user", identity.Identity);
        Assert.Null(identity.Name);
        Assert.Null(identity.Name);

        var description = ev.GetUserDescription();
        Assert.Equal("Test Description", description.Description);
        Assert.Equal(TestConstants.UserEmail, description.EmailAddress);
    }

    [Fact]
    public async Task CanPostUserDescriptionWithNoMatchingEventAsync() {
        await SendRequestAsync(r => r
            .Post()
            .AsTestOrganizationClientUser()
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
            .AsTestOrganizationClientUser()
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
        await using (var gzip = new GZipStream(ms, CompressionMode.Compress, true))
            await gzip.WriteAsync(data, CancellationToken.None);
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
    public async Task CanPostJsonWithUserInfoAsync() {
        const string json = "{\"message\":\"test\",\"@user\":{\"identity\":\"Test user\",\"name\":null}}";
        await SendRequestAsync(r => r
            .Post()
            .AsTestOrganizationClientUser()
            .AppendPath("events")
            .Content(json, "application/json")
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

        var events = await _eventRepository.GetAllAsync();
        var ev = events.Documents.Single(e => String.Equals(e.Type, Event.KnownTypes.Log));
        Assert.Equal("test", ev.Message);

        var userInfo = ev.GetUserIdentity();
        Assert.NotNull(userInfo);
        Assert.Equal("Test user", userInfo.Identity);
        Assert.Null(userInfo.Name);
    }

    [Fact]
    public async Task CanPostEventAsync() {
        var ev = new RandomEventGenerator().GeneratePersistent(false);
        if (String.IsNullOrEmpty(ev.Message))
            ev.Message = "Generated message.";

        await SendRequestAsync(r => r
            .Post()
            .AsTestOrganizationClientUser()
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
               .AsTestOrganizationClientUser()
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

        var results = await SendRequestAsAsync<List<StackSummaryModel>>(r => r
            .AsGlobalAdminUser()
            .AppendPath("events")
            .QueryString("filter", $"project:{SampleDataService.TEST_PROJECT_ID} (status:open OR status:regressed)")
            .QueryString("mode", "stack_frequent")
            .QueryString("offset", "-300m")
            .QueryString("limit", 20)
            .StatusCodeShouldBeOk()
        );

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task CanGetProjectLevelMostFrequentStackMode() {
        await CreateStacksAndEventsAsync();

        string projectId = SampleDataService.TEST_PROJECT_ID;

        var results = await SendRequestAsAsync<List<StackSummaryModel>>(r => r
            .AsTestOrganizationUser()
            .AppendPath("projects", projectId, "events")
            .QueryString("filter", $"project:{projectId} (status:open OR status:regressed)")
            .QueryString("mode", "stack_frequent")
            .QueryString("offset", "-300m")
            .QueryString("limit", 20)
            .StatusCodeShouldBeOk()
        );

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task CanGetFreeProjectLevelMostFrequentStackMode() {
        await CreateStacksAndEventsAsync();

        Log.SetLogLevel<StackRepository>(LogLevel.Trace);
        Log.SetLogLevel<EventRepository>(LogLevel.Trace);
        Log.SetLogLevel<EventStackFilterQueryBuilder>(LogLevel.Trace);

        string projectId = SampleDataService.FREE_PROJECT_ID;
        var results = await SendRequestAsAsync<List<StackSummaryModel>>(r => r
            .AsFreeOrganizationUser()
            .AppendPath("projects", projectId, "events")
            .QueryString("filter", $"project:{projectId} (status:open OR status:regressed)")
            .QueryString("mode", "stack_frequent")
            .QueryString("offset", "-300m")
            .QueryString("limit", 20)
            .StatusCodeShouldBeOk()
        );

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task CanGetNewStackMode() {
        Log.MinimumLevel = LogLevel.Warning;
        await CreateStacksAndEventsAsync();

        Log.SetLogLevel<StackRepository>(LogLevel.Trace);
        Log.SetLogLevel<EventRepository>(LogLevel.Trace);
        Log.SetLogLevel<EventStackFilterQueryBuilder>(LogLevel.Trace);

        var results = await SendRequestAsAsync<List<StackSummaryModel>>(r => r
            .AsGlobalAdminUser()
            .AppendPath("events")
            .QueryString("filter", $"project:{SampleDataService.TEST_PROJECT_ID} (status:open OR status:regressed)")
            .QueryString("mode", "stack_new")
            .StatusCodeShouldBeOk()
        );

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task GetRecentStackMode() {
        await CreateStacksAndEventsAsync();

        var results = await SendRequestAsAsync<List<StackSummaryModel>>(r => r
            .AsGlobalAdminUser()
            .AppendPath("events")
            .QueryString("filter", $"project:{SampleDataService.TEST_PROJECT_ID} (status:open OR status:regressed)")
            .QueryString("mode", "stack_recent")
            .StatusCodeShouldBeOk()
        );

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task GetUsersStackMode() {
        await CreateStacksAndEventsAsync();

        var results = await SendRequestAsAsync<List<StackSummaryModel>>(r => r
            .AsGlobalAdminUser()
            .AppendPath("events")
            .QueryString("filter", $"project:{SampleDataService.TEST_PROJECT_ID} type:error (status:open OR status:regressed)")
            .QueryString("mode", "stack_users")
            .QueryString("offset", "-300m")
            .StatusCodeShouldBeOk()
        );

        Assert.Single(results);
    }

    [Fact]
    public async Task WillExcludeDeletedSessions() {
        await CreateDataAsync(d => {
            d.Event().TestProject().Type(Event.KnownTypes.Session).Deleted();
            d.Event().TestProject().Type(Event.KnownTypes.Session);
        });

        Log.SetLogLevel<EventRepository>(LogLevel.Trace);
        Log.SetLogLevel<StackRepository>(LogLevel.Trace);
        Log.SetLogLevel<EventStackFilterQueryBuilder>(LogLevel.Trace);

        var countResult = await SendRequestAsAsync<CountResult>(r => r
            .AsGlobalAdminUser()
            .AppendPath("projects", SampleDataService.TEST_PROJECT_ID, "events", "count")
            .QueryString("filter", "type:session _missing_:data.sessionend")
            .StatusCodeShouldBeOk()
        );

        Assert.Equal(1, countResult.Total);

        var results = await SendRequestAsAsync<IReadOnlyCollection<PersistentEvent>>(r => r
            .AsGlobalAdminUser()
            .AppendPath("projects", SampleDataService.TEST_PROJECT_ID, "events", "sessions")
            .QueryString("filter", "_missing_:data.sessionend")
            .StatusCodeShouldBeOk()
        );

        Assert.Single(results);
    }

    [Fact]
    public async Task WillGetStackEvents() {
        var (stacks, _) = await CreateDataAsync(d => {
            d.Event().TestProject();
        });

        Log.SetLogLevel<EventRepository>(LogLevel.Trace);
        Log.SetLogLevel<StackRepository>(LogLevel.Trace);
        Log.SetLogLevel<EventStackFilterQueryBuilder>(LogLevel.Trace);

        var result = await SendRequestAsAsync<IReadOnlyCollection<PersistentEvent>>(r => r
            .AsGlobalAdminUser()
            .AppendPath("stacks", stacks.Single().Id, "events")
            .StatusCodeShouldBeOk()
        );

        Assert.Single(result);
    }

    [Fact]
    public async Task WillGetEventSessions() {
        string sessionId = Guid.NewGuid().ToString("N");
        await CreateDataAsync(d => {
            d.Event().TestProject().Type(Event.KnownTypes.Session).SessionId(sessionId);
            d.Event().TestProject().Type(Event.KnownTypes.Log).SessionId(sessionId);
        });

        Log.SetLogLevel<EventRepository>(LogLevel.Trace);
        Log.SetLogLevel<StackRepository>(LogLevel.Trace);
        Log.SetLogLevel<EventStackFilterQueryBuilder>(LogLevel.Trace);

        var result = await SendRequestAsAsync<IReadOnlyCollection<PersistentEvent>>(r => r
            .AsGlobalAdminUser()
            .AppendPath("events/sessions", sessionId)
            .QueryString("filter", "-type:heartbeat")
            .QueryString("limit", "10")
            .StatusCodeShouldBeOk()
        );

        Assert.Equal(2, result.Count);

        result = await SendRequestAsAsync<IReadOnlyCollection<PersistentEvent>>(r => r
            .AsGlobalAdminUser()
            .AppendPath("projects", SampleDataService.TEST_PROJECT_ID, "events/sessions", sessionId)
            .QueryString("filter", "-type:heartbeat")
            .QueryString("limit", "10")
            .QueryString("offset", "-360m")
            .QueryString("time", $"{SystemClock.UtcNow.SubtractDays(180):s}-now")
            .StatusCodeShouldBeOk()
        );

        Assert.Equal(2, result.Count);
    }

    [Theory]
    [InlineData("status:open", 1)]
    [InlineData("status:regressed", 1)]
    [InlineData("status:ignored", 1)]
    [InlineData("type:error (status:open OR status:regressed)", 1)]
    [InlineData("(status:open OR status:regressed)", 2)]
    [InlineData("is_fixed:true", 2)]
    [InlineData("status:fixed", 2)]
    [InlineData("status:discarded", 0)]
    [InlineData("tags:old_tag", 0)] // Stack only tags won't be resolved
    [InlineData("type:log status:fixed", 2)]
    [InlineData("type:log version_fixed:1.2.3", 1)]
    [InlineData("type:error is_hidden:false is_fixed:false is_regressed:true", 1)]
    [InlineData("type:error hidden:false fixed:false", 1)]
    [InlineData("type:log status:fixed version_fixed:1.2.3", 1)]
    [InlineData("1ecd0826e447a44e78877ab1", 0)] // Stack Id
    [InlineData("type:error", 1)]
    public async Task CheckStackModeCounts(string filter, int expected) {
        await CreateStacksAndEventsAsync();

        string[] modes = new[] { "stack_recent", "stack_frequent", "stack_new", "stack_users" };
        foreach (string mode in modes) {
            var results = await SendRequestAsAsync<List<StackSummaryModel>>(r => r
                .AsGlobalAdminUser()
                .AppendPath("events")
                .QueryString("filter", filter)
                .QueryString("mode", mode)
                .StatusCodeShouldBeOk()
            );

            Assert.Equal(expected, results.Count);

            // @! forces use of opposite of default filter inversion
            results = await SendRequestAsAsync<List<StackSummaryModel>>(r => r
                .AsGlobalAdminUser()
                .AppendPath("events")
                .QueryString("filter", $"@!{filter}")
                .QueryString("mode", mode)
                .StatusCodeShouldBeOk()
            );

            Assert.Equal(expected, results.Count);
        }
    }

    [Theory]
    [InlineData("status:open", 1)]
    [InlineData("status:regressed", 3)]
    [InlineData("status:ignored", 1)]
    [InlineData("type:error (status:open OR status:regressed)", 2)]
    [InlineData("(status:open OR status:regressed)", 4)]
    [InlineData("is_fixed:true", 2)]
    [InlineData("status:fixed", 2)]
    [InlineData("status:discarded", 0)]
    [InlineData("tags:old_tag", 0)] // Stack only tags won't be resolved
    [InlineData("type:log status:fixed", 2)]
    [InlineData("type:log version_fixed:1.2.3", 1)]
    [InlineData("type:error is_hidden:false is_fixed:false is_regressed:true", 2)]
    [InlineData("type:log status:fixed version_fixed:1.2.3", 1)]
    [InlineData("1ecd0826e447a44e78877ab1", 0)] // Stack Id
    [InlineData("type:error", 2)]
    public async Task CheckSummaryModeCounts(string filter, int expected) {
        await CreateStacksAndEventsAsync();
        Log.SetLogLevel<EventRepository>(LogLevel.Trace);

        var results = await SendRequestAsAsync<List<StackSummaryModel>>(r => r
            .AsGlobalAdminUser()
            .AppendPath("events")
            .QueryString("filter", filter)
            .QueryString("mode", "summary")
            .StatusCodeShouldBeOk()
        );

        Assert.Equal(expected, results.Count);

        // @! forces use of opposite of default filter inversion
        results = await SendRequestAsAsync<List<StackSummaryModel>>(r => r
            .AsGlobalAdminUser()
            .AppendPath("events")
            .QueryString("filter", $"@!{filter}")
            .QueryString("mode", "summary")
            .StatusCodeShouldBeOk()
        );

        Assert.Equal(expected, results.Count);
    }

    [InlineData(null)]
    [InlineData("")]
    [InlineData("@!")]
    [InlineData("status:open OR status:regressed")]
    [InlineData("(status:open OR status:regressed)")]
    [InlineData("@!status:open OR status:regressed")]
    [InlineData("@!(status:open OR status:regressed)")]
    [Theory]
    public async Task WillExcludeDeletedStacks(string filter) {
        var utcNow = SystemClock.UtcNow;

        await CreateDataAsync(d => {
            d.Event()
                .TestProject()
                .Type(Event.KnownTypes.Log)
                .Status(StackStatus.Open)
                .Deleted()
                .TotalOccurrences(50)
                .FirstOccurrence(utcNow.SubtractDays(1));

            d.Event()
                .TestProject()
                .Type(Event.KnownTypes.Error)
                .Status(StackStatus.Regressed)
                .TotalOccurrences(10)
                .FirstOccurrence(utcNow.SubtractDays(2))
                .StackReference("https://github.com/exceptionless/Exceptionless")
                .Version("3.2.1-beta1");
        });

        Log.MinimumLevel = LogLevel.Trace;
        var results = await SendRequestAsAsync<List<StackSummaryModel>>(r => r
            .AsGlobalAdminUser()
            .AppendPath("events")
            .QueryStringIf(() => !String.IsNullOrEmpty(filter), "filter", filter)
            .QueryString("mode", "stack_new")
            .StatusCodeShouldBeOk()
        );

        Assert.Single(results);

        var countResult = await SendRequestAsAsync<CountResult>(r => r
            .AsGlobalAdminUser()
            .AppendPath("events", "count")
            .QueryStringIf(() => !String.IsNullOrEmpty(filter), "filter", filter)
            .QueryString("aggregations", "date:(date cardinality:stack sum:count~1) cardinality:stack terms:(first @include:true) sum:count~1")
            .StatusCodeShouldBeOk()
        );

        var dateAgg = countResult.Aggregations.DateHistogram("date_date");
        double dateAggStackCount = dateAgg.Buckets.Sum(t => t.Aggregations.Cardinality("cardinality_stack").Value.GetValueOrDefault());
        double dateAggEventCount = dateAgg.Buckets.Sum(t => t.Aggregations.Cardinality("sum_count").Value.GetValueOrDefault());
        Assert.Equal(1, dateAggStackCount);
        Assert.Equal(1, dateAggEventCount);

        double? total = countResult.Aggregations.Sum("sum_count")?.Value;
        double newTotal = countResult.Aggregations.Terms<double>("terms_first")?.Buckets.FirstOrDefault()?.Total ?? 0;
        double uniqueTotal = countResult.Aggregations.Cardinality("cardinality_stack")?.Value ?? 0;

        Assert.Equal(1, total);
        Assert.Equal(0, newTotal);
        Assert.Equal(1, uniqueTotal);
    }

    [Fact]
    public async Task WillExcludeOldStacksForStackNewMode() {
        var utcNow = SystemClock.UtcNow;

        await CreateDataAsync(d => {
            d.Event()
                .TestProject()
                .Message("New stack - skip due to date filter")
                .Type(Event.KnownTypes.Log)
                .Status(StackStatus.Open)
                .TotalOccurrences(50)
                .IsFirstOccurrence()
                .FirstOccurrence(utcNow.SubtractYears(1))
                .LastOccurrence(utcNow.SubtractMonths(5));

            d.Event()
                .TestProject()
                .Message("Old stack - new event")
                .Type(Event.KnownTypes.Log)
                .Status(StackStatus.Regressed)
                .TotalOccurrences(33)
                .FirstOccurrence(utcNow.SubtractYears(1))
                .LastOccurrence(utcNow);

            d.Event()
                .TestProject()
                .Message("New Stack - event not marked as first occurrence")
                .Type(Event.KnownTypes.Log)
                .Status(StackStatus.Open)
                .TotalOccurrences(15)
                .FirstOccurrence(utcNow.SubtractDays(2))
                .Version("1.2.3");

            d.Event()
                .TestProject()
                .Message("New Stack - event marked as first occurrence")
                .Type(Event.KnownTypes.Error)
                .Status(StackStatus.Regressed)
                .TotalOccurrences(10)
                .FirstOccurrence(utcNow.SubtractDays(2))
                .Date(utcNow.SubtractDays(2))
                .IsFirstOccurrence()
                .StackReference("https://github.com/exceptionless/Exceptionless")
                .Version("3.2.1-beta1");

            d.Event()
                .TestProject()
                .Message("Deleted New stack - event is first occurrence")
                .Type(Event.KnownTypes.FeatureUsage)
                .Status(StackStatus.Open)
                .TotalOccurrences(7)
                .FirstOccurrence(utcNow.Date)
                .IsFirstOccurrence()
                .Date(utcNow.Date)
                .Deleted();
        });

        Log.SetLogLevel<StackRepository>(LogLevel.Trace);
        Log.SetLogLevel<EventRepository>(LogLevel.Trace);
        Log.SetLogLevel<EventControllerTests>(LogLevel.Trace);
        Log.SetLogLevel<EventStackFilterQueryBuilder>(LogLevel.Trace);

        const string filter = "(status:open OR status:regressed)";
        const string time = "last week";

        /*
        _logger.LogInformation("Running non-inverted query");
        var invertedResults = await SendRequestAsAsync<List<StackSummaryModel>>(r => r
            .AsGlobalAdminUser()
            .AppendPath("events")
            .QueryString("filter", "@!" + filter)
            .QueryString("time", time)
            .QueryString("mode", "stack_new")
            .StatusCodeShouldBeOk()
        );*/

        //Assert.Equal(2, invertedResults.Count);

        _logger.LogInformation("Running inverted query");
        var results = await SendRequestAsAsync<List<StackSummaryModel>>(r => r
            .AsGlobalAdminUser()
            .AppendPath("events")
            .QueryString("filter", filter)
            .QueryString("time", time)
            .QueryString("mode", "stack_new")
            .StatusCodeShouldBeOk()
        );

        Assert.Equal(2, results.Count);

        _logger.LogInformation("Running normal count");
        var countResult = await SendRequestAsAsync<CountResult>(r => r
            .AsGlobalAdminUser()
            .AppendPath("events", "count")
            .QueryString("filter", filter)
            .QueryString("time", time)
            .QueryString("mode", "stack_new")
            .QueryString("aggregations", "date:(date cardinality:stack sum:count~1) cardinality:stack terms:(first @include:true) sum:count~1")
            .StatusCodeShouldBeOk()
        );

        var dateAgg = countResult.Aggregations.DateHistogram("date_date");
        double dateAggStackCount = dateAgg.Buckets.Sum(t => t.Aggregations.Cardinality("cardinality_stack").Value.GetValueOrDefault());
        double dateAggEventCount = dateAgg.Buckets.Sum(t => t.Aggregations.Cardinality("sum_count").Value.GetValueOrDefault());
        Assert.Equal(2, dateAggStackCount);
        Assert.Equal(2, dateAggEventCount);

        double? total = countResult.Aggregations.Sum("sum_count")?.Value;
        double newTotal = countResult.Aggregations.Terms<double>("terms_first")?.Buckets.FirstOrDefault()?.Total ?? 0;
        double uniqueTotal = countResult.Aggregations.Cardinality("cardinality_stack")?.Value ?? 0;

        Assert.Equal(2, total);
        Assert.Equal(1, newTotal);
        Assert.Equal(2, uniqueTotal);
    }

    private async Task CreateStacksAndEventsAsync() {
        var utcNow = SystemClock.UtcNow;

        await CreateDataAsync(d => {
            // matches event1.json / stack1.json
            d.Event()
                .FreeProject()
                .Type(Event.KnownTypes.Log)
                .Level("Error")
                .Source("GET /Print")
                .DateFixed()
                .TotalOccurrences(5)
                .StackReference("http://exceptionless.io")
                .FirstOccurrence(utcNow.SubtractDays(1))
                .Tag("test", "Critical")
                .Geo("40,-70")
                .Value(1.0M)
                .RequestInfoSample()
                .UserIdentity("My-User-Identity", "test user")
                .UserDescription("test@exceptionless.com", "my custom description")
                .Version("1.2.3.0")
                .ReferenceId("876554321");

            // matches event2.json / stack2.json
            var stack2 = d.Event()
                .FreeProject()
                .Type(Event.KnownTypes.Error)
                .Status(StackStatus.Regressed)
                .TotalOccurrences(50)
                .FirstOccurrence(utcNow.SubtractDays(2))
                .StackReference("https://github.com/exceptionless/Exceptionless")
                .Tag("Blake Niemyjski")
                .RequestInfoSample()
                .UserIdentity("example@exceptionless.com")
                .Version("3.2.1-beta1");

            // matches event3.json and using the same stack as the previous event
            d.Event()
                .FreeProject()
                .Type(Event.KnownTypes.Error)
                .Stack(stack2)
                .Tag("Blake Niemyjski")
                .RequestInfoSample()
                .UserIdentity("example", "Blake")
                .Version("4.0.1039 6f929bbe18");

            // defaults everything
            d.Event().FreeProject();
        });

        Log.MinimumLevel = LogLevel.Warning;
        await StackData.CreateSearchDataAsync(GetService<IStackRepository>(), GetService<JsonSerializer>(), true);
        await EventData.CreateSearchDataAsync(GetService<ExceptionlessElasticConfiguration>(), _eventRepository, GetService<EventParserPluginManager>(), true);
        Log.MinimumLevel = LogLevel.Trace;
    }
}
