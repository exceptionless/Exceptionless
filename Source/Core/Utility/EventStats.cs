using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Elasticsearch.Net;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Filter;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Stats;
using Foundatio.Logging;
using Foundatio.Repositories.Elasticsearch.Extensions;
using Foundatio.Repositories.Elasticsearch.Queries;
using Foundatio.Repositories.Elasticsearch.Queries.Builders;
using Foundatio.Repositories.Queries;
using Nest;

namespace Exceptionless.Core.Utility {
    public class EventStats {
        private readonly ExceptionlessElasticConfiguration _configuration;
        private readonly ILogger _logger;

        public EventStats(ExceptionlessElasticConfiguration configuration, ILogger<EventStats> logger) {
            _configuration = configuration;
            _logger = logger;
        }
 
        public async Task<NumbersStatsResult> GetNumbersStatsAsync(IEnumerable<FieldAggregation> fields, DateTime utcStart, DateTime utcEnd, IRepositoryQuery systemFilter, string userFilter = null, TimeSpan? displayTimeOffset = null) {
            if (!displayTimeOffset.HasValue)
                displayTimeOffset = TimeSpan.Zero;

            var filter = new ElasticQuery()
                .WithSystemFilter(systemFilter)
                .WithFilter(userFilter)
                .WithDateRange(utcStart, utcEnd, EventIndexType.Fields.Date)
                .WithIndexes(utcStart, utcEnd);

            // if no start date then figure out first event date
            if (!filter.DateRanges.First().UseStartDate)
                await UpdateFilterStartDateRangesAsync(filter, utcEnd).AnyContext();
            
            utcStart = filter.DateRanges.First().GetStartDate();
            utcEnd = filter.DateRanges.First().GetEndDate();
            
            var descriptor = new SearchDescriptor<PersistentEvent>()
                .SearchType(SearchType.Count)
                .IgnoreUnavailable()
                .Index(filter.Indexes.Count > 0 ? String.Join(",", filter.Indexes) : _configuration.Events.Name)
                .Type(_configuration.Events.Event.Name)
                .Aggregations(agg => BuildAggregations(agg, fields));

            _configuration.Events.Event.QueryBuilder.ConfigureSearch(filter, null, descriptor);
            var response = await _configuration.Client.SearchAsync<PersistentEvent>(descriptor).AnyContext();
            _logger.Trace(() => response.GetRequest());

            if (!response.IsValid) {
                string message = $"Retrieving stats failed: {response.GetErrorMessage()}";
                _logger.Error().Exception(response.ConnectionStatus.OriginalException).Message(message).Property("request", response.GetRequest()).Write();
                throw new ApplicationException(message, response.ConnectionStatus.OriginalException);
            }

            return new NumbersStatsResult {
                Total = response.Total,
                Start = utcStart.SafeAdd(displayTimeOffset.Value),
                End = utcEnd.SafeAdd(displayTimeOffset.Value),
                Numbers = GetNumbers(response.Aggs, fields)
            };
        }
        
        public async Task<NumbersTermStatsResult> GetNumbersTermsStatsAsync(string term, IEnumerable<FieldAggregation> fields, DateTime utcStart, DateTime utcEnd, IRepositoryQuery systemFilter, string userFilter = null, TimeSpan? displayTimeOffset = null, int max = 25) {
            var allowedTerms = new[] { "organization_id", "project_id", "stack_id", "tags", "version" };
            if (!allowedTerms.Contains(term))
                throw new ArgumentException("Must be a valid term.", nameof(term));

            if (!displayTimeOffset.HasValue)
                displayTimeOffset = TimeSpan.Zero;

            var filter = new ElasticQuery()
                .WithSystemFilter(systemFilter)
                .WithFilter(userFilter)
                .WithDateRange(utcStart, utcEnd, EventIndexType.Fields.Date)
                .WithIndexes(utcStart, utcEnd);

            // if no start date then figure out first event date
            if (!filter.DateRanges.First().UseStartDate)
                await UpdateFilterStartDateRangesAsync(filter, utcEnd).AnyContext();

            utcStart = filter.DateRanges.First().GetStartDate();
            utcEnd = filter.DateRanges.First().GetEndDate();
            
            var descriptor = new SearchDescriptor<PersistentEvent>()
                .SearchType(SearchType.Count)
                .IgnoreUnavailable()
                .Index(filter.Indexes.Count > 0 ? String.Join(",", filter.Indexes) : _configuration.Events.Name)
                .Type(_configuration.Events.Event.Name)
                .Aggregations(agg => BuildAggregations(agg
                    .Terms("terms", t => BuildTermSort(t
                        .Field(term)
                        .Size(max)
                        .Aggregations(agg2 => BuildAggregations(agg2
                            .Min("first_occurrence", o => o.Field(ev => ev.Date))
                            .Max("last_occurrence", o => o.Field(ev => ev.Date)), fields)
                        ), fields)
                    ), fields)
                );

            _configuration.Events.Event.QueryBuilder.ConfigureSearch(filter, null, descriptor);
            var response = await _configuration.Client.SearchAsync<PersistentEvent>(descriptor).AnyContext();
            _logger.Trace(() => response.GetRequest());

            if (!response.IsValid) {
                string message = $"Retrieving stats failed: {response.GetErrorMessage()}";
                _logger.Error().Exception(response.ConnectionStatus.OriginalException).Message(message).Property("request", response.GetRequest()).Write();
                throw new ApplicationException(message, response.ConnectionStatus.OriginalException);
            }

            var stats = new NumbersTermStatsResult {
                Total = response.Total,
                Start = utcStart.SafeAdd(displayTimeOffset.Value),
                End = utcEnd.SafeAdd(displayTimeOffset.Value),
                Numbers = GetNumbers(response.Aggs, fields)
            };

            var terms = response.Aggs.Terms("terms");
            if (terms != null) {
                stats.Terms.AddRange(terms.Items.Select(i => {
                    var item = new NumbersTermStatsItem {
                        Total = i.DocCount,
                        Term = i.Key,
                        Numbers = GetNumbers(i, fields)
                    };

                    var termFirstOccurrence = i.Min("first_occurrence");
                    if (termFirstOccurrence?.Value != null)
                        item.FirstOccurrence = termFirstOccurrence.Value.Value.ToDateTime().SafeAdd(displayTimeOffset.Value);

                    var termLastOccurrence = i.Max("last_occurrence");
                    if (termLastOccurrence?.Value != null)
                        item.LastOccurrence = termLastOccurrence.Value.Value.ToDateTime().SafeAdd(displayTimeOffset.Value);

                    return item;
                }));
            }

            return stats;
        }
        
        public async Task<NumbersTimelineStatsResult> GetNumbersTimelineStatsAsync(IEnumerable<FieldAggregation> fields, DateTime utcStart, DateTime utcEnd, IRepositoryQuery systemFilter, string userFilter = null, TimeSpan? displayTimeOffset = null, int desiredDataPoints = 100) {
            if (!displayTimeOffset.HasValue)
                displayTimeOffset = TimeSpan.Zero;

            var filter = new ElasticQuery()
                .WithSystemFilter(systemFilter)
                .WithFilter(userFilter)
                .WithDateRange(utcStart, utcEnd, EventIndexType.Fields.Date)
                .WithIndexes(utcStart, utcEnd);

            // if no start date then figure out first event date
            if (!filter.DateRanges.First().UseStartDate)
                await UpdateFilterStartDateRangesAsync(filter, utcEnd).AnyContext();

            utcStart = filter.DateRanges.First().GetStartDate();
            utcEnd = filter.DateRanges.First().GetEndDate();
            var interval = GetInterval(utcStart, utcEnd, desiredDataPoints);
            
            var descriptor = new SearchDescriptor<PersistentEvent>()
                .SearchType(SearchType.Count)
                .IgnoreUnavailable()
                .Index(filter.Indexes.Count > 0 ? String.Join(",", filter.Indexes) : _configuration.Events.Name)
                .Type(_configuration.Events.Event.Name)
                .Aggregations(agg => BuildAggregations(agg
                    .DateHistogram("timelime", t => t
                        .Field(ev => ev.Date)
                        .MinimumDocumentCount(0)
                        .Interval(interval.Item1)
                        .TimeZone(HoursAndMinutes(displayTimeOffset.Value))
                        .Aggregations(agg2 => BuildAggregations(agg2, fields))
                    )
                    .Min("first_occurrence", t => t.Field(ev => ev.Date))
                    .Max("last_occurrence", t => t.Field(ev => ev.Date)), fields)
                );

            _configuration.Events.Event.QueryBuilder.ConfigureSearch(filter, null, descriptor);
            var response = await _configuration.Client.SearchAsync<PersistentEvent>(descriptor).AnyContext();
            _logger.Trace(() => response.GetRequest());

            if (!response.IsValid) {
                string message = $"Retrieving stats failed: {response.GetErrorMessage()}";
                _logger.Error().Exception(response.ConnectionStatus.OriginalException).Message(message).Property("request", response.GetRequest()).Write();
                throw new ApplicationException(message, response.ConnectionStatus.OriginalException);
            }
            
            var stats = new NumbersTimelineStatsResult { Total = response.Total, Numbers = GetNumbers(response.Aggs, fields) };
            var timeline = response.Aggs.DateHistogram("timelime");
            if (timeline != null) {
                stats.Timeline.AddRange(timeline.Items.Select(i => new NumbersTimelineItem {
                    Date = i.Date,
                    Total = i.DocCount,
                    Numbers = GetNumbers(i, fields)
                }));
            }

            stats.Start = stats.Timeline.Count > 0 ? stats.Timeline.Min(tl => tl.Date).SafeAdd(displayTimeOffset.Value) : utcStart.SafeAdd(displayTimeOffset.Value);
            stats.End = utcEnd.SafeAdd(displayTimeOffset.Value);

            var totalHours = stats.End.Subtract(stats.Start).TotalHours;
            if (totalHours > 0.0)
                stats.AvgPerHour = stats.Total / totalHours;

            if (stats.Timeline.Count <= 0)
                return stats;

            var firstOccurrence = response.Aggs.Min("first_occurrence");
            if (firstOccurrence?.Value != null)
                stats.FirstOccurrence = firstOccurrence.Value.Value.ToDateTime().SafeAdd(displayTimeOffset.Value);

            var lastOccurrence = response.Aggs.Max("last_occurrence");
            if (lastOccurrence?.Value != null)
                stats.LastOccurrence = lastOccurrence.Value.Value.ToDateTime().SafeAdd(displayTimeOffset.Value);

            return stats;
        }
        
        private AggregationDescriptor<PersistentEvent> BuildAggregations(AggregationDescriptor<PersistentEvent> aggregation, IEnumerable<FieldAggregation> fields) {
            foreach (var field in fields) {
                switch (field.Type) {
                    case FieldAggregationType.Average:
                        aggregation.Average(field.Key, a => field.DefaultValueScript == null ? a.Field(field.Field) : a.Script(field.DefaultValueScript));
                        break;
                    case FieldAggregationType.Distinct:
                        aggregation.Cardinality(field.Key, a => (field.DefaultValueScript == null ? a.Field(field.Field) : a.Script(field.DefaultValueScript)).PrecisionThreshold(100));
                        break;
                    case FieldAggregationType.Sum:
                        aggregation.Sum(field.Key, a => field.DefaultValueScript == null ? a.Field(field.Field) : a.Script(field.DefaultValueScript));
                        break;
                    case FieldAggregationType.Min:
                        aggregation.Min(field.Key, a => field.DefaultValueScript == null ? a.Field(field.Field) : a.Script(field.DefaultValueScript));
                        break;
                    case FieldAggregationType.Max:
                        aggregation.Max(field.Key, a => field.DefaultValueScript == null ? a.Field(field.Field) : a.Script(field.DefaultValueScript));
                        break;
                    case FieldAggregationType.Last:
                        // TODO: Populate with the last value.
                        break;
                    case FieldAggregationType.Term:
                        var termField = field as TermFieldAggregation;
                        if (termField == null)
                            throw new InvalidOperationException("term aggregation must be of type TermFieldAggregation");

                        aggregation.Terms(field.Key, t => {
                            var tad = t.Field(field.Field);
                            if (!String.IsNullOrEmpty(termField.ExcludePattern))
                                tad.Exclude(termField.ExcludePattern);

                            if (!String.IsNullOrEmpty(termField.IncludePattern))
                                tad.Include(termField.IncludePattern);

                            return tad;
                        });
                        break;
                    default:
                        throw new InvalidOperationException($"Unknown FieldAggregation type: {field.Type}");
                }
            }

            return aggregation;
        }

        private TermsAggregationDescriptor<PersistentEvent> BuildTermSort(TermsAggregationDescriptor<PersistentEvent> aggregations, IEnumerable<FieldAggregation> fields) {
            var field = fields.FirstOrDefault(f => f.SortOrder.HasValue);
            if (field?.SortOrder == null)
                return aggregations;
            
            return field.SortOrder.Value == Foundatio.Repositories.Models.SortOrder.Ascending ? aggregations.OrderAscending(field.Key) : aggregations.OrderDescending(field.Key);
        }

        private double[] GetNumbers(AggregationsHelper aggregations, IEnumerable<FieldAggregation> fields) {
            var results = new List<double>();
            foreach (var field in fields) {
                switch (field.Type) {
                    case FieldAggregationType.Average:
                        results.Add(aggregations.Average(field.Key)?.Value.GetValueOrDefault() ?? 0);
                        break;
                    case FieldAggregationType.Distinct:
                        results.Add(aggregations.Cardinality(field.Key)?.Value.GetValueOrDefault() ?? 0);
                        break;
                    case FieldAggregationType.Sum:
                        results.Add(aggregations.Sum(field.Key)?.Value.GetValueOrDefault() ?? 0);
                        break;
                    case FieldAggregationType.Min:
                        results.Add(aggregations.Min(field.Key)?.Value.GetValueOrDefault() ?? 0);
                        break;
                    case FieldAggregationType.Max:
                        results.Add(aggregations.Max(field.Key)?.Value.GetValueOrDefault() ?? 0);
                        break;
                    case FieldAggregationType.Last:
                        // TODO: Populate with the last value.
                        break;
                    case FieldAggregationType.Term:
                        var termResult = aggregations.Terms(field.Key);
                        results.Add(termResult?.Items.Count > 0 ? termResult.Items[0].DocCount : 0);
                        break;
                    default:
                        throw new InvalidOperationException($"Unknown FieldAggregation type: {field.Type}");
                }
            }

            return results.ToArray();
        }

        private async Task UpdateFilterStartDateRangesAsync(ElasticQuery filter, DateTime utcEnd) {
            // TODO: Cache this to save an extra search request when a date range isn't filtered.
            var descriptor = new SearchDescriptor<PersistentEvent>()
                .IgnoreUnavailable()
                .Index(filter.Indexes.Count > 0 ? String.Join(",", filter.Indexes) : _configuration.Events.Name)
                .Type(_configuration.Events.Event.Name)
                .SortAscending(ev => ev.Date)
                .Take(1);

            _configuration.Events.Event.QueryBuilder.ConfigureSearch(filter, null, descriptor);
            var response = await _configuration.Client.SearchAsync<PersistentEvent>(descriptor).AnyContext();
            _logger.Trace(() => response.GetRequest());

            if (!response.IsValid) {
                string message = $"Error getting updated start date: {response.GetErrorMessage()}";
                _logger.Error().Exception(response.ConnectionStatus.OriginalException).Message(message).Property("request", response.GetRequest()).Write();
                throw new ApplicationException(message, response.ConnectionStatus.OriginalException);
            }
            
            var firstEvent = response.Hits.FirstOrDefault()?.Source;
            if (firstEvent != null) {
                filter.DateRanges.Clear();
                filter.WithDateRange(firstEvent.Date.UtcDateTime, utcEnd, EventIndexType.Fields.Date);
                filter.Indexes.Clear();
                filter.WithIndexes(firstEvent.Date.UtcDateTime, utcEnd);
            }
        }

        private string HoursAndMinutes(TimeSpan ts) {
            return (ts < TimeSpan.Zero ? "-" : "") + ts.ToString("hh\\:mm");
        }

        private Tuple<string, TimeSpan> GetInterval(DateTime utcStart, DateTime utcEnd, int desiredDataPoints = 100) {
            string interval;
            var totalTime = utcEnd - utcStart;

            var timePerBlock = TimeSpan.FromMinutes(totalTime.TotalMinutes / desiredDataPoints);
            if (timePerBlock.TotalDays > 1) {
                timePerBlock = timePerBlock.Round(TimeSpan.FromDays(1));
                interval = $"{timePerBlock.TotalDays.ToString("0")}d";
            } else if (timePerBlock.TotalHours > 1) {
                timePerBlock = timePerBlock.Round(TimeSpan.FromHours(1));
                interval = $"{timePerBlock.TotalHours.ToString("0")}h";
            } else if (timePerBlock.TotalMinutes > 1) {
                timePerBlock = timePerBlock.Round(TimeSpan.FromMinutes(1));
                interval = $"{timePerBlock.TotalMinutes.ToString("0")}m";
            } else {
                timePerBlock = timePerBlock.Round(TimeSpan.FromSeconds(15));
                if (timePerBlock.TotalSeconds < 1)
                    timePerBlock = TimeSpan.FromSeconds(15);

                interval = $"{timePerBlock.TotalSeconds.ToString("0")}s";
            }

            return Tuple.Create(interval, timePerBlock);
        }
    }
}
