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
    public class ErrorStatsHelper {
        private readonly ErrorStackRepository _errorStackRepository;
        private readonly DayStackStatsRepository _dayStackStats;
        private readonly MonthStackStatsRepository _monthStackStats;
        private readonly DayProjectStatsRepository _dayProjectStats;
        private readonly MonthProjectStatsRepository _monthProjectStats;

        public ErrorStatsHelper(ErrorStackRepository errorStackRepository, DayStackStatsRepository dayStackStats,
            MonthStackStatsRepository monthStackStats, DayProjectStatsRepository dayProjectStats,
            MonthProjectStatsRepository monthProjectStats) {
            _errorStackRepository = errorStackRepository;
            _dayStackStats = dayStackStats;
            _monthStackStats = monthStackStats;
            _dayProjectStats = dayProjectStats;
            _monthProjectStats = monthProjectStats;
        }

        public ProjectErrorStatsResult GetProjectErrorStats(string projectId, TimeSpan utcOffset, DateTime? localStartDate = null, DateTime? localEndDate = null, DateTime? retentionStartDate = null, bool includeHidden = false, bool includeFixed = false, bool include404s = true) {
            if (localStartDate == null)
                localStartDate = new DateTime(2012, 1, 1);

            if (localEndDate == null)
                localEndDate = DateTime.UtcNow.Add(utcOffset);

            // round date range to blocks of 15 minutes since stats are per 15 minute block
            localStartDate = localStartDate.Value.Floor(TimeSpan.FromMinutes(15));
            localEndDate = localEndDate.Value.Ceiling(TimeSpan.FromMinutes(15));
            if (retentionStartDate.HasValue)
                retentionStartDate = retentionStartDate.Value.Floor(TimeSpan.FromMinutes(15));

            if (localEndDate.Value <= localStartDate.Value)
                throw new ArgumentException("End date must be greater than start date.", "localEndDate");

            // TODO: Check to see if we have day stats available for the project, if not, then use minute stats and queue new project offset to be created in a job

            // use finer grained minute blocks if the range is less than 5 days
            if (localEndDate.Value.Subtract(localStartDate.Value).TotalDays < 5)
                return GetProjectErrorStatsByMinuteBlock(projectId, utcOffset, localStartDate, localEndDate, retentionStartDate, includeHidden, includeFixed, include404s);

            return GetProjectErrorStatsByDay(projectId, utcOffset, localStartDate, localEndDate, retentionStartDate, includeHidden, includeFixed, include404s);
        }

        public ProjectErrorStatsResult GetProjectErrorStatsByDay(string projectId, TimeSpan utcOffset, DateTime? localStartDate = null, DateTime? localEndDate = null, DateTime? retentionStartDate = null, bool includeHidden = false, bool includeFixed = false, bool includeNotFound = true) {
            if (localStartDate == null)
                localStartDate = new DateTime(2012, 1, 1);

            if (localEndDate == null)
                localEndDate = DateTime.UtcNow.Add(utcOffset);

            // TODO: Check to see if we have day stats available for the project, if not, then use minute stats and queue new project offset to be created in a job

            // round date range to be full days since stats are per day
            localStartDate = localStartDate.Value.Floor(TimeSpan.FromDays(1));
            localEndDate = localEndDate.Value.Ceiling(TimeSpan.FromDays(1));
            if (retentionStartDate.HasValue)
                retentionStartDate = retentionStartDate.Value.Floor(TimeSpan.FromDays(1));

            if (localEndDate.Value <= localStartDate.Value)
                throw new ArgumentException("End date must be greater than start date.", "localEndDate");

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
                    // remove stats from hidden doc ids.
                    var hiddenIds = _errorStackRepository.GetHiddenIds(projectId);
                    if (hiddenIds.Length > 0)
                        DecrementMonthProjectStatsByStackIds(results, hiddenIds);
                }

                if (!includeNotFound) {
                    // remove stats from not found doc ids.
                    var notFoundIds = _errorStackRepository.GetNotFoundIds(projectId);
                    if (notFoundIds.Length > 0)
                        DecrementMonthProjectStatsByStackIds(results, notFoundIds);
                }

                if (!includeFixed) {
                    // remove stats from not found doc ids.
                    var fixedIds = _errorStackRepository.GetFixedIds(projectId);
                    if (fixedIds.Length > 0)
                        DecrementMonthProjectStatsByStackIds(results, fixedIds);
                }
            }

            // use finer grained minute blocks if the range is less than 5 days
            if (localEndDate.Value.Subtract(localStartDate.Value).TotalDays < 5)
                return GetProjectErrorStatsByMinuteBlock(projectId, utcOffset, localStartDate, localEndDate, retentionStartDate, includeHidden, includeFixed, includeNotFound);

            var monthDocIds = new List<Tuple<int, int>>();
            DateTime currentDay = localStartDate.Value;
            var endOfMonthEndDate = new DateTime(localEndDate.Value.Year, localEndDate.Value.Month, DateTime.DaysInMonth(localEndDate.Value.Year, localEndDate.Value.Month));
            while (currentDay <= endOfMonthEndDate) {
                monthDocIds.Add(Tuple.Create(currentDay.Year, currentDay.Month));
                currentDay = currentDay.AddMonths(1);
            }

            // add missing month documents
            foreach (var monthDocId in monthDocIds) {
                if (!results.Exists(d => d.Id == GetMonthProjectStatsId(monthDocId.Item1, monthDocId.Item2, utcOffset, projectId)))
                    results.Add(CreateBlankMonthProjectStats(utcOffset, new DateTime(monthDocId.Item1, monthDocId.Item2, 1), projectId));
            }

            // fill out missing days with blank stats
            foreach (MonthProjectStats r in results) {
                DateTime date = r.GetDateFromDayStatKey("1");
                int daysInMonth = DateTime.DaysInMonth(date.Year, date.Month);
                for (int i = 1; i <= daysInMonth; i++) {
                    if (!r.DayStats.ContainsKey(i.ToString()))
                        r.DayStats.Add(i.ToString(), new ErrorStatsWithStackIds());
                }
            }

            var days = new List<KeyValuePair<DateTime, ErrorStatsWithStackIds>>();
            days = results.Aggregate(days, (current, result) => current.Concat(result.DayStats.ToDictionary(kvp => result.GetDateFromDayStatKey(kvp.Key), kvp => kvp.Value)).ToList())
                .Where(kvp => kvp.Key >= localStartDate.Value && kvp.Key <= localEndDate.Value).OrderBy(kvp => kvp.Key).ToList();

            int totalLimitedByPlan = retentionStartDate != null && localStartDate < retentionStartDate
                ? days.Where(kvp => kvp.Key < retentionStartDate).SelectMany(kvp => kvp.Value.ErrorStackIds.Select(s => s.Key)).Distinct()
                    .Except(days.Where(kvp => kvp.Key >= retentionStartDate).SelectMany(kvp => kvp.Value.ErrorStackIds.Select(s => s.Key)).Distinct())
                    .Count()
                : 0;

            if (totalLimitedByPlan > 0)
                days = days.Where(kvp => kvp.Key >= retentionStartDate).ToList();

            // group data points by a time span to limit the number of returned data points
            TimeSpan groupTimeSpan = TimeSpan.FromDays(1);
            if (days.Count > 50) {
                DateTime first = days.Min(m => m.Key);
                DateTime last = days.Max(m => m.Key);
                TimeSpan span = last - first;
                groupTimeSpan = TimeSpan.FromDays(((int)Math.Round((span.TotalDays / 50) / 1.0)) * 1);
            }

            var stats = new ProjectErrorStatsResult {
                TotalLimitedByPlan = totalLimitedByPlan,
                StartDate = localStartDate.Value,
                EndDate = localEndDate.Value,
                Total = days.Sum(kvp => kvp.Value.Total),
                UniqueTotal = days.SelectMany(kvp => kvp.Value.ErrorStackIds.Select(s => s.Key)).Distinct().Count(),
                NewTotal = days.Sum(kvp => kvp.Value.NewTotal),
                MostFrequent = new PlanPagedResult<ErrorStackResult>(days.SelectMany(kvp => kvp.Value.ErrorStackIds).GroupBy(kvp => kvp.Key)
                    .Select(v => new ErrorStackResult {
                        Id = v.Key,
                        Total = v.Sum(kvp => kvp.Value)
                    }).OrderByDescending(s => s.Total).ToList(), totalLimitedByPlan),
                Stats = days.GroupBy(s => s.Key.Floor(groupTimeSpan)).Select(kvp => new DateProjectStatsResult {
                    Date = kvp.Key,
                    Total = kvp.Sum(b => b.Value.Total),
                    NewTotal = kvp.Sum(b => b.Value.NewTotal),
                    UniqueTotal = kvp.Select(b => b.Value.ErrorStackIds).Aggregate((current, result) => Merge(result, current)).Count()
                }).ToList()
            };

            return stats;
        }

        public ProjectErrorStatsResult GetProjectErrorStatsByMinuteBlock(string projectId, TimeSpan utcOffset, DateTime? localStartDate = null, DateTime? localEndDate = null, DateTime? retentionStartDate = null, bool includeHidden = false, bool includeFixed = false, bool includeNotFound = true) {
            if (localStartDate == null)
                localStartDate = new DateTime(2012, 1, 1);

            if (localEndDate == null)
                localEndDate = DateTime.UtcNow.Add(utcOffset);

            // round date range to blocks of 15 minutes since stats are per 15 minute block
            localStartDate = localStartDate.Value.Floor(TimeSpan.FromMinutes(15));
            localEndDate = localEndDate.Value.Ceiling(TimeSpan.FromMinutes(15));
            if (retentionStartDate.HasValue)
                retentionStartDate = retentionStartDate.Value.Floor(TimeSpan.FromMinutes(15));

            if (localEndDate.Value <= localStartDate.Value)
                throw new ArgumentException("End date must be greater than start date.", "localEndDate");

            DateTime utcStartDate = new DateTimeOffset(localStartDate.Value.Ticks, utcOffset).UtcDateTime;
            DateTime utcEndDate = new DateTimeOffset(localEndDate.Value.Ticks, utcOffset).UtcDateTime;

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
                    string[] hiddenIds = _errorStackRepository.GetHiddenIds(projectId);
                    if (hiddenIds.Length > 0)
                        DecrementDayProjectStatsByStackIds(results, hiddenIds);
                }

                if (!includeNotFound) {
                    // remove stats from not found doc ids.
                    string[] notFoundIds = _errorStackRepository.GetNotFoundIds(projectId);
                    if (notFoundIds.Length > 0)
                        DecrementDayProjectStatsByStackIds(results, notFoundIds);
                }

                if (!includeFixed) {
                    // remove stats from not found doc ids.
                    string[] fixedIds = _errorStackRepository.GetFixedIds(projectId);
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
                        r.MinuteStats.Add(minuteBlock.ToString("0000"), new ErrorStatsWithStackIds());
                }
            }

            var minuteBlocks = new List<KeyValuePair<DateTime, ErrorStatsWithStackIds>>();
            minuteBlocks = results.Aggregate(minuteBlocks, (current, result) => current.Concat(result.MinuteStats.ToDictionary(kvp => result.GetDateFromMinuteStatKey(kvp.Key), kvp => kvp.Value)).ToList())
                .Where(kvp => kvp.Key >= utcStartDate && kvp.Key <= utcEndDate).OrderBy(kvp => kvp.Key).ToList();

            int totalLimitedByPlan = retentionStartDate != null && utcStartDate < retentionStartDate
                ? minuteBlocks.Where(kvp => kvp.Key < retentionStartDate).SelectMany(kvp => kvp.Value.ErrorStackIds.Select(s => s.Key)).Distinct()
                    .Except(minuteBlocks.Where(kvp => kvp.Key >= retentionStartDate).SelectMany(kvp => kvp.Value.ErrorStackIds.Select(s => s.Key)).Distinct())
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

            var stats = new ProjectErrorStatsResult {
                TotalLimitedByPlan = totalLimitedByPlan,
                StartDate = utcStartDate,
                EndDate = utcEndDate,
                Total = minuteBlocks.Sum(kvp => kvp.Value.Total),
                UniqueTotal = minuteBlocks.SelectMany(kvp => kvp.Value.ErrorStackIds.Select(s => s.Key)).Distinct().Count(),
                NewTotal = minuteBlocks.Sum(kvp => kvp.Value.NewTotal),
                MostFrequent = new PlanPagedResult<ErrorStackResult>(minuteBlocks.SelectMany(kvp => kvp.Value.ErrorStackIds).GroupBy(kvp => kvp.Key)
                    .Select(v => new ErrorStackResult {
                        Id = v.Key,
                        Total = v.Sum(kvp => kvp.Value),
                    }).OrderByDescending(s => s.Total).ToList(), totalLimitedByPlan),
                Stats = minuteBlocks.GroupBy(s => s.Key.Floor(groupTimeSpan)).Select(kvp => new DateProjectStatsResult {
                    Date = kvp.Key,
                    Total = kvp.Sum(b => b.Value.Total),
                    NewTotal = kvp.Sum(b => b.Value.NewTotal),
                    UniqueTotal = kvp.Select(b => b.Value.ErrorStackIds).Aggregate((current, result) => Merge(result, current)).Count()
                }).ToList()
            };

            stats.ApplyTimeOffset(utcOffset);

            return stats;
        }

        public StackStatsResult GetErrorStackStats(string errorStackId, TimeSpan utcOffset, DateTime? localStartDate = null, DateTime? localEndDate = null, DateTime? retentionStartDate = null) {
            if (localStartDate == null)
                localStartDate = new DateTime(2012, 1, 1);

            if (localEndDate == null)
                localEndDate = DateTime.UtcNow.Add(utcOffset);

            // round date range to blocks of 15 minutes since stats are per 15 minute block
            localStartDate = localStartDate.Value.Floor(TimeSpan.FromMinutes(15));
            localEndDate = localEndDate.Value.Ceiling(TimeSpan.FromMinutes(15));
            if (retentionStartDate.HasValue)
                retentionStartDate = retentionStartDate.Value.Floor(TimeSpan.FromMinutes(15));

            if (localEndDate.Value <= localStartDate.Value)
                throw new ArgumentException("End date must be greater than start date.", "localEndDate");

            // TODO: Check to see if we have day stats available for the project, if not, then use minute stats and queue new project offset to be created in a job

            // use finer grained minute blocks if the range is less than 5 days
            if (localEndDate.Value.Subtract(localStartDate.Value).TotalDays < 5)
                return GetErrorStackStatsByMinuteBlock(errorStackId, utcOffset, localStartDate, localEndDate, retentionStartDate);

            return GetErrorStackStatsByDay(errorStackId, utcOffset, localStartDate, localEndDate, retentionStartDate);
        }

        public StackStatsResult GetErrorStackStatsByDay(string errorStackId, TimeSpan utcOffset, DateTime? localStartDate = null, DateTime? localEndDate = null, DateTime? retentionStartDate = null) {
            if (localStartDate == null)
                localStartDate = new DateTime(2012, 1, 1);

            if (localEndDate == null)
                localEndDate = DateTime.UtcNow.Add(utcOffset);

            localStartDate = localStartDate.Value.Floor(TimeSpan.FromDays(1));
            localEndDate = localEndDate.Value.Ceiling(TimeSpan.FromDays(1));
            if (retentionStartDate.HasValue)
                retentionStartDate = retentionStartDate.Value.Floor(TimeSpan.FromDays(1));

            if (localEndDate.Value <= localStartDate.Value)
                throw new ArgumentException("End date must be greater than start date.", "localEndDate");

            var results = _monthStackStats.Collection.Find(
                Query.And(
                    Query.GTE(MonthStackStatsRepository.FieldNames.Id, GetMonthStackStatsId(localStartDate.Value, utcOffset, errorStackId)),
                    Query.LTE(MonthStackStatsRepository.FieldNames.Id, GetMonthStackStatsId(localEndDate.Value, utcOffset, errorStackId)))
                ).ToList();

            if (results.Count > 0) {
                var firstWithOccurrence = results.OrderBy(r => r.Id).FirstOrDefault(r => r.DayStats.Any(ds => ds.Value > 0));
                if (firstWithOccurrence != null) {
                    var firstErrorDate = firstWithOccurrence.GetDay(firstWithOccurrence.DayStats.OrderBy(ds => Int32.Parse(ds.Key)).First(ds => ds.Value > 0).Key);
                    if (localStartDate < firstErrorDate)
                        localStartDate = firstErrorDate.Floor(TimeSpan.FromDays(1));
                }
            }

            if (localEndDate.Value.Subtract(localStartDate.Value).TotalDays < 5)
                return GetErrorStackStatsByMinuteBlock(errorStackId, utcOffset, localStartDate, localEndDate);

            var monthDocIds = new List<Tuple<int, int>>();
            DateTime currentDay = localStartDate.Value;
            var endOfMonthEndDate = new DateTime(localEndDate.Value.Year, localEndDate.Value.Month, DateTime.DaysInMonth(localEndDate.Value.Year, localEndDate.Value.Month));
            while (currentDay <= endOfMonthEndDate) {
                monthDocIds.Add(Tuple.Create(currentDay.Year, currentDay.Month));
                currentDay = currentDay.AddMonths(1);
            }

            foreach (var monthDocId in monthDocIds) {
                if (!results.Exists(d => d.Id == GetMonthStackStatsId(monthDocId.Item1, monthDocId.Item2, utcOffset, errorStackId)))
                    results.Add(CreateBlankMonthStackStats(utcOffset, new DateTime(monthDocId.Item1, monthDocId.Item2, 1), errorStackId, null));
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

        public StackStatsResult GetErrorStackStatsByMinuteBlock(string errorStackId, TimeSpan utcOffset, DateTime? localStartDate = null, DateTime? localEndDate = null, DateTime? retentionStartDate = null) {
            if (localStartDate == null)
                localStartDate = new DateTime(2012, 1, 1);

            if (localEndDate == null)
                localEndDate = DateTime.UtcNow.Add(utcOffset);

            localStartDate = localStartDate.Value.Floor(TimeSpan.FromMinutes(15));
            localEndDate = localEndDate.Value.Ceiling(TimeSpan.FromMinutes(15));
            if (retentionStartDate.HasValue)
                retentionStartDate = retentionStartDate.Value.Floor(TimeSpan.FromMinutes(15));

            if (localEndDate.Value <= localStartDate.Value)
                throw new ArgumentException("End date must be greater than start date.", "localEndDate");

            DateTime utcStartDate = new DateTimeOffset(localStartDate.Value.Ticks, utcOffset).UtcDateTime;
            DateTime utcEndDate = new DateTimeOffset(localEndDate.Value.Ticks, utcOffset).UtcDateTime;

            List<DayStackStats> results = _dayStackStats.Collection.Find(
                Query.And(
                    Query.GTE(MonthStackStatsRepository.FieldNames.Id, GetDayStackStatsId(errorStackId, utcStartDate)), 
                    Query.LTE(MonthStackStatsRepository.FieldNames.Id, GetDayStackStatsId(errorStackId, utcEndDate)))
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
                if (!results.Exists(d => d.Id == GetDayStackStatsId(errorStackId, dayDocDate)))
                    results.Add(CreateBlankDayStackStats(dayDocDate, errorStackId));
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

        public void Process(Error error, bool isNew, IEnumerable<TimeSpan> utcOffsets) {
            var updateActions = new List<Action> {
                () => IncrementDayStackStats(error),
                () => IncrementDayProjectStats(error, isNew)
            };

            foreach (TimeSpan utcOffset in utcOffsets) {
                DateTime localDate = error.OccurrenceDate.UtcDateTime.Add(utcOffset);
                updateActions.Add(() => IncrementMonthStackStats(error, localDate, utcOffset));
                updateActions.Add(() => IncrementMonthProjectStats(error, isNew, localDate, utcOffset));
            }

            Parallel.Invoke(updateActions.ToArray());
        }

        public void DecrementMonthProjectStatsByStackIds(IEnumerable<MonthProjectStats> monthStats, string[] errorStackIds) {
            foreach (MonthProjectStats monthStat in monthStats) {
                foreach (string errorStackId in errorStackIds) {
                    if (!monthStat.ErrorStackIds.ContainsKey(errorStackId))
                        continue;

                    int monthCount = monthStat.ErrorStackIds[errorStackId];
                    if (monthStat.Total >= monthCount)
                        monthStat.Total -= monthCount;
                    else
                        monthStat.Total = 0;

                    monthStat.ErrorStackIds.Remove(errorStackId);

                    if (monthStat.NewErrorStackIds.Contains(errorStackId)) {
                        if (monthStat.NewTotal > 0)
                            monthStat.NewTotal--;
                        monthStat.NewErrorStackIds.Remove(errorStackId);
                    }

                    foreach (ErrorStatsWithStackIds ds in monthStat.DayStats.Values) {
                        if (!ds.ErrorStackIds.ContainsKey(errorStackId))
                            continue;

                        int dayCount = ds.ErrorStackIds[errorStackId];
                        if (ds.Total >= dayCount)
                            ds.Total -= dayCount;
                        else
                            ds.Total = 0;

                        ds.ErrorStackIds.Remove(errorStackId);

                        if (!ds.NewErrorStackIds.Contains(errorStackId))
                            continue;

                        if (ds.NewTotal > 0)
                            ds.NewTotal--;

                        ds.NewErrorStackIds.Remove(errorStackId);
                    }
                }
            }
        }

        public void DecrementMonthProjectStatsByStackId(string projectId, string errorStackId) {
            var monthStats = _monthProjectStats.Where(s => s.ProjectId == projectId);
            foreach (var monthStat in monthStats) {
                if (!monthStat.ErrorStackIds.ContainsKey(errorStackId))
                    continue;

                int monthCount = monthStat.ErrorStackIds[errorStackId];

                IMongoQuery query = Query.EQ(MonthProjectStatsRepository.FieldNames.Id, monthStat.Id);
                UpdateBuilder update = Update.Inc(MonthProjectStatsRepository.FieldNames.Total, -monthCount)
                    .Unset(String.Format(MonthProjectStatsRepository.FieldNames.IdsFormat, errorStackId));

                if (monthStat.NewErrorStackIds.Contains(errorStackId)) {
                    update.Inc(MonthProjectStatsRepository.FieldNames.NewTotal, -1);
                    update.Pull(MonthProjectStatsRepository.FieldNames.NewErrorStackIds, new BsonObjectId(new ObjectId(errorStackId)));
                }

                foreach (var ds in monthStat.DayStats) {
                    if (!ds.Value.ErrorStackIds.ContainsKey(errorStackId))
                        continue;

                    int dayCount = ds.Value.ErrorStackIds[errorStackId];

                    if (ds.Value.Total <= dayCount) {
                        // remove the entire node since total will be zero after removing our stats
                        update.Unset(String.Format(MonthProjectStatsRepository.FieldNames.DayStats_Format, ds.Key));
                    } else {
                        update.Inc(String.Format(MonthProjectStatsRepository.FieldNames.DayStats_TotalFormat, ds.Key), -dayCount);
                        update.Unset(String.Format(MonthProjectStatsRepository.FieldNames.DayStats_IdsFormat, ds.Key, errorStackId));
                        if (ds.Value.NewErrorStackIds.Contains(errorStackId)) {
                            update.Inc(String.Format(MonthProjectStatsRepository.FieldNames.DayStats_NewTotalFormat, ds.Key), -1);
                            update.Pull(String.Format(MonthProjectStatsRepository.FieldNames.DayStats_NewIdsFormat, ds.Key), errorStackId);
                        }
                    }
                }

                _monthProjectStats.Collection.Update(query, update);
            }
        }

        public void DecrementDayProjectStatsByStackIds(IEnumerable<DayProjectStats> dayStats, string[] errorStackIds) {
            foreach (DayProjectStats dayStat in dayStats) {
                foreach (string errorStackId in errorStackIds) {
                    if (!dayStat.ErrorStackIds.ContainsKey(errorStackId))
                        continue;

                    int dayCount = dayStat.ErrorStackIds[errorStackId];
                    if (dayStat.Total >= dayCount)
                        dayStat.Total -= dayCount;
                    else
                        dayStat.Total = 0;

                    dayStat.ErrorStackIds.Remove(errorStackId);

                    if (dayStat.NewErrorStackIds.Contains(errorStackId)) {
                        if (dayStat.NewTotal > 0)
                            dayStat.NewTotal--;
                        dayStat.NewErrorStackIds.Remove(errorStackId);
                    }

                    foreach (ErrorStatsWithStackIds ds in dayStat.MinuteStats.Values) {
                        if (!ds.ErrorStackIds.ContainsKey(errorStackId))
                            continue;

                        int minuteCount = ds.ErrorStackIds[errorStackId];
                        if (ds.Total >= minuteCount)
                            ds.Total -= minuteCount;
                        else
                            ds.Total = 0;

                        ds.ErrorStackIds.Remove(errorStackId);

                        if (!ds.NewErrorStackIds.Contains(errorStackId))
                            continue;

                        if (ds.NewTotal > 0)
                            ds.NewTotal--;

                        ds.NewErrorStackIds.Remove(errorStackId);
                    }
                }
            }
        }

        public void DecrementDayProjectStatsByStackId(string projectId, string errorStackId) {
            IQueryable<DayProjectStats> dayStats = _dayProjectStats.Where(s => s.ProjectId == projectId);
            foreach (DayProjectStats dayStat in dayStats) {
                if (!dayStat.ErrorStackIds.ContainsKey(errorStackId))
                    continue;

                int dayCount = dayStat.ErrorStackIds[errorStackId];

                IMongoQuery query = Query.EQ(DayProjectStatsRepository.FieldNames.Id, dayStat.Id);
                UpdateBuilder update = Update.Inc(DayProjectStatsRepository.FieldNames.Total, -dayCount)
                    .Unset(String.Format(DayProjectStatsRepository.FieldNames.IdsFormat, errorStackId));

                if (dayStat.NewErrorStackIds.Contains(errorStackId)) {
                    update.Inc(DayProjectStatsRepository.FieldNames.NewTotal, -1);
                    update.Pull(DayProjectStatsRepository.FieldNames.NewErrorStackIds, new BsonObjectId(new ObjectId(errorStackId)));
                }

                foreach (var ms in dayStat.MinuteStats) {
                    if (!ms.Value.ErrorStackIds.ContainsKey(errorStackId))
                        continue;

                    int minuteCount = ms.Value.ErrorStackIds[errorStackId];

                    if (ms.Value.Total <= minuteCount) {
                        // remove the entire node since total will be zero after removing our stats
                        update.Unset(String.Format(DayProjectStatsRepository.FieldNames.MinuteStats_Format, ms.Key));
                    } else {
                        update.Inc(String.Format(DayProjectStatsRepository.FieldNames.MinuteStats_TotalFormat, ms.Key), -minuteCount);
                        update.Unset(String.Format(DayProjectStatsRepository.FieldNames.MinuteStats_IdsFormat, ms.Key, errorStackId));
                        if (ms.Value.NewErrorStackIds.Contains(errorStackId)) {
                            update.Inc(String.Format(DayProjectStatsRepository.FieldNames.MinuteStats_NewTotalFormat, ms.Key), -1);
                            update.Pull(String.Format(DayProjectStatsRepository.FieldNames.MinuteStats_NewIdsFormat, ms.Key), errorStackId);
                        }
                    }
                }

                _dayProjectStats.Collection.Update(query, update);
            }
        }

        private static readonly object _dayStackStatsLock = new object();

        private void IncrementDayStackStats(Error error) {
            string id = GetDayStackStatsId(error.ErrorStackId, error.OccurrenceDate);

            IMongoQuery query = Query.EQ(DayStackStatsRepository.FieldNames.Id, id);
            UpdateBuilder update = Update
                .Inc(DayStackStatsRepository.FieldNames.Total, 1)
                .Inc(String.Format(DayStackStatsRepository.FieldNames.MinuteStats_Format, GetTimeBucket(error.OccurrenceDate).ToString("0000")), 1);

            WriteConcernResult result = _dayStackStats.Collection.Update(query, update);
            if (result.DocumentsAffected != 0)
                return;

            lock (_dayStackStatsLock) {
                try {
                    _dayStackStats.Collection.Insert(CreateBlankDayStackStats(error));
                } catch (MongoDuplicateKeyException) {
                    // the doc was already created by another thread, update it.
                    result = _dayStackStats.Collection.Update(query, update);
                    if (result.DocumentsAffected == 0)
                        Log.Error().Project(error.ProjectId).Message("Unable to update or insert stats doc id (\"{0}\").", id).Write();
                }
            }
        }

        private DayStackStats CreateBlankDayStackStats(Error error) {
            return CreateBlankDayStackStats(error.OccurrenceDate.UtcDateTime, error.ErrorStackId, error.ProjectId);
        }

        private DayStackStats CreateBlankDayStackStats(DateTime occurrenceDate, string errorStackId, string projectId = null) {
            bool hasError = !String.IsNullOrEmpty(projectId);

            // store stats in 15 minute buckets
            var bucketCounts = new Dictionary<string, int>();
            for (int i = 0; i < 1440; i += 15) {
                if (hasError && i == GetTimeBucket(occurrenceDate))
                    bucketCounts.Add(i.ToString("0000"), 1);
            }

            var s = new DayStackStats {
                Id = GetDayStackStatsId(errorStackId, occurrenceDate),
                ProjectId = projectId,
                ErrorStackId = errorStackId,
                Total = hasError ? 1 : 0,
                MinuteStats = bucketCounts
            };

            return s;
        }

        public static string GetDayStackStatsId(string errorStackId, DateTimeOffset date) {
            return String.Concat(errorStackId, "/", date.UtcDateTime.ToString("yyyyMMdd"));
        }

        private static readonly object _dayProjectStatsLock = new object();

        private void IncrementDayProjectStats(Error error, bool isNew) {
            string id = GetDayProjectStatsId(error.ProjectId, error.OccurrenceDate);

            IMongoQuery query = Query.EQ(DayProjectStatsRepository.FieldNames.Id, id);
            UpdateBuilder update = Update
                .Inc(DayProjectStatsRepository.FieldNames.Total, 1)
                .Inc(DayProjectStatsRepository.FieldNames.NewTotal, isNew ? 1 : 0)
                .Inc(String.Format(DayProjectStatsRepository.FieldNames.MinuteStats_TotalFormat, GetTimeBucket(error.OccurrenceDate).ToString("0000")), 1)
                .Inc(String.Format(DayProjectStatsRepository.FieldNames.MinuteStats_NewTotalFormat, GetTimeBucket(error.OccurrenceDate).ToString("0000")), isNew ? 1 : 0)
                .Inc(String.Format(DayProjectStatsRepository.FieldNames.IdsFormat, error.ErrorStackId), 1)
                .Inc(String.Format(DayProjectStatsRepository.FieldNames.MinuteStats_IdsFormat, GetTimeBucket(error.OccurrenceDate).ToString("0000"), error.ErrorStackId), 1);

            if (isNew) {
                update.Push(DayProjectStatsRepository.FieldNames.NewErrorStackIds, new BsonObjectId(new ObjectId(error.ErrorStackId)));
                update.Push(String.Format(DayProjectStatsRepository.FieldNames.MinuteStats_NewIdsFormat, GetTimeBucket(error.OccurrenceDate).ToString("0000")), new BsonObjectId(new ObjectId(error.ErrorStackId)));
            }

            WriteConcernResult result = _dayProjectStats.Collection.Update(query, update);
            if (result.DocumentsAffected != 0)
                return;

            lock (_dayProjectStatsLock) {
                try {
                    _dayProjectStats.Collection.Insert(CreateBlankDayProjectStats(error, isNew));
                } catch (MongoDuplicateKeyException) {
                    // the doc was already created by another thread, update it.
                    result = _dayProjectStats.Collection.Update(query, update);
                    if (result.DocumentsAffected == 0)
                        Log.Error().Project(error.ProjectId).Message("Unable to update or insert stats doc id (\"{0}\").", id).Write();
                }
            }
        }

        public static string GetDayProjectStatsId(string projectId, DateTimeOffset date) {
            return GetDayProjectStatsId(projectId, date.UtcDateTime);
        }

        public static string GetDayProjectStatsId(string projectId, DateTime utcDate) {
            return String.Concat(projectId, "/", utcDate.ToString("yyyyMMdd"));
        }

        private DayProjectStats CreateBlankDayProjectStats(Error error, bool isNew) {
            return CreateBlankDayProjectStats(error.OccurrenceDate.UtcDateTime, error.ProjectId, error.ErrorStackId, isNew);
        }

        private DayProjectStats CreateBlankDayProjectStats(DateTime occurrenceDate, string projectId, string errorStackId = null, bool isNew = false) {
            bool hasError = !String.IsNullOrEmpty(errorStackId);

            // store stats in 15 minute buckets
            var bucketStats = new Dictionary<string, ErrorStatsWithStackIds>();
            for (int i = 0; i < 1440; i += 15) {
                var stat = new ErrorStatsWithStackIds();
                if (hasError && i == GetTimeBucket(occurrenceDate)) {
                    stat.Total = 1;
                    stat.NewTotal = isNew ? 1 : 0;
                    stat.ErrorStackIds.Add(errorStackId, 1);
                    if (isNew)
                        stat.NewErrorStackIds.Add(errorStackId);

                    bucketStats.Add(i.ToString("0000"), stat);
                }
            }

            var s = new DayProjectStats {
                Id = GetDayProjectStatsId(projectId, occurrenceDate),
                ProjectId = projectId,
                Total = hasError ? 1 : 0,
                NewTotal = isNew ? 1 : 0,
                MinuteStats = bucketStats
            };
            if (hasError)
                s.ErrorStackIds.Add(errorStackId, 1);
            if (isNew)
                s.NewErrorStackIds.Add(errorStackId);

            return s;
        }

        private static readonly object _monthStackStatsLock = new object();

        private void IncrementMonthStackStats(Error error, DateTime localDate, TimeSpan utcOffset) {
            string id = GetMonthStackStatsId(localDate, utcOffset, error.ErrorStackId);

            IMongoQuery query = Query.EQ(MonthStackStatsRepository.FieldNames.Id, id);
            UpdateBuilder update = Update
                .Inc(MonthStackStatsRepository.FieldNames.Total, 1)
                .Inc(String.Format(MonthStackStatsRepository.FieldNames.DayStats_Format, localDate.Day), 1);

            WriteConcernResult result = _monthStackStats.Collection.Update(query, update);
            if (result.DocumentsAffected != 0)
                return;

            lock (_monthStackStatsLock) {
                try {
                    _monthStackStats.Collection.Insert(CreateBlankMonthStackStats(utcOffset, localDate, error.ErrorStackId, error.ProjectId));
                } catch (MongoDuplicateKeyException) {
                    // the doc was already created by another thread, update it.
                    result = _monthStackStats.Collection.Update(query, update);
                    if (result.DocumentsAffected == 0)
                        Log.Error().Project(error.ProjectId).Message("Unable to update or insert stats doc id (\"{0}\").", id).Write();
                }
            }
        }

        public static string GetMonthStackStatsId(DateTime localDate, TimeSpan utcOffset, string errorStackId) {
            return GetMonthStackStatsId(localDate.Year, localDate.Month, utcOffset, errorStackId);
        }

        public static string GetMonthStackStatsId(int year, int month, TimeSpan utcOffset, string errorStackId) {
            return String.Concat(errorStackId, "/", (utcOffset < TimeSpan.Zero ? "-" : ""), utcOffset.ToString("hh\\:mm"), "/", year, month.ToString("00"));
        }

        private MonthStackStats CreateBlankMonthStackStats(TimeSpan utcOffset, DateTime localDate, string errorStackId, string projectId) {
            var dayStats = new Dictionary<string, int>();
            int daysInMonth = DateTime.DaysInMonth(localDate.Year, localDate.Month);
            bool hasError = !String.IsNullOrEmpty(projectId);
            for (int i = 1; i <= daysInMonth; i++) {
                if (hasError && i == localDate.Day)
                    dayStats.Add(i.ToString(), 1);
            }

            var s = new MonthStackStats {
                Id = GetMonthStackStatsId(localDate, utcOffset, errorStackId),
                ProjectId = projectId,
                ErrorStackId = errorStackId,
                Total = hasError ? 1 : 0,
                DayStats = dayStats
            };

            return s;
        }

        private static readonly object _monthProjectStatsLock = new object();

        private void IncrementMonthProjectStats(Error error, bool isNew, DateTime localDate, TimeSpan utcOffset) {
            string id = GetMonthProjectStatsId(localDate, utcOffset, error.ProjectId);

            IMongoQuery query = Query.EQ(MonthProjectStatsRepository.FieldNames.Id, id);
            UpdateBuilder update = Update
                .Inc(MonthProjectStatsRepository.FieldNames.Total, 1)
                .Inc(MonthProjectStatsRepository.FieldNames.NewTotal, isNew ? 1 : 0)
                .Inc(String.Format(MonthProjectStatsRepository.FieldNames.DayStats_TotalFormat, localDate.Day), 1)
                .Inc(String.Format(MonthProjectStatsRepository.FieldNames.DayStats_NewTotalFormat, localDate.Day), isNew ? 1 : 0)
                .Inc(String.Format(MonthProjectStatsRepository.FieldNames.IdsFormat, error.ErrorStackId), 1)
                .Inc(String.Format(MonthProjectStatsRepository.FieldNames.DayStats_IdsFormat, localDate.Day, error.ErrorStackId), 1);

            if (isNew) {
                update.Push(MonthProjectStatsRepository.FieldNames.NewErrorStackIds, new BsonObjectId(new ObjectId(error.ErrorStackId)));
                update.Push(String.Format(MonthProjectStatsRepository.FieldNames.DayStats_NewIdsFormat, localDate.Day), new BsonObjectId(new ObjectId(error.ErrorStackId)));
            }

            WriteConcernResult result = _monthProjectStats.Collection.Update(query, update);
            if (result.DocumentsAffected != 0)
                return;

            lock (_monthProjectStatsLock) {
                try {
                    _monthProjectStats.Collection.Insert(CreateBlankMonthProjectStats(utcOffset, localDate, error.ProjectId, error.ErrorStackId, isNew));
                } catch (MongoDuplicateKeyException) {
                    // the doc was already created by another thread, update it.
                    result = _monthProjectStats.Collection.Update(query, update);
                    if (result.DocumentsAffected == 0)
                        Log.Error().Project(error.ProjectId).Message("Unable to update or insert stats doc id (\"{0}\").", id).Write();
                }
            }
        }

        public static string GetMonthProjectStatsId(DateTime localDate, TimeSpan utcOffset, string projectId) {
            return GetMonthProjectStatsId(localDate.Year, localDate.Month, utcOffset, projectId);
        }

        public static string GetMonthProjectStatsId(int year, int month, TimeSpan utcOffset, string projectId) {
            return String.Concat(projectId, "/", (utcOffset < TimeSpan.Zero ? "-" : ""), utcOffset.ToString("hh\\:mm"), "/", year, month.ToString("00"));
        }

        private MonthProjectStats CreateBlankMonthProjectStats(TimeSpan utcOffset, DateTime localDate, string projectId, string errorStackId = null, bool isNew = false) {
            var dayStats = new Dictionary<string, ErrorStatsWithStackIds>();
            int daysInMonth = DateTime.DaysInMonth(localDate.Year, localDate.Month);
            bool hasError = !String.IsNullOrEmpty(errorStackId);

            for (int i = 1; i <= daysInMonth; i++) {
                var stat = new ErrorStatsWithStackIds();
                if (hasError && i == localDate.Day) {
                    stat.Total = 1;
                    stat.NewTotal = isNew ? 1 : 0;
                    stat.ErrorStackIds.Add(errorStackId, 1);
                    if (isNew)
                        stat.NewErrorStackIds.Add(errorStackId);

                    dayStats.Add(i.ToString(), stat);
                }
            }

            string id = GetMonthProjectStatsId(localDate, utcOffset, projectId);
            var s = new MonthProjectStats {
                Id = id,
                ProjectId = projectId,
                Total = hasError ? 1 : 0,
                NewTotal = isNew ? 1 : 0,
                DayStats = dayStats
            };

            if (hasError)
                s.ErrorStackIds.Add(errorStackId, 1);

            if (hasError && isNew)
                s.NewErrorStackIds.Add(errorStackId);

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
            if (!BsonClassMap.IsClassMapRegistered(typeof(ErrorStatsWithStackIds))) {
                BsonClassMap.RegisterClassMap<ErrorStatsWithStackIds>(cm2 => {
                    cm2.AutoMap();
                    cm2.GetMemberMap(c => c.ErrorStackIds).SetElementName("ids");
                    cm2.GetMemberMap(c => c.NewErrorStackIds).SetElementName("newids").SetSerializationOptions(new ArraySerializationOptions(new RepresentationSerializationOptions(BsonType.ObjectId)));
                });
            }

            if (!BsonClassMap.IsClassMapRegistered(typeof(ErrorStats))) {
                BsonClassMap.RegisterClassMap<ErrorStats>(cm2 => {
                    cm2.AutoMap();
                    cm2.GetMemberMap(c => c.Total).SetElementName("tot");
                    cm2.GetMemberMap(c => c.NewTotal).SetElementName("new");
                });
            }
        }
    }
}