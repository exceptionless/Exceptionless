using System;
using System.Linq;
using CodeSmith.Core.Extensions;
using Elasticsearch.Net;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Models;
using Exceptionless.Models.Stats;
using Nest;
using NLog.Fluent;

namespace Exceptionless.Core.Utility {
    public class EventStats {
        private readonly IElasticClient _client;

        public EventStats(IElasticClient client) {
            _client = client;
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
            _client.EnableTrace();

            // if no start date then figure out first event date
            if (!filter.UseStartDate) {
                var result = _client.Search<PersistentEvent>(s => s.IgnoreUnavailable().Index(filter.Indices.Count > 0 ? String.Join(",", filter.Indices) : String.Concat(ElasticSearchRepository<PersistentEvent>.EventsIndexName, "-*")).Filter(d => filter.GetElasticSearchFilter()).SortAscending(ev => ev.Date).Take(1));

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
            var res = _client.Search<PersistentEvent>(s => s
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
                                        .PrecisionThreshold(1000)
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
                    )
                )
            );

            if (!res.IsValid) {
                Log.Error().Message("Retrieving term stats failed: {0}", res.ServerError.Error).Write();
                throw new ApplicationException("Retrieving term stats failed.");
            }

            _client.DisableTrace();


            var filtered = res.Aggs.Filter("filtered");
            if (filtered == null)
                return new EventTermStatsResult();

            var stats = new EventTermStatsResult {
                Total = filtered.DocCount
            };

            stats.Terms.AddRange(filtered.Terms("terms").Items.Select(i => {
                long count = 0;
                var timelineUnique = i.Cardinality("unique").Value;
                if (timelineUnique.HasValue)
                    count = (long)timelineUnique.Value;

                var item = new TermStatsItem {
                    Total = i.DocCount,
                    Unique = count,
                    Term = i.Key,
                    New = i.Terms("new").Items.Count > 0 ? i.Terms("new").Items[0].DocCount : 0
                };

                var firstOccurrence = i.Min("first_occurrence").Value;
                var lastOccurrence = i.Max("last_occurrence").Value;

                if (firstOccurrence.HasValue)
                    item.FirstOccurrence = firstOccurrence.Value.ToDateTime().SafeAdd(displayTimeOffset.Value);

                if (lastOccurrence.HasValue)
                    item.LastOccurrence = lastOccurrence.Value.ToDateTime().SafeAdd(displayTimeOffset.Value);

                item.Timeline.AddRange(i.DateHistogram("timelime").Items.Select(ti => new TermTimelineItem {
                    Date = ti.Date,
                    Total = ti.DocCount
                }));

                return item;
            }));

            stats.Start = utcStart.SafeAdd(displayTimeOffset.Value);
            stats.End = utcEnd.SafeAdd(displayTimeOffset.Value);

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
            _client.EnableTrace();

            // if no start date then figure out first event date
            if (!filter.UseStartDate) {
                var result = _client.Search<PersistentEvent>(s => s.IgnoreUnavailable().Index(filter.Indices.Count > 0 ? String.Join(",", filter.Indices) : String.Concat(ElasticSearchRepository<PersistentEvent>.EventsIndexName, "-*")).Filter(d => filter.GetElasticSearchFilter()).SortAscending(ev => ev.Date).Take(1));

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
            var res = _client.Search<PersistentEvent>(s => s
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
                                        .PrecisionThreshold(1000)
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
                                .PrecisionThreshold(1000)
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

            if (!res.IsValid) {
                Log.Error().Message("Retrieving stats failed: {0}", res.ServerError.Error).Write();
                throw new ApplicationException("Retrieving stats failed.");
            }

            _client.DisableTrace();

            var filtered = res.Aggs.Filter("filtered");
            if (filtered == null)
                return new EventStatsResult();

            var stats = new EventStatsResult {
                Total = filtered.DocCount,
                New = filtered.Terms("new").Items.Count > 0 ? filtered.Terms("new").Items[0].DocCount : 0
            };

            var unique = filtered.Cardinality("unique").Value;
            if (unique.HasValue)
                stats.Unique = (long)unique.Value;

            stats.Timeline.AddRange(filtered.DateHistogram("timelime").Items.Select(i => {
                long count = 0;
                var timelineUnique = i.Cardinality("tl_unique").Value;
                if (timelineUnique.HasValue)
                    count = (long)timelineUnique.Value;

                return new TimelineItem {
                    Date = i.Date,
                    Total = i.DocCount,
                    Unique = count,
                    New = i.Terms("tl_new").Items.Count > 0 ? i.Terms("tl_new").Items[0].DocCount : 0
                };
            }));

            stats.Start = stats.Timeline.Count > 0 ? stats.Timeline.Min(tl => tl.Date).SafeAdd(displayTimeOffset.Value) : utcStart.SafeAdd(displayTimeOffset.Value);
            stats.End = utcEnd.SafeAdd(displayTimeOffset.Value);
            stats.AvgPerHour = stats.Total / stats.End.Subtract(stats.Start).TotalHours;

            if (stats.Timeline.Count <= 0)
                return stats;

            var firstOccurrence = filtered.Min("first_occurrence").Value;
            var lastOccurrence = filtered.Max("last_occurrence").Value;
                
            if (firstOccurrence.HasValue)
                stats.FirstOccurrence = firstOccurrence.Value.ToDateTime().SafeAdd(displayTimeOffset.Value);
                
            if (lastOccurrence.HasValue)
                stats.LastOccurrence = lastOccurrence.Value.ToDateTime().SafeAdd(displayTimeOffset.Value);

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
            } else {
                timePerBlock = timePerBlock.Round(TimeSpan.FromMinutes(1));
                interval = String.Format("{0}m", timePerBlock.TotalMinutes.ToString("0"));
            }

            return Tuple.Create(interval, timePerBlock);
        }
    }
}
