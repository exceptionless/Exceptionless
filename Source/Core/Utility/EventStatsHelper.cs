#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CodeSmith.Core.Extensions;
using Exceptionless.Core.Repositories;
using Exceptionless.Models;
using Exceptionless.Models.Stats;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Options;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using NLog.Fluent;

namespace Exceptionless.Core.Utility {
    public class EventStatsHelper {
        private readonly StackRepository _stackRepository;
        private readonly DayStackStatsRepository _dayStackStats;
        private readonly MonthStackStatsRepository _monthStackStats;
        private readonly DayProjectStatsRepository _dayProjectStats;
        private readonly MonthProjectStatsRepository _monthProjectStats;

        public EventStatsHelper(StackRepository stackRepository, DayStackStatsRepository dayStackStats,
            MonthStackStatsRepository monthStackStats, DayProjectStatsRepository dayProjectStats,
            MonthProjectStatsRepository monthProjectStats) {
            _stackRepository = stackRepository;
            _dayStackStats = dayStackStats;
            _monthStackStats = monthStackStats;
            _dayProjectStats = dayProjectStats;
            _monthProjectStats = monthProjectStats;
        }

        public ProjectEventStatsResult GetProjectErrorStats(string projectId, TimeSpan utcOffset, DateTime? localStartDate = null, DateTime? localEndDate = null, DateTime? retentionStartDate = null, bool includeHidden = false, bool includeFixed = false, bool include404s = true) {
            // TODO: Check to see if we have day stats available for the project, if not, then use minute stats and queue new project offset to be created in a job.
            if (retentionStartDate.HasValue)
                retentionStartDate = retentionStartDate.Value.Floor(TimeSpan.FromMinutes(15));

            var range = GetDateRange(localStartDate, localEndDate, utcOffset, TimeSpan.FromMinutes(15));
            if (range.Item1 == range.Item2)
                return new ProjectEventStatsResult();

            // Use finer grained minute blocks if the range is less than 5 days.
            return range.Item2.Subtract(range.Item1).TotalDays < 5 
                ? GetProjectEventStatsByMinuteBlock(projectId, utcOffset, localStartDate, localEndDate, retentionStartDate, includeHidden, includeFixed, include404s) 
                : GetProjectEventStatsByDay(projectId, utcOffset, localStartDate, localEndDate, retentionStartDate, includeHidden, includeFixed, include404s);
        }

        public ProjectEventStatsResult GetProjectEventStatsByDay(string projectId, TimeSpan utcOffset, DateTime? localStartDate = null, DateTime? localEndDate = null, DateTime? retentionStartDate = null, bool includeHidden = false, bool includeFixed = false, bool includeNotFound = true) {
            // TODO: Check to see if we have day stats available for the project, if not, then use minute stats and queue new project offset to be created in a job
            // Round date range to be full days since stats are per day.
            var range = GetDateRange(localStartDate, localEndDate, utcOffset, TimeSpan.FromDays(1));
            if (range.Item1 == range.Item2)
                return new ProjectEventStatsResult();

            localStartDate = range.Item1;
            localEndDate = range.Item2;

            var results = _monthProjectStats.Collection.Find(
                Query.And(
                    Query.GTE(MonthProjectStatsRepository.FieldNames.Id, GetMonthProjectStatsId(localStartDate.Value, utcOffset, projectId)),
                    Query.LTE(MonthProjectStatsRepository.FieldNames.Id, GetMonthProjectStatsId(localEndDate.Value, utcOffset, projectId))
                )).ToList();

            if (results.Count > 0) {
                var firstWithOccurrence = results.OrderBy(r => r.Id).FirstOrDefault(r => r.DayStats.Any(ds => ds.Value.Total > 0));
                if (firstWithOccurrence != null) {
                    var firstErrorDate = firstWithOccurrence.GetDateFromDayStatKey(firstWithOccurrence.DayStats.OrderBy(ds => Int32.Parse(ds.Key)).First(ds => ds.Value.Total > 0).Key);
                    if (localStartDate < firstErrorDate)
                        localStartDate = firstErrorDate;
                }

                if (!includeHidden) {
                    // Remove stats from hidden doc ids.
                    var hiddenIds = _stackRepository.GetHiddenIds(projectId);
                    if (hiddenIds.Length > 0)
                        DecrementMonthProjectStatsByStackIds(results, hiddenIds);
                }

                if (!includeNotFound) {
                    // Remove stats from not found doc ids.
                    var notFoundIds = _stackRepository.GetNotFoundIds(projectId);
                    if (notFoundIds.Length > 0)
                        DecrementMonthProjectStatsByStackIds(results, notFoundIds);
                }

                if (!includeFixed) {
                    // Remove stats from not found doc ids.
                    var fixedIds = _stackRepository.GetFixedIds(projectId);
                    if (fixedIds.Length > 0)
                        DecrementMonthProjectStatsByStackIds(results, fixedIds);
                }
            }

            if (retentionStartDate.HasValue)
                retentionStartDate = retentionStartDate.Value.Floor(TimeSpan.FromDays(1));

            // Use finer grained minute blocks if the range is less than 5 days.
            if (localEndDate.Value.Subtract(localStartDate.Value).TotalDays < 5)
                return GetProjectEventStatsByMinuteBlock(projectId, utcOffset, localStartDate, localEndDate, retentionStartDate, includeHidden, includeFixed, includeNotFound);

            var monthDocIds = new List<Tuple<int, int>>();
            DateTime currentDay = localStartDate.Value;
            var endOfMonthEndDate = new DateTime(localEndDate.Value.Year, localEndDate.Value.Month, DateTime.DaysInMonth(localEndDate.Value.Year, localEndDate.Value.Month));
            while (currentDay <= endOfMonthEndDate) {
                monthDocIds.Add(Tuple.Create(currentDay.Year, currentDay.Month));
                currentDay = currentDay.AddMonths(1);
            }

            // Add missing month documents.
            foreach (var monthDocId in monthDocIds) {
                if (!results.Exists(d => d.Id == GetMonthProjectStatsId(monthDocId.Item1, monthDocId.Item2, utcOffset, projectId)))
                    results.Add(CreateBlankMonthProjectStats(utcOffset, new DateTime(monthDocId.Item1, monthDocId.Item2, 1), projectId));
            }

            // Fill out missing days with blank stats.
            foreach (MonthProjectStats r in results) {
                DateTime date = r.GetDateFromDayStatKey("1");
                int daysInMonth = DateTime.DaysInMonth(date.Year, date.Month);
                for (int i = 1; i <= daysInMonth; i++) {
                    if (!r.DayStats.ContainsKey(i.ToString()))
                        r.DayStats.Add(i.ToString(), new EventStatsWithStackIds());
                }
            }

            var days = new List<KeyValuePair<DateTime, EventStatsWithStackIds>>();
            days = results.Aggregate(days, (current, result) => current.Concat(result.DayStats.ToDictionary(kvp => result.GetDateFromDayStatKey(kvp.Key), kvp => kvp.Value)).ToList())
                .Where(kvp => kvp.Key >= localStartDate.Value && kvp.Key <= localEndDate.Value).OrderBy(kvp => kvp.Key).ToList();

            int totalLimitedByPlan = retentionStartDate != null && localStartDate < retentionStartDate
                ? days.Where(kvp => kvp.Key < retentionStartDate).SelectMany(kvp => kvp.Value.StackIds.Select(s => s.Key)).Distinct()
                    .Except(days.Where(kvp => kvp.Key >= retentionStartDate).SelectMany(kvp => kvp.Value.StackIds.Select(s => s.Key)).Distinct())
                    .Count()
                : 0;

            if (totalLimitedByPlan > 0)
                days = days.Where(kvp => kvp.Key >= retentionStartDate).ToList();

            // Group data points by a time span to limit the number of returned data points.
            TimeSpan groupTimeSpan = TimeSpan.FromDays(1);
            if (days.Count > 50) {
                DateTime first = days.Min(m => m.Key);
                DateTime last = days.Max(m => m.Key);
                TimeSpan span = last - first;
                groupTimeSpan = TimeSpan.FromDays(((int)Math.Round((span.TotalDays / 50) / 1.0)) * 1);
            }

            var stats = new ProjectEventStatsResult {
                TotalLimitedByPlan = totalLimitedByPlan,
                StartDate = localStartDate.Value,
                EndDate = localEndDate.Value,
                Total = days.Sum(kvp => kvp.Value.Total),
                UniqueTotal = days.SelectMany(kvp => kvp.Value.StackIds.Select(s => s.Key)).Distinct().Count(),
                NewTotal = days.Sum(kvp => kvp.Value.NewTotal),
                MostFrequent = new PlanPagedResult<EventStackResult>(days.SelectMany(kvp => kvp.Value.StackIds).GroupBy(kvp => kvp.Key)
                    .Select(v => new EventStackResult {
                        Id = v.Key,
                        Total = v.Sum(kvp => kvp.Value)
                    }).OrderByDescending(s => s.Total).ToList(), totalLimitedByPlan: totalLimitedByPlan, totalCount: days.Count),
                Stats = days.GroupBy(s => s.Key.Floor(groupTimeSpan)).Select(kvp => new DateProjectStatsResult {
                    Date = kvp.Key,
                    Total = kvp.Sum(b => b.Value.Total),
                    NewTotal = kvp.Sum(b => b.Value.NewTotal),
                    UniqueTotal = kvp.Select(b => b.Value.StackIds).Aggregate((current, result) => Merge(result, current)).Count()
                }).ToList()
            };

            return stats;
        }

        public ProjectEventStatsResult GetProjectEventStatsByMinuteBlock(string projectId, TimeSpan utcOffset, DateTime? localStartDate = null, DateTime? localEndDate = null, DateTime? retentionStartDate = null, bool includeHidden = false, bool includeFixed = false, bool includeNotFound = true) {
            // Round date range to blocks of 15 minutes since stats are per 15 minute block.
            var range = GetDateRange(localStartDate, localEndDate, utcOffset, TimeSpan.FromMinutes(15));
            if (range.Item1 == range.Item2)
                return new ProjectEventStatsResult();

            DateTime utcStartDate = new DateTimeOffset(range.Item1.Ticks, utcOffset).UtcDateTime;
            DateTime utcEndDate = new DateTimeOffset(range.Item2.Ticks, utcOffset).UtcDateTime;

            List<DayProjectStats> results = _dayProjectStats.Collection.Find(
                Query.And(
                    Query.GTE(MonthProjectStatsRepository.FieldNames.Id, GetDayProjectStatsId(projectId, utcStartDate)),
                    Query.LTE(MonthProjectStatsRepository.FieldNames.Id, GetDayProjectStatsId(projectId, utcEndDate))
                )).ToList();

            if (results.Count > 0) {
                DayProjectStats firstWithOccurrence = results.OrderBy(r => r.Id).FirstOrDefault(r => r.MinuteStats.Any(ds => ds.Value.Total > 0));
                if (firstWithOccurrence != null) {
                    DateTime firstErrorDate = firstWithOccurrence.GetDateFromMinuteStatKey(firstWithOccurrence.MinuteStats.OrderBy(ds => Int32.Parse(ds.Key)).First(ds => ds.Value.Total > 0).Key);
                    if (utcStartDate < firstErrorDate)
                        utcStartDate = firstErrorDate;
                }

                if (!includeHidden) {
                    // remove stats from hidden doc ids.
                    string[] hiddenIds = _stackRepository.GetHiddenIds(projectId);
                    if (hiddenIds.Length > 0)
                        DecrementDayProjectStatsByStackIds(results, hiddenIds);
                }

                if (!includeNotFound) {
                    // remove stats from not found doc ids.
                    string[] notFoundIds = _stackRepository.GetNotFoundIds(projectId);
                    if (notFoundIds.Length > 0)
                        DecrementDayProjectStatsByStackIds(results, notFoundIds);
                }

                if (!includeFixed) {
                    // remove stats from not found doc ids.
                    string[] fixedIds = _stackRepository.GetFixedIds(projectId);
                    if (fixedIds.Length > 0)
                        DecrementDayProjectStatsByStackIds(results, fixedIds);
                }
            }

            var dayDocDates = new List<DateTime>();
            DateTime currentDay = utcStartDate;
            DateTime endOfDayEndDate = utcEndDate.ToEndOfDay();
            while (currentDay <= endOfDayEndDate) {
                dayDocDates.Add(currentDay);
                currentDay = currentDay.AddDays(1);
            }

            // add missing day documents
            foreach (DateTime dayDocDate in dayDocDates) {
                if (!results.Exists(d => d.Id == GetDayProjectStatsId(projectId, dayDocDate)))
                    results.Add(CreateBlankDayProjectStats(dayDocDate, projectId));
            }

            // fill out missing minute blocks with blank stats
            foreach (DayProjectStats r in results) {
                const int minuteBlocksInDay = 96;
                for (int i = 0; i <= minuteBlocksInDay - 1; i++) {
                    int minuteBlock = i * 15;
                    if (!r.MinuteStats.ContainsKey(minuteBlock.ToString("0000")))
                        r.MinuteStats.Add(minuteBlock.ToString("0000"), new EventStatsWithStackIds());
                }
            }

            if (retentionStartDate.HasValue)
                retentionStartDate = retentionStartDate.Value.Floor(TimeSpan.FromMinutes(15));

            var minuteBlocks = new List<KeyValuePair<DateTime, EventStatsWithStackIds>>();
            minuteBlocks = results.Aggregate(minuteBlocks, (current, result) => current.Concat(result.MinuteStats.ToDictionary(kvp => result.GetDateFromMinuteStatKey(kvp.Key), kvp => kvp.Value)).ToList())
                .Where(kvp => kvp.Key >= utcStartDate && kvp.Key <= utcEndDate).OrderBy(kvp => kvp.Key).ToList();

            int totalLimitedByPlan = retentionStartDate != null && utcStartDate < retentionStartDate
                ? minuteBlocks.Where(kvp => kvp.Key < retentionStartDate).SelectMany(kvp => kvp.Value.StackIds.Select(s => s.Key)).Distinct()
                    .Except(minuteBlocks.Where(kvp => kvp.Key >= retentionStartDate).SelectMany(kvp => kvp.Value.StackIds.Select(s => s.Key)).Distinct())
                    .Count()
                : 0;

            if (totalLimitedByPlan > 0)
                minuteBlocks = minuteBlocks.Where(kvp => kvp.Key >= retentionStartDate).ToList();

            // group data points by a timespan to limit the number of returned data points
            TimeSpan groupTimeSpan = TimeSpan.FromMinutes(15);
            if (minuteBlocks.Count > 50) {
                DateTime first = minuteBlocks.Min(m => m.Key);
                DateTime last = minuteBlocks.Max(m => m.Key);
                TimeSpan span = last - first;
                groupTimeSpan = TimeSpan.FromMinutes(((int)Math.Round(span.TotalMinutes / 50 / 15.0)) * 15);
            }

            var stats = new ProjectEventStatsResult {
                TotalLimitedByPlan = totalLimitedByPlan,
                StartDate = utcStartDate,
                EndDate = utcEndDate,
                Total = minuteBlocks.Sum(kvp => kvp.Value.Total),
                UniqueTotal = minuteBlocks.SelectMany(kvp => kvp.Value.StackIds.Select(s => s.Key)).Distinct().Count(),
                NewTotal = minuteBlocks.Sum(kvp => kvp.Value.NewTotal),
                MostFrequent = new PlanPagedResult<EventStackResult>(minuteBlocks.SelectMany(kvp => kvp.Value.StackIds).GroupBy(kvp => kvp.Key)
                    .Select(v => new EventStackResult {
                        Id = v.Key,
                        Total = v.Sum(kvp => kvp.Value),
                    }).OrderByDescending(s => s.Total).ToList(), totalLimitedByPlan: totalLimitedByPlan, totalCount: minuteBlocks.Count),
                Stats = minuteBlocks.GroupBy(s => s.Key.Floor(groupTimeSpan)).Select(kvp => new DateProjectStatsResult {
                    Date = kvp.Key,
                    Total = kvp.Sum(b => b.Value.Total),
                    NewTotal = kvp.Sum(b => b.Value.NewTotal),
                    UniqueTotal = kvp.Select(b => b.Value.StackIds).Aggregate((current, result) => Merge(result, current)).Count()
                }).ToList()
            };

            stats.ApplyTimeOffset(utcOffset);

            return stats;
        }

        public StackStatsResult GetStackStats(string stackId, TimeSpan utcOffset, DateTime? localStartDate = null, DateTime? localEndDate = null, DateTime? retentionStartDate = null) {
            // TODO: Check to see if we have day stats available for the project, if not, then use minute stats and queue new project offset to be created in a job
            if (retentionStartDate.HasValue)
                retentionStartDate = retentionStartDate.Value.Floor(TimeSpan.FromMinutes(15));

            // Round date range to blocks of 15 minutes since stats are per 15 minute block.
            var range = GetDateRange(localStartDate, localEndDate, utcOffset, TimeSpan.FromMinutes(15));
            if (range.Item1 == range.Item2)
                return new StackStatsResult();
           
            // Use finer grained minute blocks if the range is less than 5 days.
            return range.Item2.Subtract(range.Item1).TotalDays < 5
                ? GetStackStatsByMinuteBlock(stackId, utcOffset, range.Item1, range.Item2, retentionStartDate)
                : GetStackStatsByDay(stackId, utcOffset, range.Item1, range.Item2, retentionStartDate);
        }

        public StackStatsResult GetStackStatsByDay(string stackId, TimeSpan utcOffset, DateTime? localStartDate = null, DateTime? localEndDate = null, DateTime? retentionStartDate = null) {
            if (retentionStartDate.HasValue)
                retentionStartDate = retentionStartDate.Value.Floor(TimeSpan.FromDays(1));

            var range = GetDateRange(localStartDate, localEndDate, utcOffset, TimeSpan.FromDays(1));
            if (range.Item1 == range.Item2)
                return new StackStatsResult();

            localStartDate = range.Item1;
            localEndDate = range.Item2;

            var results = _monthStackStats.Collection.Find(
                Query.And(
                    Query.GTE(MonthStackStatsRepository.FieldNames.Id, GetMonthStackStatsId(localStartDate.Value, utcOffset, stackId)),
                    Query.LTE(MonthStackStatsRepository.FieldNames.Id, GetMonthStackStatsId(localEndDate.Value, utcOffset, stackId)))
                ).ToList();

            if (results.Count > 0) {
                var firstWithOccurrence = results.OrderBy(r => r.Id).FirstOrDefault(r => r.DayStats.Any(ds => ds.Value > 0));
                if (firstWithOccurrence != null) {
                    var firstEventDate = firstWithOccurrence.GetDay(firstWithOccurrence.DayStats.OrderBy(ds => Int32.Parse(ds.Key)).First(ds => ds.Value > 0).Key);
                    if (localStartDate < firstEventDate)
                        localStartDate = firstEventDate.Floor(TimeSpan.FromDays(1));
                }
            }

            if (localEndDate.Value.Subtract(localStartDate.Value).TotalDays < 5)
                return GetStackStatsByMinuteBlock(stackId, utcOffset, localStartDate, localEndDate);

            var monthDocIds = new List<Tuple<int, int>>();
            DateTime currentDay = localStartDate.Value;
            var endOfMonthEndDate = new DateTime(localEndDate.Value.Year, localEndDate.Value.Month, DateTime.DaysInMonth(localEndDate.Value.Year, localEndDate.Value.Month));
            while (currentDay <= endOfMonthEndDate) {
                monthDocIds.Add(Tuple.Create(currentDay.Year, currentDay.Month));
                currentDay = currentDay.AddMonths(1);
            }

            foreach (var monthDocId in monthDocIds) {
                if (!results.Exists(d => d.Id == GetMonthStackStatsId(monthDocId.Item1, monthDocId.Item2, utcOffset, stackId)))
                    results.Add(CreateBlankMonthStackStats(utcOffset, new DateTime(monthDocId.Item1, monthDocId.Item2, 1), stackId, null));
            }

            // fill out missing days with blank stats
            foreach (MonthStackStats r in results) {
                DateTime date = r.GetDay("1");
                int daysInMonth = DateTime.DaysInMonth(date.Year, date.Month);
                for (int i = 1; i <= daysInMonth; i++) {
                    if (!r.DayStats.ContainsKey(i.ToString()))
                        r.DayStats.Add(i.ToString(), 0);
                }
            }

            var days = new List<KeyValuePair<DateTime, int>>();
            days = results.Aggregate(days, (current, result) => current.Concat(result.DayStats.ToDictionary(kvp => result.GetDay(kvp.Key), kvp => kvp.Value)).ToList()).Where(kvp => kvp.Key >= localStartDate.Value && kvp.Key <= localEndDate.Value).ToList();

            int totalLimitedByPlan = retentionStartDate != null && localStartDate < retentionStartDate ? days.Count(kvp => kvp.Key < retentionStartDate) : 0;
            if (totalLimitedByPlan > 0)
                days = days.Where(kvp => kvp.Key >= retentionStartDate).ToList();

            return new StackStatsResult {
                TotalLimitedByPlan = totalLimitedByPlan,
                StartDate = localStartDate.Value,
                EndDate = localEndDate.Value,
                Total = days.Sum(kvp => kvp.Value),
                Stats = days.OrderBy(kvp => kvp.Key).Select(kvp => new DateStackStatsResult {
                    Date = kvp.Key,
                    Total = kvp.Value
                }).ToList()
            };
        }

        public StackStatsResult GetStackStatsByMinuteBlock(string stackId, TimeSpan utcOffset, DateTime? localStartDate = null, DateTime? localEndDate = null, DateTime? retentionStartDate = null) {
            // Round date range to blocks of 15 minutes since stats are per 15 minute block.
            var range = GetDateRange(localStartDate, localEndDate, utcOffset, TimeSpan.FromMinutes(15));
            if (range.Item1 == range.Item2)
                return new StackStatsResult();

            DateTime utcStartDate = new DateTimeOffset(range.Item1.Ticks, utcOffset).UtcDateTime;
            DateTime utcEndDate = new DateTimeOffset(range.Item2.Ticks, utcOffset).UtcDateTime;

            List<DayStackStats> results = _dayStackStats.Collection.Find(
                Query.And(
                    Query.GTE(MonthStackStatsRepository.FieldNames.Id, GetDayStackStatsId(stackId, utcStartDate)), 
                    Query.LTE(MonthStackStatsRepository.FieldNames.Id, GetDayStackStatsId(stackId, utcEndDate)))
                ).ToList();

            if (results.Count > 0) {
                DayStackStats firstWithOccurrence = results.OrderBy(r => r.Id).FirstOrDefault(r => r.MinuteStats.Any(ds => ds.Value > 0));
                if (firstWithOccurrence != null) {
                    DateTime firstErrorDate = firstWithOccurrence.GetDateFromMinuteStatKey(firstWithOccurrence.MinuteStats.OrderBy(ds => Int32.Parse(ds.Key)).First(ds => ds.Value > 0).Key);
                    if (utcStartDate < firstErrorDate)
                        utcStartDate = firstErrorDate;
                }
            }

            var dayDocDates = new List<DateTime>();
            DateTime currentDay = utcStartDate;
            DateTime endOfDayEndDate = utcEndDate.ToEndOfDay();
            while (currentDay <= endOfDayEndDate) {
                dayDocDates.Add(currentDay);
                currentDay = currentDay.AddDays(1);
            }

            // add missing day documents
            foreach (DateTime dayDocDate in dayDocDates) {
                if (!results.Exists(d => d.Id == GetDayStackStatsId(stackId, dayDocDate)))
                    results.Add(CreateBlankDayStackStats(dayDocDate, stackId));
            }

            // fill out missing minute blocks with blank stats
            foreach (DayStackStats r in results) {
                const int minuteBlocksInDay = 96;
                for (int i = 0; i <= minuteBlocksInDay - 1; i++) {
                    int minuteBlock = i * 15;
                    if (!r.MinuteStats.ContainsKey(minuteBlock.ToString("0000")))
                        r.MinuteStats.Add(minuteBlock.ToString("0000"), 0);
                }
            }

            if (retentionStartDate.HasValue)
                retentionStartDate = retentionStartDate.Value.Floor(TimeSpan.FromMinutes(15));

            var minuteBlocks = new List<KeyValuePair<DateTime, int>>();
            minuteBlocks = results.Aggregate(minuteBlocks, (current, result) => current.Concat(result.MinuteStats.ToDictionary(kvp => result.GetDateFromMinuteStatKey(kvp.Key), kvp => kvp.Value)).ToList())
                .Where(kvp => kvp.Key >= utcStartDate && kvp.Key <= utcEndDate).OrderBy(kvp => kvp.Key).ToList();

            int totalLimitedByPlan = retentionStartDate != null && utcStartDate < retentionStartDate ? minuteBlocks.Count(kvp => kvp.Key < retentionStartDate) : 0;
            if (totalLimitedByPlan > 0)
                minuteBlocks = minuteBlocks.Where(kvp => kvp.Key >= retentionStartDate).ToList();

            // group data points by a timespan to limit the number of returned data points
            TimeSpan groupTimeSpan = TimeSpan.FromMinutes(15);
            if (minuteBlocks.Count > 50) {
                DateTime first = minuteBlocks.Min(m => m.Key);
                DateTime last = minuteBlocks.Max(m => m.Key);
                TimeSpan span = last - first;
                groupTimeSpan = TimeSpan.FromMinutes(((int)Math.Round(span.TotalMinutes / 50 / 15.0)) * 15);
            }

            var stats = new StackStatsResult {
                TotalLimitedByPlan = totalLimitedByPlan,
                StartDate = utcStartDate,
                EndDate = utcEndDate,
                Total = minuteBlocks.Sum(kvp => kvp.Value),
                Stats = minuteBlocks.GroupBy(s => s.Key.Floor(groupTimeSpan)).Select(kvp => new DateStackStatsResult {
                    Date = kvp.Key,
                    Total = kvp.Sum(b => b.Value)
                }).ToList()
            };

            stats.ApplyTimeOffset(utcOffset);

            return stats;
        }

        private Dictionary<string, int> Merge(Dictionary<string, int> destination, Dictionary<string, int> source) {
            foreach (string key in source.Keys) {
                if (destination.ContainsKey(key))
                    destination[key] += source[key];
                else
                    destination.Add(key, source[key]);
            }

            return destination;
        }

        public void Process(PersistentEvent data, bool isNew, IEnumerable<TimeSpan> utcOffsets) {
            var updateActions = new List<Action> {
                () => IncrementDayStackStats(data),
                () => IncrementDayProjectStats(data, isNew)
            };

            foreach (TimeSpan utcOffset in utcOffsets) {
                DateTime localDate = data.Date.UtcDateTime.Add(utcOffset);
                updateActions.Add(() => IncrementMonthStackStats(data, localDate, utcOffset));
                updateActions.Add(() => IncrementMonthProjectStats(data, isNew, localDate, utcOffset));
            }

            Parallel.Invoke(updateActions.ToArray());
        }

        public void DecrementMonthProjectStatsByStackIds(IEnumerable<MonthProjectStats> monthStats, string[] stackIds) {
            foreach (MonthProjectStats monthStat in monthStats) {
                foreach (string stackId in stackIds) {
                    if (!monthStat.StackIds.ContainsKey(stackId))
                        continue;

                    int monthCount = monthStat.StackIds[stackId];
                    if (monthStat.Total >= monthCount)
                        monthStat.Total -= monthCount;
                    else
                        monthStat.Total = 0;

                    monthStat.StackIds.Remove(stackId);

                    if (monthStat.NewStackIds.Contains(stackId)) {
                        if (monthStat.NewTotal > 0)
                            monthStat.NewTotal--;
                        monthStat.NewStackIds.Remove(stackId);
                    }

                    foreach (EventStatsWithStackIds ds in monthStat.DayStats.Values) {
                        if (!ds.StackIds.ContainsKey(stackId))
                            continue;

                        int dayCount = ds.StackIds[stackId];
                        if (ds.Total >= dayCount)
                            ds.Total -= dayCount;
                        else
                            ds.Total = 0;

                        ds.StackIds.Remove(stackId);

                        if (!ds.NewStackIds.Contains(stackId))
                            continue;

                        if (ds.NewTotal > 0)
                            ds.NewTotal--;

                        ds.NewStackIds.Remove(stackId);
                    }
                }
            }
        }

        public void DecrementMonthProjectStatsByStackId(string projectId, string stackId) {
            var monthStats = _monthProjectStats.GetByProjectId(projectId);
            foreach (var monthStat in monthStats) {
                if (!monthStat.StackIds.ContainsKey(stackId))
                    continue;

                int monthCount = monthStat.StackIds[stackId];

                IMongoQuery query = Query.EQ(MonthProjectStatsRepository.FieldNames.Id, monthStat.Id);
                UpdateBuilder update = Update.Inc(MonthProjectStatsRepository.FieldNames.Total, -monthCount)
                    .Unset(String.Format(MonthProjectStatsRepository.FieldNames.IdsFormat, stackId));

                if (monthStat.NewStackIds.Contains(stackId)) {
                    update.Inc(MonthProjectStatsRepository.FieldNames.NewTotal, -1);
                    update.Pull(MonthProjectStatsRepository.FieldNames.NewStackIds, new BsonObjectId(new ObjectId(stackId)));
                }

                foreach (var ds in monthStat.DayStats) {
                    if (!ds.Value.StackIds.ContainsKey(stackId))
                        continue;

                    int dayCount = ds.Value.StackIds[stackId];

                    if (ds.Value.Total <= dayCount) {
                        // remove the entire node since total will be zero after removing our stats
                        update.Unset(String.Format(MonthProjectStatsRepository.FieldNames.DayStats_Format, ds.Key));
                    } else {
                        update.Inc(String.Format(MonthProjectStatsRepository.FieldNames.DayStats_TotalFormat, ds.Key), -dayCount);
                        update.Unset(String.Format(MonthProjectStatsRepository.FieldNames.DayStats_IdsFormat, ds.Key, stackId));
                        if (ds.Value.NewStackIds.Contains(stackId)) {
                            update.Inc(String.Format(MonthProjectStatsRepository.FieldNames.DayStats_NewTotalFormat, ds.Key), -1);
                            update.Pull(String.Format(MonthProjectStatsRepository.FieldNames.DayStats_NewIdsFormat, ds.Key), stackId);
                        }
                    }
                }

                _monthProjectStats.Collection.Update(query, update);
            }
        }

        public void DecrementDayProjectStatsByStackIds(IEnumerable<DayProjectStats> dayStats, string[] stackIds) {
            foreach (DayProjectStats dayStat in dayStats) {
                foreach (string stackId in stackIds) {
                    if (!dayStat.StackIds.ContainsKey(stackId))
                        continue;

                    int dayCount = dayStat.StackIds[stackId];
                    if (dayStat.Total >= dayCount)
                        dayStat.Total -= dayCount;
                    else
                        dayStat.Total = 0;

                    dayStat.StackIds.Remove(stackId);

                    if (dayStat.NewStackIds.Contains(stackId)) {
                        if (dayStat.NewTotal > 0)
                            dayStat.NewTotal--;
                        dayStat.NewStackIds.Remove(stackId);
                    }

                    foreach (EventStatsWithStackIds ds in dayStat.MinuteStats.Values) {
                        if (!ds.StackIds.ContainsKey(stackId))
                            continue;

                        int minuteCount = ds.StackIds[stackId];
                        if (ds.Total >= minuteCount)
                            ds.Total -= minuteCount;
                        else
                            ds.Total = 0;

                        ds.StackIds.Remove(stackId);

                        if (!ds.NewStackIds.Contains(stackId))
                            continue;

                        if (ds.NewTotal > 0)
                            ds.NewTotal--;

                        ds.NewStackIds.Remove(stackId);
                    }
                }
            }
        }

        public void DecrementDayProjectStatsByStackId(string projectId, string stackId) {
            var dayStats = _dayProjectStats.GetByProjectId(projectId);
            foreach (DayProjectStats dayStat in dayStats) {
                if (!dayStat.StackIds.ContainsKey(stackId))
                    continue;

                int dayCount = dayStat.StackIds[stackId];

                IMongoQuery query = Query.EQ(DayProjectStatsRepository.FieldNames.Id, dayStat.Id);
                UpdateBuilder update = Update.Inc(DayProjectStatsRepository.FieldNames.Total, -dayCount)
                    .Unset(String.Format(DayProjectStatsRepository.FieldNames.IdsFormat, stackId));

                if (dayStat.NewStackIds.Contains(stackId)) {
                    update.Inc(DayProjectStatsRepository.FieldNames.NewTotal, -1);
                    update.Pull(DayProjectStatsRepository.FieldNames.NewStackIds, new BsonObjectId(new ObjectId(stackId)));
                }

                foreach (var ms in dayStat.MinuteStats) {
                    if (!ms.Value.StackIds.ContainsKey(stackId))
                        continue;

                    int minuteCount = ms.Value.StackIds[stackId];

                    if (ms.Value.Total <= minuteCount) {
                        // remove the entire node since total will be zero after removing our stats
                        update.Unset(String.Format(DayProjectStatsRepository.FieldNames.MinuteStats_Format, ms.Key));
                    } else {
                        update.Inc(String.Format(DayProjectStatsRepository.FieldNames.MinuteStats_TotalFormat, ms.Key), -minuteCount);
                        update.Unset(String.Format(DayProjectStatsRepository.FieldNames.MinuteStats_IdsFormat, ms.Key, stackId));
                        if (ms.Value.NewStackIds.Contains(stackId)) {
                            update.Inc(String.Format(DayProjectStatsRepository.FieldNames.MinuteStats_NewTotalFormat, ms.Key), -1);
                            update.Pull(String.Format(DayProjectStatsRepository.FieldNames.MinuteStats_NewIdsFormat, ms.Key), stackId);
                        }
                    }
                }

                _dayProjectStats.Collection.Update(query, update);
            }
        }

        private static readonly object _dayStackStatsLock = new object();

        private void IncrementDayStackStats(PersistentEvent data) {
            string id = GetDayStackStatsId(data.StackId, data.Date);

            IMongoQuery query = Query.EQ(DayStackStatsRepository.FieldNames.Id, id);
            UpdateBuilder update = Update
                .Inc(DayStackStatsRepository.FieldNames.Total, 1)
                .Inc(String.Format(DayStackStatsRepository.FieldNames.MinuteStats_Format, GetTimeBucket(data.Date).ToString("0000")), 1);

            WriteConcernResult result = _dayStackStats.Collection.Update(query, update);
            if (result.DocumentsAffected != 0)
                return;

            lock (_dayStackStatsLock) {
                try {
                    _dayStackStats.Save(CreateBlankDayStackStats(data));
                } catch (MongoDuplicateKeyException) {
                    // the doc was already created by another thread, update it.
                    result = _dayStackStats.Collection.Update(query, update);
                    if (result.DocumentsAffected == 0)
                        Log.Error().Project(data.ProjectId).Message("Unable to update or insert stats doc id (\"{0}\").", id).Write();
                }
            }
        }

        private DayStackStats CreateBlankDayStackStats(PersistentEvent data) {
            return CreateBlankDayStackStats(data.Date.UtcDateTime, data.StackId, data.ProjectId);
        }

        private DayStackStats CreateBlankDayStackStats(DateTime occurrenceDate, string stackId, string projectId = null) {
            bool hasError = !String.IsNullOrEmpty(projectId);

            // store stats in 15 minute buckets
            var bucketCounts = new Dictionary<string, int>();
            for (int i = 0; i < 1440; i += 15) {
                if (hasError && i == GetTimeBucket(occurrenceDate))
                    bucketCounts.Add(i.ToString("0000"), 1);
            }

            var s = new DayStackStats {
                Id = GetDayStackStatsId(stackId, occurrenceDate),
                ProjectId = projectId,
                StackId = stackId,
                Total = hasError ? 1 : 0,
                MinuteStats = bucketCounts
            };

            return s;
        }

        public static string GetDayStackStatsId(string stackId, DateTimeOffset date) {
            return String.Concat(stackId, "/", date.UtcDateTime.ToString("yyyyMMdd"));
        }

        private static readonly object _dayProjectStatsLock = new object();

        private void IncrementDayProjectStats(PersistentEvent data, bool isNew) {
            string id = GetDayProjectStatsId(data.ProjectId, data.Date);

            IMongoQuery query = Query.EQ(DayProjectStatsRepository.FieldNames.Id, id);
            UpdateBuilder update = Update
                .Inc(DayProjectStatsRepository.FieldNames.Total, 1)
                .Inc(DayProjectStatsRepository.FieldNames.NewTotal, isNew ? 1 : 0)
                .Inc(String.Format(DayProjectStatsRepository.FieldNames.MinuteStats_TotalFormat, GetTimeBucket(data.Date).ToString("0000")), 1)
                .Inc(String.Format(DayProjectStatsRepository.FieldNames.MinuteStats_NewTotalFormat, GetTimeBucket(data.Date).ToString("0000")), isNew ? 1 : 0)
                .Inc(String.Format(DayProjectStatsRepository.FieldNames.IdsFormat, data.StackId), 1)
                .Inc(String.Format(DayProjectStatsRepository.FieldNames.MinuteStats_IdsFormat, GetTimeBucket(data.Date).ToString("0000"), data.StackId), 1);

            if (isNew) {
                update.Push(DayProjectStatsRepository.FieldNames.NewStackIds, new BsonObjectId(new ObjectId(data.StackId)));
                update.Push(String.Format(DayProjectStatsRepository.FieldNames.MinuteStats_NewIdsFormat, GetTimeBucket(data.Date).ToString("0000")), new BsonObjectId(new ObjectId(data.StackId)));
            }

            WriteConcernResult result = _dayProjectStats.Collection.Update(query, update);
            if (result.DocumentsAffected != 0)
                return;

            lock (_dayProjectStatsLock) {
                try {
                    _dayProjectStats.Collection.Insert(CreateBlankDayProjectStats(data, isNew));
                } catch (MongoDuplicateKeyException) {
                    // the doc was already created by another thread, update it.
                    result = _dayProjectStats.Collection.Update(query, update);
                    if (result.DocumentsAffected == 0)
                        Log.Error().Project(data.ProjectId).Message("Unable to update or insert stats doc id (\"{0}\").", id).Write();
                }
            }
        }

        public static string GetDayProjectStatsId(string projectId, DateTimeOffset date) {
            return GetDayProjectStatsId(projectId, date.UtcDateTime);
        }

        public static string GetDayProjectStatsId(string projectId, DateTime utcDate) {
            return String.Concat(projectId, "/", utcDate.ToString("yyyyMMdd"));
        }

        private DayProjectStats CreateBlankDayProjectStats(PersistentEvent data, bool isNew) {
            return CreateBlankDayProjectStats(data.Date.UtcDateTime, data.ProjectId, data.StackId, isNew);
        }

        private DayProjectStats CreateBlankDayProjectStats(DateTime occurrenceDate, string projectId, string stackId = null, bool isNew = false) {
            bool hasEvent = !String.IsNullOrEmpty(stackId);

            // store stats in 15 minute buckets
            var bucketStats = new Dictionary<string, EventStatsWithStackIds>();
            for (int i = 0; i < 1440; i += 15) {
                var stat = new EventStatsWithStackIds();
                if (hasEvent && i == GetTimeBucket(occurrenceDate)) {
                    stat.Total = 1;
                    stat.NewTotal = isNew ? 1 : 0;
                    stat.StackIds.Add(stackId, 1);
                    if (isNew)
                        stat.NewStackIds.Add(stackId);

                    bucketStats.Add(i.ToString("0000"), stat);
                }
            }

            var s = new DayProjectStats {
                Id = GetDayProjectStatsId(projectId, occurrenceDate),
                ProjectId = projectId,
                Total = hasEvent ? 1 : 0,
                NewTotal = isNew ? 1 : 0,
                MinuteStats = bucketStats
            };
            if (hasEvent)
                s.StackIds.Add(stackId, 1);
            if (isNew)
                s.NewStackIds.Add(stackId);

            return s;
        }

        private static readonly object _monthStackStatsLock = new object();

        private void IncrementMonthStackStats(PersistentEvent data, DateTime localDate, TimeSpan utcOffset) {
            string id = GetMonthStackStatsId(localDate, utcOffset, data.StackId);

            IMongoQuery query = Query.EQ(MonthStackStatsRepository.FieldNames.Id, id);
            UpdateBuilder update = Update
                .Inc(MonthStackStatsRepository.FieldNames.Total, 1)
                .Inc(String.Format(MonthStackStatsRepository.FieldNames.DayStats_Format, localDate.Day), 1);

            WriteConcernResult result = _monthStackStats.Collection.Update(query, update);
            if (result.DocumentsAffected != 0)
                return;

            lock (_monthStackStatsLock) {
                try {
                    _monthStackStats.Add(CreateBlankMonthStackStats(utcOffset, localDate, data.StackId, data.ProjectId));
                } catch (MongoDuplicateKeyException) {
                    // the doc was already created by another thread, update it.
                    result = _monthStackStats.Collection.Update(query, update);
                    if (result.DocumentsAffected == 0)
                        Log.Error().Project(data.ProjectId).Message("Unable to update or insert stats doc id (\"{0}\").", id).Write();
                }
            }
        }

        public static string GetMonthStackStatsId(DateTime localDate, TimeSpan utcOffset, string stackId) {
            return GetMonthStackStatsId(localDate.Year, localDate.Month, utcOffset, stackId);
        }

        public static string GetMonthStackStatsId(int year, int month, TimeSpan utcOffset, string stackId) {
            return String.Concat(stackId, "/", (utcOffset < TimeSpan.Zero ? "-" : ""), utcOffset.ToString("hh\\:mm"), "/", year, month.ToString("00"));
        }

        private MonthStackStats CreateBlankMonthStackStats(TimeSpan utcOffset, DateTime localDate, string stackId, string projectId) {
            var dayStats = new Dictionary<string, int>();
            int daysInMonth = DateTime.DaysInMonth(localDate.Year, localDate.Month);
            bool hasEvent = !String.IsNullOrEmpty(projectId);
            for (int i = 1; i <= daysInMonth; i++) {
                if (hasEvent && i == localDate.Day)
                    dayStats.Add(i.ToString(), 1);
            }

            var s = new MonthStackStats {
                Id = GetMonthStackStatsId(localDate, utcOffset, stackId),
                ProjectId = projectId,
                StackId = stackId,
                Total = hasEvent ? 1 : 0,
                DayStats = dayStats
            };

            return s;
        }

        private static readonly object _monthProjectStatsLock = new object();

        private void IncrementMonthProjectStats(PersistentEvent data, bool isNew, DateTime localDate, TimeSpan utcOffset) {
            string id = GetMonthProjectStatsId(localDate, utcOffset, data.ProjectId);

            IMongoQuery query = Query.EQ(MonthProjectStatsRepository.FieldNames.Id, id);
            UpdateBuilder update = Update
                .Inc(MonthProjectStatsRepository.FieldNames.Total, 1)
                .Inc(MonthProjectStatsRepository.FieldNames.NewTotal, isNew ? 1 : 0)
                .Inc(String.Format(MonthProjectStatsRepository.FieldNames.DayStats_TotalFormat, localDate.Day), 1)
                .Inc(String.Format(MonthProjectStatsRepository.FieldNames.DayStats_NewTotalFormat, localDate.Day), isNew ? 1 : 0)
                .Inc(String.Format(MonthProjectStatsRepository.FieldNames.IdsFormat, data.StackId), 1)
                .Inc(String.Format(MonthProjectStatsRepository.FieldNames.DayStats_IdsFormat, localDate.Day, data.StackId), 1);

            if (isNew) {
                update.Push(MonthProjectStatsRepository.FieldNames.NewStackIds, new BsonObjectId(new ObjectId(data.StackId)));
                update.Push(String.Format(MonthProjectStatsRepository.FieldNames.DayStats_NewIdsFormat, localDate.Day), new BsonObjectId(new ObjectId(data.StackId)));
            }

            WriteConcernResult result = _monthProjectStats.Collection.Update(query, update);
            if (result.DocumentsAffected != 0)
                return;

            lock (_monthProjectStatsLock) {
                try {
                    _monthProjectStats.Add(CreateBlankMonthProjectStats(utcOffset, localDate, data.ProjectId, data.StackId, isNew));
                } catch (MongoDuplicateKeyException) {
                    // the doc was already created by another thread, update it.
                    result = _monthProjectStats.Collection.Update(query, update);
                    if (result.DocumentsAffected == 0)
                        Log.Error().Project(data.ProjectId).Message("Unable to update or insert stats doc id (\"{0}\").", id).Write();
                }
            }
        }

        public static string GetMonthProjectStatsId(DateTime localDate, TimeSpan utcOffset, string projectId) {
            return GetMonthProjectStatsId(localDate.Year, localDate.Month, utcOffset, projectId);
        }

        public static string GetMonthProjectStatsId(int year, int month, TimeSpan utcOffset, string projectId) {
            return String.Concat(projectId, "/", (utcOffset < TimeSpan.Zero ? "-" : ""), utcOffset.ToString("hh\\:mm"), "/", year, month.ToString("00"));
        }

        private MonthProjectStats CreateBlankMonthProjectStats(TimeSpan utcOffset, DateTime localDate, string projectId, string stackId = null, bool isNew = false) {
            var dayStats = new Dictionary<string, EventStatsWithStackIds>();
            int daysInMonth = DateTime.DaysInMonth(localDate.Year, localDate.Month);
            bool hasEvent = !String.IsNullOrEmpty(stackId);

            for (int i = 1; i <= daysInMonth; i++) {
                var stat = new EventStatsWithStackIds();
                if (hasEvent && i == localDate.Day) {
                    stat.Total = 1;
                    stat.NewTotal = isNew ? 1 : 0;
                    stat.StackIds.Add(stackId, 1);
                    if (isNew)
                        stat.NewStackIds.Add(stackId);

                    dayStats.Add(i.ToString(), stat);
                }
            }

            string id = GetMonthProjectStatsId(localDate, utcOffset, projectId);
            var s = new MonthProjectStats {
                Id = id,
                ProjectId = projectId,
                Total = hasEvent ? 1 : 0,
                NewTotal = isNew ? 1 : 0,
                DayStats = dayStats
            };

            if (hasEvent)
                s.StackIds.Add(stackId, 1);

            if (hasEvent && isNew)
                s.NewStackIds.Add(stackId);

            return s;
        }

        public static long GetTimeBucket(DateTime utcDateTime) {
            DateTime rounded = utcDateTime.Floor(TimeSpan.FromMinutes(15));
            return (rounded.Ticks % TimeSpan.TicksPerDay) / TimeSpan.TicksPerMinute;
        }

        public static long GetTimeBucket(DateTimeOffset dateTime) {
            return GetTimeBucket(dateTime.UtcDateTime);
        }

        internal static void MapStatsClasses() {
            if (!BsonClassMap.IsClassMapRegistered(typeof(EventStatsWithStackIds))) {
                BsonClassMap.RegisterClassMap<EventStatsWithStackIds>(cm2 => {
                    cm2.AutoMap();
                    cm2.GetMemberMap(c => c.StackIds).SetElementName("ids");
                    cm2.GetMemberMap(c => c.NewStackIds).SetElementName("newids").SetSerializationOptions(new ArraySerializationOptions(new RepresentationSerializationOptions(BsonType.ObjectId)));
                });
            }

            if (!BsonClassMap.IsClassMapRegistered(typeof(EventStats))) {
                BsonClassMap.RegisterClassMap<EventStats>(cm2 => {
                    cm2.AutoMap();
                    cm2.GetMemberMap(c => c.Total).SetElementName("tot");
                    cm2.GetMemberMap(c => c.NewTotal).SetElementName("new");
                });
            }
        }

        private Tuple<DateTime, DateTime> GetDateRange(DateTime? starTime, DateTime? endTime, TimeSpan utcOffset, TimeSpan roundingInterval) {
            if (starTime == null)
                starTime = new DateTime(2012, 1, 1);

            if (endTime == null)
                endTime = DateTime.UtcNow.Add(utcOffset);

            // Round date range to blocks of X (minutes/hours) since stats are per X (minutes/hours) block.
            starTime = starTime.Value.Floor(roundingInterval);
            endTime = endTime.Value.Ceiling(roundingInterval);

            return starTime < endTime ? new Tuple<DateTime, DateTime>(starTime.Value, endTime.Value) : new Tuple<DateTime, DateTime>(endTime.Value, starTime.Value);
        }
    }
}