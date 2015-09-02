using System;
using System.Linq;
using Elasticsearch.Net;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Stats;
using Foundatio.Caching;
using Nest;
using NLog.Fluent;

namespace Exceptionless.Core.Utility {
    public class EventStats {
        private readonly ICacheClient _cacheClient;
        private readonly IElasticClient _elasticClient;

        public EventStats(ICacheClient cacheClient, IElasticClient elasticClient) {
            _cacheClient = cacheClient;
            _elasticClient = elasticClient;
        }

        public EventTermStatsResult GetTermsStats(DateTime utcStart, DateTime utcEnd, string term, string systemFilter, string userFilter = null, TimeSpan? displayTimeOffset = null, int max = 25, int desiredDataPoints = 10) {
            if (!displayTimeOffset.HasValue)
                displayTimeOffset = TimeSpan.Zero;

            var allowedTerms = new[] { "organization_id", "project_id", "stack_id", "tags", "version" };
            if (!allowedTerms.Contains(term))
                throw new ArgumentException("Must be a valid term.", "term");
            
            var filter = new ElasticSearchOptions<PersistentEvent>()
                .WithFilter(!String.IsNullOrEmpty(systemFilter) ? Filter<PersistentEvent>.Query(q => q.QueryString(qs => qs.DefaultOperator(Operator.And).Query(systemFilter))) : null)
                .WithQuery(userFilter)
                .WithDateRange(utcStart, utcEnd, "date")
                .WithIndicesFromDateRange();

            // if no start date then figure out first event date
            if (!filter.UseStartDate) {
                // TODO: Cache this to save an extra search request when a date range isn't filtered.
                _elasticClient.EnableTrace();
                var result = _elasticClient.Search<PersistentEvent>(s => s.IgnoreUnavailable().Index(filter.Indices.Count > 0 ? String.Join(",", filter.Indices) : String.Concat(ElasticSearchRepository<PersistentEvent>.EventsIndexName, "-*")).Filter(d => filter.GetElasticSearchFilter()).SortAscending(ev => ev.Date).Take(1));
                _elasticClient.DisableTrace();

                var firstEvent = result.Hits.FirstOrDefault();
                if (firstEvent != null) {
                    utcStart = firstEvent.Source.Date.UtcDateTime;
                    filter.WithDateRange(utcStart, utcEnd, "date");
                    filter.WithIndicesFromDateRange();
                }
            }

            utcStart = filter.GetStartDate();
            utcEnd = filter.GetEndDate();
            var interval = GetInterval(utcStart, utcEnd, desiredDataPoints);

            _elasticClient.EnableTrace();
            var res = _elasticClient.Search<PersistentEvent>(s => s
                .SearchType(SearchType.Count)
                .IgnoreUnavailable()
                .Index(filter.Indices.Count > 0 ? String.Join(",", filter.Indices) : String.Concat(ElasticSearchRepository<PersistentEvent>.EventsIndexName, "-*"))
                .Aggregations(agg => agg
                    .Filter("filtered", f => f
                        .Filter(d => filter.GetElasticSearchFilter())
                        .Aggregations(filteredAgg => filteredAgg
                            .Terms("terms", t => t
                                .Field(term)
                                .Size(max)
                                .Aggregations(agg2 => agg2
                                    .DateHistogram("timelime", tl => tl
                                        .Field(ev => ev.Date)
                                        .MinimumDocumentCount(0)
                                        .Interval(interval.Item1)
                                        .TimeZone(HoursAndMinutes(displayTimeOffset.Value))
                                    )
                                    .Cardinality("unique", u => u
                                        .Field(ev => ev.StackId)
                                        .PrecisionThreshold(100)
                                    )
                                    .Terms("new", u => u
                                        .Field(ev => ev.IsFirstOccurrence)
                                        .Exclude("F")
                                    )
                                    .Min("first_occurrence", o => o.Field(ev => ev.Date))
                                    .Max("last_occurrence", o => o.Field(ev => ev.Date))
                                )
                            )
                            .Cardinality("unique", u => u
                                .Field(ev => ev.StackId)
                                .PrecisionThreshold(100)
                            )
                            .Terms("new", u => u
                                .Field(ev => ev.IsFirstOccurrence)
                                .Exclude("F")
                            )
                            .Min("first_occurrence", o => o.Field(ev => ev.Date))
                            .Max("last_occurrence", o => o.Field(ev => ev.Date))
                        )
                    )
                )
            );

            _elasticClient.DisableTrace();

            if (!res.IsValid) {
                Log.Error().Message("Retrieving term stats failed: {0}", res.ServerError.Error).Write();
                throw new ApplicationException("Retrieving term stats failed.");
            }

            var filtered = res.Aggs.Filter("filtered");
            if (filtered == null)
                return new EventTermStatsResult();

            var newTerms = filtered.Terms("new");
            var stats = new EventTermStatsResult {
                Total = filtered.DocCount,
                New = newTerms != null && newTerms.Items.Count > 0 ? newTerms.Items[0].DocCount : 0,
                Start = utcStart.SafeAdd(displayTimeOffset.Value),
                End = utcEnd.SafeAdd(displayTimeOffset.Value)
            };

            var unique = filtered.Cardinality("unique");
            if (unique != null && unique.Value.HasValue)
                stats.Unique = (long)unique.Value;

            var firstOccurrence = filtered.Min("first_occurrence");
            if (firstOccurrence != null && firstOccurrence.Value.HasValue)
                stats.FirstOccurrence = firstOccurrence.Value.Value.ToDateTime().SafeAdd(displayTimeOffset.Value);

            var lastOccurrence = filtered.Max("last_occurrence");
            if (lastOccurrence != null && lastOccurrence.Value.HasValue)
                stats.LastOccurrence = lastOccurrence.Value.Value.ToDateTime().SafeAdd(displayTimeOffset.Value);

            var terms = filtered.Terms("terms");
            if (terms == null)
                return stats;

            stats.Terms.AddRange(terms.Items.Select(i => {
                long count = 0;
                var timelineUnique = i.Cardinality("unique");
                if (timelineUnique != null && timelineUnique.Value.HasValue)
                    count = (long)timelineUnique.Value;

                var termNew = i.Terms("new");
                var item = new TermStatsItem {
                    Total = i.DocCount,
                    Unique = count,
                    Term = i.Key,
                    New = termNew != null && termNew.Items.Count > 0 ? termNew.Items[0].DocCount : 0
                };

                var termFirstOccurrence = i.Min("first_occurrence");
                if (termFirstOccurrence != null && termFirstOccurrence.Value.HasValue)
                    item.FirstOccurrence = termFirstOccurrence.Value.Value.ToDateTime().SafeAdd(displayTimeOffset.Value);

                var termLastOccurrence = i.Max("last_occurrence");
                if (termLastOccurrence != null && termLastOccurrence.Value.HasValue)
                    item.LastOccurrence = termLastOccurrence.Value.Value.ToDateTime().SafeAdd(displayTimeOffset.Value);

                var timeLine = i.DateHistogram("timelime");
                if (timeLine != null) {
                    item.Timeline.AddRange(timeLine.Items.Select(ti => new TermTimelineItem {
                        Date = ti.Date,
                        Total = ti.DocCount
                    }));
                }

                return item;
            }));

            return stats;
        }

        public EventStatsResult GetOccurrenceStats(DateTime utcStart, DateTime utcEnd, string systemFilter, string userFilter = null, TimeSpan? displayTimeOffset = null, int desiredDataPoints = 100) {
            if (!displayTimeOffset.HasValue)
                displayTimeOffset = TimeSpan.Zero;

            var filter = new ElasticSearchOptions<PersistentEvent>()
                .WithFilter(!String.IsNullOrEmpty(systemFilter) ? Filter<PersistentEvent>.Query(q => q.QueryString(qs => qs.DefaultOperator(Operator.And).Query(systemFilter))) : null)
                .WithQuery(userFilter)
                .WithDateRange(utcStart, utcEnd, "date")
                .WithIndicesFromDateRange();

            // if no start date then figure out first event date
            if (!filter.UseStartDate) {
                // TODO: Cache this to save an extra search request when a date range isn't filtered.
                _elasticClient.EnableTrace();
                var result = _elasticClient.Search<PersistentEvent>(s => s.IgnoreUnavailable().Index(filter.Indices.Count > 0 ? String.Join(",", filter.Indices) : String.Concat(ElasticSearchRepository<PersistentEvent>.EventsIndexName, "-*")).Filter(d => filter.GetElasticSearchFilter()).SortAscending(ev => ev.Date).Take(1));
                _elasticClient.DisableTrace();

                var firstEvent = result.Hits.FirstOrDefault();
                if (firstEvent != null) {
                    utcStart = firstEvent.Source.Date.UtcDateTime;
                    filter.WithDateRange(utcStart, utcEnd, "date");
                    filter.WithIndicesFromDateRange();
                }
            }

            utcStart = filter.GetStartDate();
            utcEnd = filter.GetEndDate();
            var interval = GetInterval(utcStart, utcEnd, desiredDataPoints);

            _elasticClient.EnableTrace();
            var res = _elasticClient.Search<PersistentEvent>(s => s
                .SearchType(SearchType.Count)
                .IgnoreUnavailable()
                .Index(filter.Indices.Count > 0 ? String.Join(",", filter.Indices) : String.Concat(ElasticSearchRepository<PersistentEvent>.EventsIndexName, "-*"))
                .Aggregations(agg => agg
                    .Filter("filtered", f => f
                        .Filter(d => filter.GetElasticSearchFilter())
                        .Aggregations(filteredAgg => filteredAgg
                            .DateHistogram("timelime", t => t
                                .Field(ev => ev.Date)
                                .MinimumDocumentCount(0)
                                .Interval(interval.Item1)
                                .Aggregations(agg2 => agg2
                                    .Cardinality("tl_unique", u => u
                                        .Field(ev => ev.StackId)
                                        .PrecisionThreshold(100)
                                    )
                                    .Terms("tl_new", u => u
                                        .Field(ev => ev.IsFirstOccurrence)
                                        .Exclude("F")
                                    )
                                )
                                .TimeZone(HoursAndMinutes(displayTimeOffset.Value))
                            )
                            .Cardinality("unique", u => u
                                .Field(ev => ev.StackId)
                                .PrecisionThreshold(100)
                            )
                            .Terms("new", u => u
                                .Field(ev => ev.IsFirstOccurrence)
                                .Exclude("F")
                            )
                            .Min("first_occurrence", t => t.Field(ev => ev.Date))
                            .Max("last_occurrence", t => t.Field(ev => ev.Date))
                        )
                    )
                )
            );
            _elasticClient.DisableTrace();

            if (!res.IsValid) {
                Log.Error().Message("Retrieving stats failed: {0}", res.ServerError.Error).Write();
                throw new ApplicationException("Retrieving stats failed.");
            }

            var filtered = res.Aggs.Filter("filtered");
            if (filtered == null)
                return new EventStatsResult();

            var newTerms = filtered.Terms("new");
            var stats = new EventStatsResult {
                Total = filtered.DocCount,
                New = newTerms != null && newTerms.Items.Count > 0 ? newTerms.Items[0].DocCount : 0
            };

            var unique = filtered.Cardinality("unique");
            if (unique != null && unique.Value.HasValue)
                stats.Unique = (long)unique.Value;

            var timeline = filtered.DateHistogram("timelime");
            if (timeline != null) {
                stats.Timeline.AddRange(timeline.Items.Select(i => {
                    long count = 0;
                    var timelineUnique = i.Cardinality("tl_unique");
                    if (timelineUnique != null && timelineUnique.Value.HasValue)
                        count = (long)timelineUnique.Value;

                    var timelineNew = i.Terms("tl_new");
                    return new TimelineItem {
                        Date = i.Date,
                        Total = i.DocCount,
                        Unique = count,
                        New = timelineNew != null && timelineNew.Items.Count > 0 ? timelineNew.Items[0].DocCount : 0
                    };
                }));
            }

            stats.Start = stats.Timeline.Count > 0 ? stats.Timeline.Min(tl => tl.Date).SafeAdd(displayTimeOffset.Value) : utcStart.SafeAdd(displayTimeOffset.Value);
            stats.End = utcEnd.SafeAdd(displayTimeOffset.Value);

            var totalHours = stats.End.Subtract(stats.Start).TotalHours;
            if (totalHours > 0.0)
                stats.AvgPerHour = stats.Total / totalHours;

            if (stats.Timeline.Count <= 0)
                return stats;

            var firstOccurrence = filtered.Min("first_occurrence");
            if (firstOccurrence != null && firstOccurrence.Value.HasValue)
                stats.FirstOccurrence = firstOccurrence.Value.Value.ToDateTime().SafeAdd(displayTimeOffset.Value);

            var lastOccurrence = filtered.Max("last_occurrence");
            if (lastOccurrence != null && lastOccurrence.Value.HasValue)
                stats.LastOccurrence = lastOccurrence.Value.Value.ToDateTime().SafeAdd(displayTimeOffset.Value);

            return stats;
        }

        private static string HoursAndMinutes(TimeSpan ts) {
            return (ts < TimeSpan.Zero ? "-" : "") + ts.ToString("hh\\:mm");
        }

        private static Tuple<string, TimeSpan> GetInterval(DateTime utcStart, DateTime utcEnd, int desiredDataPoints = 100) {
            string interval;
            var totalTime = utcEnd - utcStart;

            var timePerBlock = TimeSpan.FromMinutes(totalTime.TotalMinutes / desiredDataPoints);
            if (timePerBlock.TotalDays > 1) {
                timePerBlock = timePerBlock.Round(TimeSpan.FromDays(1));
                interval = String.Format("{0}d", timePerBlock.TotalDays.ToString("0"));
            } else if (timePerBlock.TotalHours > 1) {
                timePerBlock = timePerBlock.Round(TimeSpan.FromHours(1));
                interval = String.Format("{0}h", timePerBlock.TotalHours.ToString("0"));
            } else if (timePerBlock.TotalMinutes > 1) {
                timePerBlock = timePerBlock.Round(TimeSpan.FromMinutes(1));
                interval = String.Format("{0}m", timePerBlock.TotalMinutes.ToString("0"));
            } else {
                timePerBlock = timePerBlock.Round(TimeSpan.FromSeconds(15));
                if (timePerBlock.TotalSeconds < 1)
                    timePerBlock = TimeSpan.FromSeconds(15);

                interval = String.Format("{0}s", timePerBlock.TotalSeconds.ToString("0"));
            }

            return Tuple.Create(interval, timePerBlock);
        }
    }
}
