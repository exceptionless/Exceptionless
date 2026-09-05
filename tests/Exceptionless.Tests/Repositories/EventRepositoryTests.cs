using System.Diagnostics;
using Exceptionless.Core;
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
    private readonly AppOptions _appOptions;
    private readonly Exceptionless.Helpers.RandomEventGenerator _randomEventGenerator;
    private readonly EventData _eventData;
    private readonly IEventRepository _repository;
    private readonly StackData _stackData;
    private readonly IStackRepository _stackRepository;
    private readonly ITextSerializer _serializer;

    public EventRepositoryTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _appOptions = GetService<AppOptions>();
        _randomEventGenerator = GetService<Exceptionless.Helpers.RandomEventGenerator>();
        _eventData = GetService<EventData>();
        _repository = GetService<IEventRepository>();
        _stackData = GetService<StackData>();
        _stackRepository = GetService<IStackRepository>();
        _serializer = GetService<ITextSerializer>();
    }

    [Fact]
    public async Task GetProductTourUsageAsync_AllSourcesOverNinetyDays_PreservesLargeCounts()
    {
        // Arrange
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        string[] sources = ProductTours.Definitions.Values
            .SelectMany(definition => Enumerable.Range(1, definition.CurrentVersion)
                .SelectMany(version => Enum.GetValues<ProductTourTelemetryEvent>()
                    .SelectMany(action => Enum.GetValues<ProductTourLaunchSource>()
                        .Select(source => ProductTours.CreateTelemetrySource(action, definition.Name, version, source)))))
            .ToArray();
        // Include the source bucket and the parser's padded end-date bucket. Catalog/version growth
        // must stay within Elasticsearch's default search.max_buckets before it reaches production.
        int maximumPeriods = 201;
        Assert.InRange((long)sources.Length * (1 + maximumPeriods + 1), 1, 65_536);
        await CreateDataAsync(builder =>
        {
            foreach (string source in sources)
            {
                foreach (var date in new[] { start, start.AddDays(89) })
                {
                    builder.Event().Organization(TestConstants.OrganizationId).Project(_appOptions.InternalProjectId)
                        .Type(Event.KnownTypes.FeatureUsage).Source(source).Date(date)
                        .Mutate(ev => ev.Count = Int32.MaxValue);
                }
            }
        });

        // Act
        var result = await _repository.GetProductTourUsageAsync(_appOptions.InternalProjectId, start, start.AddDays(90));

        // Assert
        Assert.Equal(sources.Length, result.Buckets.Count);
        Assert.All(result.Buckets, bucket =>
        {
            Assert.Equal(2L * Int32.MaxValue, bucket.Count);
            Assert.Equal(bucket.Count, bucket.Activity.Sum(period => period.Count));
            Assert.Equal(2, bucket.Activity.Count(period => period.Count > 0));
            Assert.InRange(bucket.Activity.Count, 2, maximumPeriods);
        });
    }

    [Theory]
    [InlineData(5)]
    [InlineData(180)]
    [InlineData(1095)]
    public async Task GetProductTourUsageAsync_AutomaticInterval_UsesDateFilterAndPreservesEmptyBuckets(int days)
    {
        // Arrange
        var start = new DateTime(2026, 8, 31, 12, 0, 0, DateTimeKind.Utc);
        var end = start.AddDays(days);
        TimeProvider.SetUtcNow(end.AddDays(1));
        await CreateDataAsync(builder =>
        {
            foreach (var date in new[] { start.AddTicks(-1), start, end.AddHours(-1), end })
            {
                builder.Event().Organization(TestConstants.OrganizationId).Project(_appOptions.InternalProjectId)
                    .Type(Event.KnownTypes.FeatureUsage)
                    .Source(ProductTours.CreateTelemetrySource(ProductTourTelemetryEvent.Started, ProductTours.AppOverview, 1, ProductTourLaunchSource.Catalog))
                    .Date(date).Mutate(ev => ev.Count = 3);
            }
        });

        // Act
        var result = await _repository.GetProductTourUsageAsync(_appOptions.InternalProjectId, start, end);

        // Assert
        var bucket = Assert.Single(result.Buckets);
        Assert.Equal(6, bucket.Count);
        Assert.Equal(bucket.Count, bucket.Activity.Sum(period => period.Count));
        Assert.InRange(bucket.Activity.Count, 80, 201);
        Assert.Equal(2, bucket.Activity.Count(period => period.Count > 0));
        Assert.Contains(bucket.Activity, period => period.Count == 0);
        Assert.All(bucket.Activity, period => Assert.True(period.DateUtc < end));
    }

    [Fact]
    public async Task GetProductTourUsageAsync_ActivityAcrossFebruary_PreservesCoalescedCounts()
    {
        // Arrange
        var start = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        await CreateDataAsync(builder =>
        {
            foreach (var action in new[] { ProductTourTelemetryEvent.Started, ProductTourTelemetryEvent.Dismissed })
            {
                builder.Event().Organization(TestConstants.OrganizationId).Project(_appOptions.InternalProjectId)
                    .Type(Event.KnownTypes.FeatureUsage)
                    .Source(ProductTours.CreateTelemetrySource(action, ProductTours.AppOverview, 1, ProductTourLaunchSource.Catalog))
                    .Date(start.AddDays(28))
                    .Mutate(ev => ev.Count = 3);
            }
        });

        // Act
        var result = await _repository.GetProductTourUsageAsync(_appOptions.InternalProjectId, start, start.AddDays(30));

        // Assert
        Assert.Equal(2, result.Buckets.Count);
        Assert.All(result.Buckets, bucket =>
        {
            Assert.Equal(3, bucket.Count);
            Assert.InRange(Assert.Single(bucket.Activity, period => period.Count > 0).DateUtc, start.AddDays(27), start.AddDays(28));
        });
    }

    [Fact]
    public async Task GetProductTourUsageAsync_KnownSources_ReturnsPerTourAggregations()
    {
        // Arrange
        var month = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        await CreateDataAsync(builder =>
        {
            AddProductTourUsage(builder, ProductTours.CreateTelemetrySource(ProductTourTelemetryEvent.Started, ProductTours.AppOverview, 1, ProductTourLaunchSource.Catalog), month.AddDays(1), "user-1", 2);
            AddProductTourUsage(builder, ProductTours.CreateTelemetrySource(ProductTourTelemetryEvent.Started, ProductTours.AppOverview, 1, ProductTourLaunchSource.HelpMenu), month.AddDays(2), "user-1");
            AddProductTourUsage(builder, ProductTours.CreateTelemetrySource(ProductTourTelemetryEvent.Completed, ProductTours.AppOverview, 1, ProductTourLaunchSource.Catalog), month.AddDays(3), "user-2");
            AddProductTourUsage(builder, ProductTours.CreateTelemetrySource(ProductTourTelemetryEvent.Shown, ProductTours.AppWelcome, 1, ProductTourLaunchSource.Welcome), month.AddDays(4), "user-3");
            AddProductTourUsage(builder, "product-tour.started.app-overview.v2.catalog", month.AddDays(5), "user-4");
            AddProductTourUsage(builder, "product-tour.started.unknown-tour.v1.catalog", month.AddDays(6), "user-5");
            AddProductTourUsage(builder, ProductTours.CreateTelemetrySource(ProductTourTelemetryEvent.Started, ProductTours.AppOverview, 1, ProductTourLaunchSource.Catalog), month.AddMonths(-1), "user-6");
            builder.Event()
                .Organization(TestConstants.OrganizationId)
                .Project(_appOptions.InternalProjectId)
                .Type(Event.KnownTypes.FeatureUsage)
                .Source(ProductTours.CreateTelemetrySource(ProductTourTelemetryEvent.Started, ProductTours.AppOverview, 1, ProductTourLaunchSource.Catalog))
                .Date(month.AddDays(8))
                .UserIdentity("user-8");
            builder.Event()
                .TestProject()
                .Type(Event.KnownTypes.FeatureUsage)
                .Source(ProductTours.CreateTelemetrySource(ProductTourTelemetryEvent.Started, ProductTours.AppOverview, 1, ProductTourLaunchSource.Catalog))
                .Date(month.AddDays(7))
                .UserIdentity("user-7");
        });

        // Act
        var result = await _repository.GetProductTourUsageAsync(_appOptions.InternalProjectId, month, month.AddMonths(1));

        // Assert
        Assert.Equal(2, result.Buckets.Select(bucket => bucket.Source.TourName).Distinct(StringComparer.Ordinal).Count());
        var overview = result.Buckets.Where(bucket => String.Equals(bucket.Source.TourName, ProductTours.AppOverview, StringComparison.Ordinal)).ToArray();
        Assert.Equal(4, overview.Where(bucket => bucket.Source.Event == ProductTourTelemetryEvent.Started).Sum(bucket => bucket.Count));
        Assert.Equal(1, overview.Where(bucket => bucket.Source.Event == ProductTourTelemetryEvent.Completed).Sum(bucket => bucket.Count));
        Assert.Equal(month.AddDays(8), overview.Max(bucket => bucket.LastUtc));

        var welcome = Assert.Single(result.Buckets, bucket => String.Equals(bucket.Source.TourName, ProductTours.AppWelcome, StringComparison.Ordinal));
        Assert.Equal(1, welcome.Count);

        Assert.All(result.Buckets, item => Assert.True(ProductTours.IsValid(item.Source.TourName, item.Source.Version)));
        Assert.All(result.Buckets, bucket => Assert.Equal(bucket.Count, bucket.Activity.Sum(period => period.Count)));
        var catalogStarts = Assert.Single(overview, bucket => bucket.Source.Event == ProductTourTelemetryEvent.Started && bucket.Source.LaunchSource == ProductTourLaunchSource.Catalog);
        Assert.InRange(Assert.Single(catalogStarts.Activity, period => period.Count == 2).DateUtc, month, month.AddDays(1));
    }

    [Fact]
    public async Task GetProductTourUsageAsync_WithoutDates_ReturnsAllUsage()
    {
        // Arrange
        var month = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        await CreateDataAsync(builder =>
        {
            AddProductTourUsage(builder, ProductTours.CreateTelemetrySource(ProductTourTelemetryEvent.Started, ProductTours.AppOverview, 1, ProductTourLaunchSource.Catalog), month.AddMonths(-1), "user-1");
            AddProductTourUsage(builder, ProductTours.CreateTelemetrySource(ProductTourTelemetryEvent.Started, ProductTours.AppOverview, 1, ProductTourLaunchSource.Welcome), month.AddDays(1), "user-2");
        });

        // Act
        var result = await _repository.GetProductTourUsageAsync(_appOptions.InternalProjectId, null, month.AddMonths(1));

        // Assert
        Assert.Equal(2, result.Buckets.Sum(bucket => bucket.Count));
        Assert.Contains(result.Buckets.SelectMany(bucket => bucket.Activity), period => period.DateUtc <= month.AddMonths(-1) && period.Count == 1);
    }

    [Fact]
    public async Task GetProductTourUsageAsync_History_UsesRetainedActivityBoundsForAutomaticBuckets()
    {
        // Arrange
        var first = new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);
        var end = first.AddDays(7);
        TimeProvider.SetUtcNow(end);
        string source = ProductTours.CreateTelemetrySource(ProductTourTelemetryEvent.Started, ProductTours.AppOverview, 1, ProductTourLaunchSource.Catalog);
        await CreateDataAsync(builder =>
        {
            AddProductTourUsage(builder, source, first, "first");
            AddProductTourUsage(builder, source, end.AddSeconds(-1), "last");
        });

        // Act
        var result = await _repository.GetProductTourUsageAsync(_appOptions.InternalProjectId, null, end);

        // Assert
        var bucket = Assert.Single(result.Buckets);
        Assert.Equal(2, bucket.Count);
        Assert.Equal(bucket.Count, bucket.Activity.Sum(period => period.Count));
        Assert.InRange(bucket.Activity.Count, 80, 201);
        Assert.All(bucket.Activity, period => Assert.InRange(period.DateUtc, first.AddDays(-1), end));
    }

    [Fact]
    public async Task GetProductTourUsageAsync_EmptyHistory_DoesNotInventDateBounds()
    {
        // Act
        var result = await _repository.GetProductTourUsageAsync(_appOptions.InternalProjectId, null, TimeProvider.GetUtcNow().UtcDateTime);

        // Assert
        Assert.Empty(result.Buckets);
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

    private void AddProductTourUsage(DataBuilder builder, string source, DateTime dateUtc, string userIdentity, int count = 1)
    {
        builder.Event()
            .Organization(TestConstants.OrganizationId)
            .Project(_appOptions.InternalProjectId)
            .Type(Event.KnownTypes.FeatureUsage)
            .Source(source)
            .Date(dateUtc)
            .UserIdentity(userIdentity)
            .Mutate(ev => ev.Count = count);
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
}
