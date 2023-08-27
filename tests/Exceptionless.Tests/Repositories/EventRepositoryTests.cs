using System.Diagnostics;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Repositories;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Helpers;
using Exceptionless.Tests.Utility;
using Foundatio.Repositories;
using Foundatio.Repositories.Utility;
using Foundatio.Utility;
using Xunit;
using Xunit.Abstractions;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Exceptionless.Tests.Repositories;

public sealed class EventRepositoryTests : IntegrationTestsBase
{
    private readonly IEventRepository _repository;
    private readonly IStackRepository _stackRepository;

    public EventRepositoryTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _repository = GetService<IEventRepository>();
        _stackRepository = GetService<IStackRepository>();
    }

    [Fact(Skip = "https://github.com/elastic/elasticsearch-net/issues/2463")]
    public async Task GetAsync()
    {
        Log.SetLogLevel<EventRepository>(LogLevel.Trace);
        var ev = await _repository.AddAsync(new PersistentEvent
        {
            CreatedUtc = SystemClock.UtcNow,
            Date = new DateTimeOffset(SystemClock.UtcNow.Date, TimeSpan.Zero),
            OrganizationId = TestConstants.OrganizationId,
            ProjectId = TestConstants.ProjectId,
            StackId = TestConstants.StackId,
            Type = Event.KnownTypes.Log,
            Count = Int32.MaxValue,
            Value = Decimal.MaxValue,
            Geo = "40,-70"
        });

        Assert.Equal(ev, await _repository.GetByIdAsync(ev.Id));
    }

    [Fact(Skip = "Performance Testing")]
    public async Task GetAsyncPerformanceAsync()
    {
        var ev = await _repository.AddAsync(new RandomEventGenerator().GeneratePersistent());
        await RefreshDataAsync();
        Assert.Equal(1, await _repository.CountAsync());

        var sw = Stopwatch.StartNew();
        const int MAX_ITERATIONS = 100;
        for (int i = 0; i < MAX_ITERATIONS; i++)
        {
            Assert.NotNull(await _repository.GetByIdAsync(ev.Id));
        }

        sw.Stop();
        _logger.LogInformation("{Duration:g}", sw.Elapsed);
    }

    [Fact]
    public async Task GetPagedAsync()
    {
        var events = new List<PersistentEvent>();
        for (int i = 0; i < 6; i++)
            events.Add(EventData.GenerateEvent(projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, stackId: TestConstants.StackId, occurrenceDate: SystemClock.UtcNow.Subtract(TimeSpan.FromMinutes(i))));

        await _repository.AddAsync(events);
        await RefreshDataAsync();
        Assert.Equal(events.Count, await _repository.CountAsync());

        var results = await _repository.GetByOrganizationIdAsync(TestConstants.OrganizationId, o => o.PageNumber(2).PageLimit(2));
        Assert.Equal(2, results.Documents.Count);
        Assert.Equal(results.Documents.First().Id, events[2].Id);
        Assert.Equal(results.Documents.Last().Id, events[3].Id);

        results = await _repository.GetByOrganizationIdAsync(TestConstants.OrganizationId, o => o.PageNumber(3).PageLimit(2));
        Assert.Equal(2, results.Documents.Count);
        Assert.Equal(results.Documents.First().Id, events[4].Id);
        Assert.Equal(results.Documents.Last().Id, events[5].Id);
    }

    [Fact]
    public async Task GetPreviousEventIdInStackTestAsync()
    {
        await CreateDataAsync();
        Log.SetLogLevel<StackRepository>(LogLevel.Trace);
        Log.SetLogLevel<EventRepository>(LogLevel.Trace);

        _logger.LogDebug("Actual order:");
        foreach (var t in _ids)
            _logger.LogDebug("{Id}: {Date}", t.Item1, t.Item2.ToLongTimeString());

        _logger.LogDebug("");
        _logger.LogDebug("Sorted order:");
        var sortedIds = _ids.OrderBy(t => t.Item2.Ticks).ThenBy(t => t.Item1).ToList();
        foreach (var t in sortedIds)
            _logger.LogDebug("{Id}: {Date}", t.Item1, t.Item2.ToLongTimeString());

        _logger.LogDebug("");
        _logger.LogDebug("Tests:");
        await RefreshDataAsync();
        Assert.Equal(_ids.Count, await _repository.CountAsync());
        for (int i = 0; i < sortedIds.Count; i++)
        {
            _logger.LogDebug("Current - {Id}: {Date}", sortedIds[i].Item1, sortedIds[i].Item2.ToLongTimeString());
            if (i == 0)
                Assert.Null((await _repository.GetPreviousAndNextEventIdsAsync(sortedIds[i].Item1)).Previous);
            else
                Assert.Equal(sortedIds[i - 1].Item1, (await _repository.GetPreviousAndNextEventIdsAsync(sortedIds[i].Item1)).Previous);
        }
    }

    [Fact]
    public async Task GetNextEventIdInStackTestAsync()
    {
        await CreateDataAsync();

        _logger.LogDebug("Actual order:");
        foreach (var t in _ids)
            _logger.LogDebug("{Id}: {Date}", t.Item1, t.Item2.ToLongTimeString());

        _logger.LogDebug("");
        _logger.LogDebug("Sorted order:");
        var sortedIds = _ids.OrderBy(t => t.Item2.Ticks).ThenBy(t => t.Item1).ToList();
        foreach (var t in sortedIds)
            _logger.LogDebug("{Id}: {Date}", t.Item1, t.Item2.ToLongTimeString());

        _logger.LogDebug("");
        _logger.LogDebug("Tests:");
        await RefreshDataAsync();
        Assert.Equal(_ids.Count, await _repository.CountAsync());
        for (int i = 0; i < sortedIds.Count; i++)
        {
            _logger.LogDebug("Current - {Id}: {Date}", sortedIds[i].Item1, sortedIds[i].Item2.ToLongTimeString());
            string? nextId = (await _repository.GetPreviousAndNextEventIdsAsync(sortedIds[i].Item1)).Next;
            if (i == sortedIds.Count - 1)
                Assert.Null(nextId);
            else
                Assert.Equal(sortedIds[i + 1].Item1, nextId);
        }
    }

    [Fact]
    public async Task CanGetPreviousAndNExtEventIdWithFilterTestAsync()
    {
        await CreateDataAsync();
        Log.SetLogLevel<StackRepository>(LogLevel.Trace);
        Log.SetLogLevel<EventRepository>(LogLevel.Trace);


        var sortedIds = _ids.OrderBy(t => t.Item2.Ticks).ThenBy(t => t.Item1).ToList();
        var result = await _repository.GetPreviousAndNextEventIdsAsync(sortedIds[1].Item1);
        Assert.Equal(sortedIds[0].Item1, result.Previous);
        Assert.Equal(sortedIds[2].Item1, result.Next);
    }

    [Fact]
    public async Task GetByReferenceIdAsync()
    {
        string referenceId = ObjectId.GenerateNewId().ToString();
        await _repository.AddAsync(EventData.GenerateEvents(3, TestConstants.OrganizationId, TestConstants.ProjectId, TestConstants.StackId2, referenceId: referenceId).ToList());

        await RefreshDataAsync();
        var results = await _repository.GetByReferenceIdAsync(TestConstants.ProjectId, referenceId);
        Assert.True(results.Total > 0);
        Assert.NotNull(results.Documents.First());
        Assert.Equal(referenceId, results.Documents.First().ReferenceId);
    }

    [Fact]
    public async Task GetOpenSessionsAsync()
    {
        var firstEvent = SystemClock.OffsetNow.Subtract(TimeSpan.FromMinutes(35));

        var sessionLastActive35MinAgo = EventData.GenerateEvent(TestConstants.OrganizationId, TestConstants.ProjectId, TestConstants.StackId2, occurrenceDate: firstEvent, type: Event.KnownTypes.Session, sessionId: "opensession", generateData: false);
        var sessionLastActive34MinAgo = EventData.GenerateEvent(TestConstants.OrganizationId, TestConstants.ProjectId, TestConstants.StackId2, occurrenceDate: firstEvent, type: Event.KnownTypes.Session, sessionId: "opensession2", generateData: false);
        sessionLastActive34MinAgo.UpdateSessionStart(firstEvent.UtcDateTime.AddMinutes(1));
        var sessionLastActive5MinAgo = EventData.GenerateEvent(TestConstants.OrganizationId, TestConstants.ProjectId, TestConstants.StackId2, occurrenceDate: firstEvent, type: Event.KnownTypes.Session, sessionId: "opensession3", generateData: false);
        sessionLastActive5MinAgo.UpdateSessionStart(firstEvent.UtcDateTime.AddMinutes(30));
        var closedSession = EventData.GenerateEvent(TestConstants.OrganizationId, TestConstants.ProjectId, TestConstants.StackId2, occurrenceDate: firstEvent, type: Event.KnownTypes.Session, sessionId: "opensession", generateData: false);
        closedSession.UpdateSessionStart(firstEvent.UtcDateTime.AddMinutes(5), true);

        var events = new List<PersistentEvent> {
                sessionLastActive35MinAgo,
                sessionLastActive34MinAgo,
                sessionLastActive5MinAgo,
                closedSession
            };

        await _repository.AddAsync(events);

        await RefreshDataAsync();
        var results = await _repository.GetOpenSessionsAsync(SystemClock.UtcNow.SubtractMinutes(30));
        Assert.Equal(3, results.Total);
    }

    [Fact]
    public async Task RemoveAllByClientIpAndDateAsync()
    {
        const string _clientIpAddress = "123.123.12.255";

        const int NUMBER_OF_EVENTS_TO_CREATE = 50;
        var events = EventData.GenerateEvents(NUMBER_OF_EVENTS_TO_CREATE, TestConstants.OrganizationId, TestConstants.ProjectId, TestConstants.StackId2, startDate: SystemClock.UtcNow.SubtractDays(2), endDate: SystemClock.UtcNow).ToList();
        events.ForEach(e => e.AddRequestInfo(new RequestInfo { ClientIpAddress = _clientIpAddress }));
        await _repository.AddAsync(events);

        await RefreshDataAsync();
        events = (await _repository.GetByProjectIdAsync(TestConstants.ProjectId, o => o.PageLimit(NUMBER_OF_EVENTS_TO_CREATE))).Documents.ToList();
        Assert.Equal(NUMBER_OF_EVENTS_TO_CREATE, events.Count);
        events.ForEach(e =>
        {
            var ri = e.GetRequestInfo();
            Assert.NotNull(ri);
            Assert.Equal(_clientIpAddress, ri.ClientIpAddress);
        });

        await _repository.RemoveAllAsync(TestConstants.OrganizationId, _clientIpAddress, SystemClock.UtcNow.SubtractDays(3), SystemClock.UtcNow.AddDays(2));

        await RefreshDataAsync();
        events = (await _repository.GetByProjectIdAsync(TestConstants.ProjectId, o => o.PageLimit(NUMBER_OF_EVENTS_TO_CREATE))).Documents.ToList();
        Assert.Empty(events);
    }

    private readonly List<Tuple<string, DateTime>> _ids = new();

    private async Task CreateDataAsync()
    {
        var baseDate = SystemClock.UtcNow.SubtractHours(1);
        var occurrenceDateStart = baseDate.AddMinutes(-30);
        var occurrenceDateMid = baseDate;
        var occurrenceDateEnd = baseDate.AddMinutes(30);

        await _stackRepository.AddAsync(StackData.GenerateStack(id: TestConstants.StackId, organizationId: TestConstants.OrganizationId, projectId: TestConstants.ProjectId));

        var occurrenceDates = new List<DateTime> {
                occurrenceDateStart,
                occurrenceDateEnd,
                baseDate.AddMinutes(-10),
                baseDate.AddMinutes(-20),
                occurrenceDateMid,
                occurrenceDateMid,
                occurrenceDateMid,
                baseDate.AddMinutes(20),
                baseDate.AddMinutes(10),
                baseDate.AddSeconds(1),
                occurrenceDateEnd,
                occurrenceDateStart
            };

        foreach (var date in occurrenceDates)
        {
            var ev = await _repository.AddAsync(EventData.GenerateEvent(projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, stackId: TestConstants.StackId, occurrenceDate: date));
            _ids.Add(Tuple.Create(ev.Id, date));
        }

        await RefreshDataAsync();
    }
}
