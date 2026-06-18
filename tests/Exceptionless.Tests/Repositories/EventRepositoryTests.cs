using System.Diagnostics;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Repositories;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Helpers;
using Exceptionless.Tests.Utility;
using Foundatio.Repositories;
using Foundatio.Repositories.Utility;
using Foundatio.Serializer;
using Xunit;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Exceptionless.Tests.Repositories;

public sealed class EventRepositoryTests : IntegrationTestsBase
{
    private readonly List<Tuple<string, DateTime>> _ids = new();
    private readonly Exceptionless.Helpers.RandomEventGenerator _randomEventGenerator;
    private readonly EventData _eventData;
    private readonly IEventRepository _repository;
    private readonly StackData _stackData;
    private readonly IStackRepository _stackRepository;
    private readonly ITextSerializer _serializer;

    public EventRepositoryTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _randomEventGenerator = GetService<Exceptionless.Helpers.RandomEventGenerator>();
        _eventData = GetService<EventData>();
        _repository = GetService<IEventRepository>();
        _stackData = GetService<StackData>();
        _stackRepository = GetService<IStackRepository>();
        _serializer = GetService<ITextSerializer>();
    }

    [Fact]
    public async Task GetAsync()
    {
        Log.SetLogLevel<EventRepository>(LogLevel.Trace);
        var ev = await _repository.AddAsync(new PersistentEvent
        {
            CreatedUtc = DateTime.UtcNow,
            Date = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero),
            OrganizationId = TestConstants.OrganizationId,
            ProjectId = TestConstants.ProjectId,
            StackId = TestConstants.StackId,
            Type = Event.KnownTypes.Log,
            Count = Int32.MaxValue,
            Value = Decimal.MaxValue,
            Geo = "40,-70"
        });

        var actual = await _repository.GetByIdAsync(ev.Id);
        Assert.NotNull(actual);
        Assert.Equal(ev.Id, actual.Id);
        Assert.Equal(ev.Type, actual.Type);
        Assert.Equal(ev.OrganizationId, actual.OrganizationId);
        Assert.Equal(ev.ProjectId, actual.ProjectId);
        Assert.Equal(ev.StackId, actual.StackId);
        Assert.Equal(ev.Date, actual.Date);
        Assert.Equal(ev.Count, actual.Count);
        Assert.Equal(ev.Value, actual.Value);
        Assert.Equal(ev.Geo, actual.Geo);
    }

    [Fact(Skip = "Performance Testing")]
    public async Task GetAsyncPerformanceAsync()
    {
        var ev = await _repository.AddAsync(_randomEventGenerator.GeneratePersistent());
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
            events.Add(_eventData.GenerateEvent(projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, stackId: TestConstants.StackId, occurrenceDate: DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(i))));

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
            var adjacentEvents = await _repository.GetPreviousAndNextEventIdsAsync(sortedIds[i].Item1);
            Assert.NotNull(adjacentEvents);
            if (i == 0)
                Assert.Null(adjacentEvents.Previous);
            else
                Assert.Equal(sortedIds[i - 1].Item1, adjacentEvents.Previous);
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
        Assert.Equal(_ids.Count, await _repository.CountAsync());
        for (int i = 0; i < sortedIds.Count; i++)
        {
            _logger.LogDebug("Current - {Id}: {Date}", sortedIds[i].Item1, sortedIds[i].Item2.ToLongTimeString());
            var adjacentEvents = await _repository.GetPreviousAndNextEventIdsAsync(sortedIds[i].Item1);
            Assert.NotNull(adjacentEvents);
            string? nextId = adjacentEvents.Next;
            if (i == sortedIds.Count - 1)
                Assert.Null(nextId);
            else
                Assert.Equal(sortedIds[i + 1].Item1, nextId);
        }
    }

    [Fact]
    public async Task CanGetPreviousAndNextEventIdWithFilterTestAsync()
    {
        await CreateDataAsync();
        Log.SetLogLevel<StackRepository>(LogLevel.Trace);
        Log.SetLogLevel<EventRepository>(LogLevel.Trace);


        var sortedIds = _ids.OrderBy(t => t.Item2.Ticks).ThenBy(t => t.Item1).ToList();
        var result = await _repository.GetPreviousAndNextEventIdsAsync(sortedIds[1].Item1);
        Assert.NotNull(result);
        Assert.Equal(sortedIds[0].Item1, result.Previous);
        Assert.Equal(sortedIds[2].Item1, result.Next);
    }

    [Fact]
    public async Task GetByReferenceIdAsync()
    {
        string referenceId = ObjectId.GenerateNewId().ToString();
        await _repository.AddAsync(_eventData.GenerateEvents(3, TestConstants.OrganizationId, TestConstants.ProjectId, TestConstants.StackId2, referenceId: referenceId).ToList(), o => o.ImmediateConsistency());

        var results = await _repository.GetByReferenceIdAsync(TestConstants.ProjectId, referenceId);
        Assert.True(results.Total > 0);
        Assert.NotNull(results.Documents.First());
        Assert.Equal(referenceId, results.Documents.First().ReferenceId);
    }

    [Fact]
    public async Task GetOpenSessionsAsync()
    {
        var firstEvent = DateTimeOffset.Now.Subtract(TimeSpan.FromMinutes(35));

        var sessionLastActive35MinAgo = _eventData.GenerateEvent(TestConstants.OrganizationId, TestConstants.ProjectId, TestConstants.StackId2, occurrenceDate: firstEvent, type: Event.KnownTypes.Session, sessionId: "opensession", generateData: false);
        var sessionLastActive34MinAgo = _eventData.GenerateEvent(TestConstants.OrganizationId, TestConstants.ProjectId, TestConstants.StackId2, occurrenceDate: firstEvent, type: Event.KnownTypes.Session, sessionId: "opensession2", generateData: false);
        sessionLastActive34MinAgo.UpdateSessionStart(firstEvent.UtcDateTime.AddMinutes(1));
        var sessionLastActive5MinAgo = _eventData.GenerateEvent(TestConstants.OrganizationId, TestConstants.ProjectId, TestConstants.StackId2, occurrenceDate: firstEvent, type: Event.KnownTypes.Session, sessionId: "opensession3", generateData: false);
        sessionLastActive5MinAgo.UpdateSessionStart(firstEvent.UtcDateTime.AddMinutes(30));
        var closedSession = _eventData.GenerateEvent(TestConstants.OrganizationId, TestConstants.ProjectId, TestConstants.StackId2, occurrenceDate: firstEvent, type: Event.KnownTypes.Session, sessionId: "opensession", generateData: false);
        closedSession.UpdateSessionStart(firstEvent.UtcDateTime.AddMinutes(5), true);

        var events = new List<PersistentEvent> {
                sessionLastActive35MinAgo,
                sessionLastActive34MinAgo,
                sessionLastActive5MinAgo,
                closedSession
            };

        await _repository.AddAsync(events, o => o.ImmediateConsistency());

        var results = await _repository.GetOpenSessionsAsync(DateTime.UtcNow.SubtractMinutes(30));
        Assert.Equal(3, results.Total);
    }

    [Fact]
    public async Task RemoveAllByClientIpAndDateAsync()
    {
        const string _clientIpAddress = "123.123.12.255";
        const int NUMBER_OF_EVENTS_TO_CREATE = 50;

        var events = _eventData.GenerateEvents(NUMBER_OF_EVENTS_TO_CREATE, TestConstants.OrganizationId, TestConstants.ProjectId, TestConstants.StackId2, startDate: DateTime.UtcNow.SubtractDays(2), endDate: DateTime.UtcNow).ToList();
        events.ForEach(e => e.AddRequestInfo(new RequestInfo { ClientIpAddress = _clientIpAddress }));
        await _repository.AddAsync(events, o => o.ImmediateConsistency());

        events = (await _repository.GetByProjectIdAsync(TestConstants.ProjectId, o => o.PageLimit(NUMBER_OF_EVENTS_TO_CREATE))).Documents.ToList();
        Assert.Equal(NUMBER_OF_EVENTS_TO_CREATE, events.Count);
        events.ForEach(e =>
        {
            var ri = e.GetRequestInfo(_serializer, _logger);
            Assert.NotNull(ri);
            Assert.Equal(_clientIpAddress, ri.ClientIpAddress);
        });

        await _repository.RemoveAllAsync(TestConstants.OrganizationId, _clientIpAddress, DateTime.UtcNow.SubtractDays(3), DateTime.UtcNow.AddDays(2), o => o.ImmediateConsistency());

        events = (await _repository.GetByProjectIdAsync(TestConstants.ProjectId, o => o.PageLimit(NUMBER_OF_EVENTS_TO_CREATE))).Documents.ToList();
        Assert.Empty(events);
    }

    private async Task CreateDataAsync()
    {
        var baseDate = DateTime.UtcNow.SubtractHours(1);
        var occurrenceDateStart = baseDate.AddMinutes(-30);
        var occurrenceDateMid = baseDate;
        var occurrenceDateEnd = baseDate.AddMinutes(30);

        await _stackRepository.AddAsync(_stackData.GenerateStack(id: TestConstants.StackId, organizationId: TestConstants.OrganizationId, projectId: TestConstants.ProjectId), o => o.ImmediateConsistency());

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
            var ev = await _repository.AddAsync(_eventData.GenerateEvent(projectId: TestConstants.ProjectId, organizationId: TestConstants.OrganizationId, stackId: TestConstants.StackId, occurrenceDate: date), o => o.ImmediateConsistency());
            _ids.Add(Tuple.Create(ev.Id, date));
        }
    }

    [Fact]
    public async Task GetDistinctStackIds_WithMultipleStacks_ReturnsAllUniqueIds()
    {
        // Arrange
        var stack1 = await _stackRepository.AddAsync(_stackData.GenerateStack(generateId: true, organizationId: TestConstants.OrganizationId, projectId: TestConstants.ProjectId), o => o.ImmediateConsistency());
        var stack2 = await _stackRepository.AddAsync(_stackData.GenerateStack(generateId: true, organizationId: TestConstants.OrganizationId, projectId: TestConstants.ProjectId), o => o.ImmediateConsistency());

        await _repository.AddAsync(_eventData.GenerateEvents(5, TestConstants.OrganizationId, TestConstants.ProjectId, stack1.Id), o => o.ImmediateConsistency());
        await _repository.AddAsync(_eventData.GenerateEvents(3, TestConstants.OrganizationId, TestConstants.ProjectId, stack2.Id), o => o.ImmediateConsistency());

        // Act
        var afterKey = new CompositeKeyResult();
        var stackIds = await _repository.GetDistinctStackIdsAsync(10000, afterKey);

        // Assert
        Assert.Equal(stackIds.Count, stackIds.Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(stack1.Id, stackIds);
        Assert.Contains(stack2.Id, stackIds);
    }

    [Fact]
    public async Task GetDistinctStackIds_WithPagination_ReturnsAllIds()
    {
        // Arrange
        var stacks = new List<Stack>();
        for (int i = 0; i < 5; i++)
            stacks.Add(await _stackRepository.AddAsync(_stackData.GenerateStack(generateId: true, organizationId: TestConstants.OrganizationId, projectId: TestConstants.ProjectId), o => o.ImmediateConsistency()));

        foreach (var stack in stacks)
            await _repository.AddAsync(_eventData.GenerateEvents(2, TestConstants.OrganizationId, TestConstants.ProjectId, stack.Id), o => o.ImmediateConsistency());

        // Act - page through with batch size of 2
        var allIds = new List<string>();
        var afterKey = new CompositeKeyResult();
        IReadOnlyCollection<string> batch;
        do
        {
            batch = await _repository.GetDistinctStackIdsAsync(2, afterKey);
            allIds.AddRange(batch);
        } while (afterKey.AfterKey.Count > 0);

        // Assert
        Assert.Equal(allIds.Count, allIds.Distinct(StringComparer.Ordinal).Count());
        foreach (var stack in stacks)
            Assert.Contains(stack.Id, allIds);
    }

    [Fact]
    public async Task ReassignStack_WithSourceEvents_MovesAllEventsToTarget()
    {
        // Arrange
        var stack1 = await _stackRepository.AddAsync(_stackData.GenerateStack(generateId: true, organizationId: TestConstants.OrganizationId, projectId: TestConstants.ProjectId), o => o.ImmediateConsistency());
        var stack2 = await _stackRepository.AddAsync(_stackData.GenerateStack(generateId: true, organizationId: TestConstants.OrganizationId, projectId: TestConstants.ProjectId), o => o.ImmediateConsistency());

        await _repository.AddAsync(_eventData.GenerateEvents(10, TestConstants.OrganizationId, TestConstants.ProjectId, stack1.Id), o => o.ImmediateConsistency());
        await _repository.AddAsync(_eventData.GenerateEvents(5, TestConstants.OrganizationId, TestConstants.ProjectId, stack2.Id), o => o.ImmediateConsistency());

        // Act
        long affected = await _repository.ReassignStackAsync([stack1.Id], stack2.Id);

        // Assert
        Assert.Equal(10, affected);

        await RefreshDataAsync();

        Assert.Equal(0, await _repository.CountAsync(q => q.Stack(stack1.Id)));
        Assert.Equal(15, await _repository.CountAsync(q => q.Stack(stack2.Id)));
    }

    [Fact]
    public async Task RemoveAllByProjectIds_WithMatchingEvents_RemovesAll()
    {
        // Arrange
        var stack = await _stackRepository.AddAsync(_stackData.GenerateStack(generateId: true, organizationId: TestConstants.OrganizationId, projectId: TestConstants.ProjectId), o => o.ImmediateConsistency());
        await _repository.AddAsync(_eventData.GenerateEvents(10, TestConstants.OrganizationId, TestConstants.ProjectId, stack.Id), o => o.ImmediateConsistency());

        // Act
        long removed = await _repository.RemoveAllByProjectIdsAsync([TestConstants.ProjectId]);

        // Assert
        Assert.Equal(10, removed);

        await RefreshDataAsync();
        Assert.Equal(0, await _repository.CountAsync(o => o.IncludeSoftDeletes()));
    }

    [Fact]
    public async Task RemoveAllByOrganizationIds_WithMatchingEvents_RemovesAll()
    {
        // Arrange
        var stack = await _stackRepository.AddAsync(_stackData.GenerateStack(generateId: true, organizationId: TestConstants.OrganizationId, projectId: TestConstants.ProjectId), o => o.ImmediateConsistency());
        await _repository.AddAsync(_eventData.GenerateEvents(10, TestConstants.OrganizationId, TestConstants.ProjectId, stack.Id), o => o.ImmediateConsistency());

        // Act
        long removed = await _repository.RemoveAllByOrganizationIdsAsync([TestConstants.OrganizationId]);

        // Assert
        Assert.Equal(10, removed);

        await RefreshDataAsync();
        Assert.Equal(0, await _repository.CountAsync(o => o.IncludeSoftDeletes()));
    }

    [Fact]
    public async Task RemoveAllByStackIds_WithMatchingEvents_RemovesAll()
    {
        // Arrange
        var stack = await _stackRepository.AddAsync(_stackData.GenerateStack(generateId: true, organizationId: TestConstants.OrganizationId, projectId: TestConstants.ProjectId), o => o.ImmediateConsistency());
        await _repository.AddAsync(_eventData.GenerateEvents(10, TestConstants.OrganizationId, TestConstants.ProjectId, stack.Id), o => o.ImmediateConsistency());

        // Act
        long removed = await _repository.RemoveAllByStackIdsAsync([stack.Id]);

        // Assert
        Assert.Equal(10, removed);

        await RefreshDataAsync();
        Assert.Equal(0, await _repository.CountAsync(o => o.IncludeSoftDeletes()));
    }

    [Fact]
    public async Task ReassignStack_WithEmptySourceIds_ReturnsZeroWithoutModification()
    {
        // Arrange
        var stack1 = await _stackRepository.AddAsync(_stackData.GenerateStack(generateId: true, organizationId: TestConstants.OrganizationId, projectId: TestConstants.ProjectId), o => o.ImmediateConsistency());
        var stack2 = await _stackRepository.AddAsync(_stackData.GenerateStack(generateId: true, organizationId: TestConstants.OrganizationId, projectId: TestConstants.ProjectId), o => o.ImmediateConsistency());
        await _repository.AddAsync(_eventData.GenerateEvents(10, TestConstants.OrganizationId, TestConstants.ProjectId, stack1.Id), o => o.ImmediateConsistency());

        // Act - empty source list must be a no-op; an unchecked empty .Stack() filter would patch ALL events
        long affected = await _repository.ReassignStackAsync([], stack2.Id);

        // Assert
        Assert.Equal(0, affected);

        await RefreshDataAsync();
        Assert.Equal(10, await _repository.CountAsync(q => q.Stack(stack1.Id)));
        Assert.Equal(0, await _repository.CountAsync(q => q.Stack(stack2.Id)));
    }

    [Fact]
    public async Task GetDistinctProjectIds_WithMultipleProjects_ReturnsAllUniqueIds()
    {
        // Arrange
        var stack = await _stackRepository.AddAsync(_stackData.GenerateStack(generateId: true, organizationId: TestConstants.OrganizationId, projectId: TestConstants.ProjectId), o => o.ImmediateConsistency());
        string project2Id = ObjectId.GenerateNewId().ToString();

        await _repository.AddAsync(_eventData.GenerateEvents(3, TestConstants.OrganizationId, TestConstants.ProjectId, stack.Id), o => o.ImmediateConsistency());
        await _repository.AddAsync(_eventData.GenerateEvents(2, TestConstants.OrganizationId, project2Id, stack.Id), o => o.ImmediateConsistency());

        // Act
        var afterKey = new CompositeKeyResult();
        var projectIds = await _repository.GetDistinctProjectIdsAsync(10000, afterKey);

        // Assert
        Assert.Equal(projectIds.Count, projectIds.Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(TestConstants.ProjectId, projectIds);
        Assert.Contains(project2Id, projectIds);
    }

    [Fact]
    public async Task GetDistinctOrganizationIds_WithMultipleOrganizations_ReturnsAllUniqueIds()
    {
        // Arrange
        var stack = await _stackRepository.AddAsync(_stackData.GenerateStack(generateId: true, organizationId: TestConstants.OrganizationId, projectId: TestConstants.ProjectId), o => o.ImmediateConsistency());
        string org2Id = ObjectId.GenerateNewId().ToString();

        await _repository.AddAsync(_eventData.GenerateEvents(3, TestConstants.OrganizationId, TestConstants.ProjectId, stack.Id), o => o.ImmediateConsistency());
        await _repository.AddAsync(_eventData.GenerateEvents(2, org2Id, TestConstants.ProjectId, stack.Id), o => o.ImmediateConsistency());

        // Act
        var afterKey = new CompositeKeyResult();
        var orgIds = await _repository.GetDistinctOrganizationIdsAsync(10000, afterKey);

        // Assert
        Assert.Equal(orgIds.Count, orgIds.Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(TestConstants.OrganizationId, orgIds);
        Assert.Contains(org2Id, orgIds);
    }
}
