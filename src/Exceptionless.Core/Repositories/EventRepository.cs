using Elastic.Clients.Elasticsearch.Aggregations;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Repositories.Queries;
using Exceptionless.Core.Validation;
using Exceptionless.DateTimeExtensions;
using Foundatio.Repositories;
using Foundatio.Repositories.Elasticsearch.Extensions;
using Foundatio.Repositories.Exceptions;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Repositories;

public class EventRepository : RepositoryOwnedByOrganizationAndProject<PersistentEvent>, IEventRepository
{
    private static readonly string[] _productTourEvents = ["completed", "dismissed", "shown", "started"];
    private static readonly string[] _productTourLaunchSources = ["automatic", "catalog", "command-palette", "feature-announcement", "help-menu"];
    private readonly TimeProvider _timeProvider;

    public EventRepository(ExceptionlessElasticConfiguration configuration, AppOptions options, MiniValidationValidator validator)
        : base(configuration.Events, validator, options)
    {
        _timeProvider = configuration.TimeProvider;

        DisableCache(); // NOTE: If cache is ever enabled, then fast paths for patching/deleting with scripts will be super slow!
        BatchNotifications = true;
        DefaultPipeline = "events-pipeline";

        AddDefaultExclude(e => e.Idx);
        // copy to fields
        AddDefaultExclude(EventIndex.Alias.IpAddress);
        AddDefaultExclude(EventIndex.Alias.OperatingSystem);
        AddDefaultExclude(EventIndex.Alias.Error);

        AddRequiredField(e => e.Date);
    }

    public Task<FindResults<PersistentEvent>> GetOpenSessionsAsync(DateTime createdBeforeUtc, CommandOptionsDescriptor<PersistentEvent>? options = null)
    {
        var query = new RepositoryQuery<PersistentEvent>()
            .FieldEquals(e => e.Type, Event.KnownTypes.Session)
            .ElasticFilter(new BoolQuery { MustNot = [new ExistsQuery { Field = $"idx.{Event.KnownDataKeys.SessionEnd}-d" }] });

        if (createdBeforeUtc.Ticks > 0)
            query = query.DateRange(null, createdBeforeUtc, (PersistentEvent e) => e.Date); // No lower bound, upper bound is exclusive

        return FindAsync(q => query.SortDescending(e => e.Date), options);
    }

    /// <summary>
    /// Updates the session start last activity time if the id is a valid session start event.
    /// </summary>
    public async Task<bool> UpdateSessionStartLastActivityAsync(string id, DateTime lastActivityUtc, bool isSessionEnd = false, bool hasError = false, bool sendNotifications = true)
    {
        var ev = await GetByIdAsync(id);
        if (ev is null)
            return false;

        if (!ev.UpdateSessionStart(lastActivityUtc, isSessionEnd))
            return false;

        await SaveAsync(ev, o => o.Notifications(sendNotifications));
        return true;
    }

    public Task<long> RemoveAllAsync(string organizationId, string? clientIpAddress, DateTime? utcStart, DateTime? utcEnd, CommandOptionsDescriptor<PersistentEvent>? options = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(organizationId);

        var query = new RepositoryQuery<PersistentEvent>().Organization(organizationId);
        if (utcStart.HasValue && utcEnd.HasValue)
            query = query.DateRange(utcStart, utcEnd, InferField(e => e.Date)).Index(utcStart, utcEnd);
        else if (utcEnd.HasValue)
            query = query.DateRange(null, utcEnd, (PersistentEvent e) => e.Date);
        else if (utcStart.HasValue)
            query = query.DateRange(utcStart, null, (PersistentEvent e) => e.Date);

        if (!String.IsNullOrEmpty(clientIpAddress))
            query = query.FieldEquals(EventIndex.Alias.IpAddress, clientIpAddress);

        return RemoveAllAsync(q => query, options);
    }

    public Task<FindResults<PersistentEvent>> GetByReferenceIdAsync(string projectId, string referenceId)
    {
        return FindAsync(q => q.Project(projectId).FieldEquals(e => e.ReferenceId, referenceId).SortDescending(e => e.Date), o => o.PageLimit(10));
    }

    public async Task<ProductTourUsageResult> GetProductTourUsageAsync(string projectId, DateTime utcStart, DateTime utcEnd, int recentLimit = 500)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectId);
        ArgumentOutOfRangeException.ThrowIfLessThan(recentLimit, 1);
        if (utcEnd <= utcStart)
            throw new ArgumentOutOfRangeException(nameof(utcEnd), "The end date must be later than the start date.");

        var sourcesByTour = ProductTours.Versions.ToDictionary(
            pair => pair.Key,
            pair => CreateProductTourSources(pair.Key, pair.Value),
            StringComparer.Ordinal);
        var sourcesByName = sourcesByTour.Values
            .SelectMany(sources => sources)
            .ToDictionary(source => source.Raw, StringComparer.Ordinal);
        string[] allSources = sourcesByName.Keys.ToArray();

        var aggregationTask = GetProductTourUsageToursAsync(projectId, utcStart, utcEnd, sourcesByTour, allSources);
        var recentTask = FindAsync(query => ApplyProductTourUsageFilter(query, projectId, utcStart, utcEnd, allSources)
            .SortDescending(ev => ev.Date), options => options.PageLimit(recentLimit));

        await Task.WhenAll(aggregationTask, recentTask);
        var recentEvents = (await recentTask).Documents
            .Select(ev => ev.Source is not null && sourcesByName.TryGetValue(ev.Source, out var source) ? new ProductTourUsageEvent(ev, source) : null)
            .OfType<ProductTourUsageEvent>()
            .ToArray();
        return new ProductTourUsageResult(await aggregationTask, recentEvents);
    }

    private async Task<IReadOnlyCollection<ProductTourUsageTour>> GetProductTourUsageToursAsync(
        string projectId,
        DateTime utcStart,
        DateTime utcEnd,
        IReadOnlyDictionary<string, ProductTourUsageSource[]> sourcesByTour,
        string[] allSources)
    {
        var query = ApplyProductTourUsageFilter(NewQuery(), projectId, utcStart, utcEnd, allSources);
        var options = ConfigureOptions(null);
        await OnBeforeQueryAsync(query, options, typeof(PersistentEvent));
        await RefreshForConsistency(query, options);

        var search = (await CreateSearchDescriptorAsync(query, options))
            .Size(0)
            .Aggregations(CreateProductTourAggregations(sourcesByTour));
        var response = await _client.SearchAsync<PersistentEvent>(search);
        _logger.LogRequest(response);

        if (!response.IsValidResponse)
            throw new DocumentException($"Error getting product tour usage: {response.ElasticsearchServerError?.Error?.Reason}", response.ApiCallDetails.OriginalException);

        var tours = new List<ProductTourUsageTour>();
        foreach ((string tourName, ProductTourUsageSource[] sources) in sourcesByTour)
        {
            if (response.Aggregations is null
                || !response.Aggregations.TryGetValue(tourName, out var aggregate)
                || aggregate is not FilterAggregate filterAggregate
                || filterAggregate.Aggregations is null)
                continue;

            if (!filterAggregate.Aggregations.TryGetValue("sources", out var sourceAggregate) || sourceAggregate is not StringTermsAggregate sourceTerms)
                continue;

            var buckets = new List<ProductTourUsageBucket>();
            foreach (var bucket in sourceTerms.Buckets)
            {
                if (!bucket.Key.TryGetString(out string? sourceValue))
                    continue;

                var source = sources.FirstOrDefault(source => String.Equals(source.Raw, sourceValue, StringComparison.Ordinal));
                if (source is null)
                    continue;

                long count = bucket.Aggregations is { } bucketAggregations
                    && bucketAggregations.TryGetValue("count", out var countAggregate)
                    && countAggregate is SumAggregate sum
                    ? Convert.ToInt64(sum.Value)
                    : bucket.DocCount;
                DateTime? lastUtc = bucket.Aggregations is { } lastAggregations
                    && lastAggregations.TryGetValue("last", out var lastAggregate)
                    && lastAggregate is MaxAggregate max
                    ? max.ValueAsString is null ? null : DateTime.Parse(max.ValueAsString, null, System.Globalization.DateTimeStyles.RoundtripKind)
                    : null;
                buckets.Add(new ProductTourUsageBucket(source, count, lastUtc));
            }

            long uniqueUsers = filterAggregate.Aggregations.TryGetValue("users", out var usersAggregate) && usersAggregate is CardinalityAggregate cardinality
                ? Convert.ToInt64(cardinality.Value)
                : 0;
            if (buckets.Count > 0)
                tours.Add(new ProductTourUsageTour(tourName, uniqueUsers, buckets));
        }

        return tours;
    }

    private IDictionary<string, Aggregation> CreateProductTourAggregations(IReadOnlyDictionary<string, ProductTourUsageSource[]> sourcesByTour)
    {
        string sourceField = ElasticIndex.MappingResolver.GetNonAnalyzedFieldName(InferField(ev => ev.Source))!;
        string userPath = EventIndexExtensions.DataPath<UserInfo>(Event.KnownDataKeys.UserInfo, user => user.Identity);
        string userField = ElasticIndex.MappingResolver.GetNonAnalyzedFieldName(userPath) ?? userPath;
        var aggregations = new Dictionary<string, Aggregation>(StringComparer.Ordinal);

        foreach ((string tourName, ProductTourUsageSource[] sources) in sourcesByTour)
        {
            aggregations[tourName] = new Aggregation
            {
                Filter = new TermsQuery
                {
                    Field = sourceField,
                    Terms = new TermsQueryField(sources.Select(source => (Elastic.Clients.Elasticsearch.FieldValue)source.Raw).ToArray())
                },
                Aggregations = new Dictionary<string, Aggregation>
                {
                    ["sources"] = new Aggregation
                    {
                        Terms = new TermsAggregation { Field = sourceField, Size = sources.Length },
                        Aggregations = new Dictionary<string, Aggregation>
                        {
                            ["count"] = new SumAggregation { Field = InferField(ev => ev.Count), Missing = 1 },
                            ["last"] = new MaxAggregation { Field = InferField(ev => ev.Date) }
                        }
                    },
                    ["users"] = new CardinalityAggregation { Field = userField }
                }
            };
        }

        return aggregations;
    }

    private static IRepositoryQuery<PersistentEvent> ApplyProductTourUsageFilter(
        IRepositoryQuery<PersistentEvent> query,
        string projectId,
        DateTime utcStart,
        DateTime utcEnd,
        string[] sources)
    {
        return query
            .Project(projectId)
            .FieldEquals(ev => ev.Type, Event.KnownTypes.FeatureUsage)
            .FieldEquals(ev => ev.Source, sources)
            .DateRange(utcStart, utcEnd, (PersistentEvent ev) => ev.Date)
            .Index(utcStart, utcEnd);
    }

    private static ProductTourUsageSource[] CreateProductTourSources(string tourName, int currentVersion)
    {
        return Enumerable.Range(1, currentVersion)
            .SelectMany(version => _productTourEvents.SelectMany(eventName => _productTourLaunchSources.Select(launchSource =>
                new ProductTourUsageSource(
                    $"product-tour.{eventName}.{tourName}.v{version}.{launchSource}",
                    eventName,
                    tourName,
                    version,
                    launchSource))))
            .ToArray();
    }

    public async Task<PreviousAndNextEventIdResult> GetPreviousAndNextEventIdsAsync(PersistentEvent ev, AppFilter? systemFilter = null, DateTime? utcStart = null, DateTime? utcEnd = null)
    {
        var previous = GetPreviousEventIdAsync(ev, systemFilter, utcStart, utcEnd);
        var next = GetNextEventIdAsync(ev, systemFilter, utcStart, utcEnd);
        await Task.WhenAll(previous, next);

        return new PreviousAndNextEventIdResult
        {
            Previous = previous.Result,
            Next = next.Result
        };
    }

    private async Task<string?> GetPreviousEventIdAsync(PersistentEvent ev, AppFilter? systemFilter = null, DateTime? utcStart = null, DateTime? utcEnd = null)
    {
        var retentionDate = _options.MaximumRetentionDays > 0 ? _timeProvider.GetUtcNow().UtcDateTime.Date.SubtractDays(_options.MaximumRetentionDays) : DateTime.MinValue;
        if (!utcStart.HasValue || utcStart.Value.IsBefore(retentionDate))
            utcStart = retentionDate;

        if (!utcEnd.HasValue || utcEnd.Value.IsAfter(ev.Date.UtcDateTime))
            utcEnd = ev.Date.UtcDateTime;

        var utcEventDate = ev.Date.UtcDateTime;
        // utcEnd is before the current event date.
        if (utcStart > utcEventDate || utcEnd < utcEventDate)
            return null;

        var results = await FindAsync(q => q
            .DateRange(utcStart, utcEventDate, (PersistentEvent e) => e.Date)
            .Index(utcStart, utcEventDate)
            .SortDescending(e => e.Date)
            .Include(e => e.Id, e => e.Date)
            .AppFilter(systemFilter)
            .Stack(ev.StackId)
            .ExcludedId(ev.Id)
            .EnforceEventStackFilter(false), o => o.PageLimit(10));

        if (results.Total == 0)
            return null;

        // make sure we don't have records with the exact same occurrence date
        if (results.Documents.All(t => t.Date != ev.Date))
            return results.Documents.OrderByDescending(t => t.Date).ThenByDescending(t => t.Id).First().Id;

        // we have records with the exact same occurrence date, we need to figure out the order of those
        // put our target error into the mix, sort it and return the result before the target
        var unionResults = results.Documents.Union([ev])
            .OrderBy(t => t.Date.UtcTicks).ThenBy(t => t.Id)
            .ToList();

        int index = unionResults.FindIndex(t => t.Id == ev.Id);
        return index == 0 ? null : unionResults[index - 1].Id;
    }

    private async Task<string?> GetNextEventIdAsync(PersistentEvent ev, AppFilter? systemFilter = null, DateTime? utcStart = null, DateTime? utcEnd = null)
    {
        if (!utcStart.HasValue || utcStart.Value.IsBefore(ev.Date.UtcDateTime))
            utcStart = ev.Date.UtcDateTime;

        if (!utcEnd.HasValue || utcEnd.Value.IsAfter(_timeProvider.GetUtcNow().UtcDateTime))
            utcEnd = _timeProvider.GetUtcNow().UtcDateTime;

        var utcEventDate = ev.Date.UtcDateTime;
        // utcEnd is before the current event date.
        if (utcStart > utcEventDate || utcEnd < utcEventDate)
            return null;

        var results = await FindAsync(q => q
            .DateRange(utcEventDate, utcEnd, (PersistentEvent e) => e.Date)
            .Index(utcEventDate, utcEnd)
            .SortAscending(e => e.Date)
            .Include(e => e.Id, e => e.Date)
            .AppFilter(systemFilter)
            .Stack(ev.StackId)
            .ExcludedId(ev.Id)
            .EnforceEventStackFilter(false), o => o.PageLimit(10));

        if (results.Total == 0)
            return null;

        // make sure we don't have records with the exact same occurrence date
        if (results.Documents.All(t => t.Date != ev.Date))
            return results.Documents.OrderBy(t => t.Date).ThenBy(t => t.Id).First().Id;

        // we have records with the exact same occurrence date, we need to figure out the order of those
        // put our target error into the mix, sort it and return the result after the target
        var unionResults = results.Documents.Union([ev])
            .OrderBy(t => t.Date.Ticks).ThenBy(t => t.Id)
            .ToList();

        int index = unionResults.FindIndex(t => t.Id == ev.Id);
        return index == unionResults.Count - 1 ? null : unionResults[index + 1].Id;
    }

    public override Task<FindResults<PersistentEvent>> GetByOrganizationIdAsync(string organizationId, CommandOptionsDescriptor<PersistentEvent>? options = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(organizationId);

        return FindAsync(q => q.Organization(organizationId).SortDescending(e => e.Date).SortDescending(e => e.Id), options);
    }

    public override Task<FindResults<PersistentEvent>> GetByProjectIdAsync(string projectId, CommandOptionsDescriptor<PersistentEvent>? options = null)
    {
        return FindAsync(q => q.Project(projectId).SortDescending(e => e.Date).SortDescending(e => e.Id), options);
    }

    public Task<long> RemoveAllByStackIdsAsync(string[] stackIds)
    {
        ArgumentNullException.ThrowIfNull(stackIds);
        if (stackIds.Length == 0)
            throw new ArgumentOutOfRangeException(nameof(stackIds));

        return RemoveAllAsync(q => q.Stack(stackIds));
    }
}
