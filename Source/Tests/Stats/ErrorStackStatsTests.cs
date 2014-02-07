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
using System.Diagnostics;
using System.Linq;
using CodeSmith.Core.Extensions;
using Exceptionless.Core;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.Membership;
using Exceptionless.Models;
using Exceptionless.Tests.Utility;
using Xunit;

namespace Exceptionless.Tests.Analytics {
    public class ErrorStackStatsTests : MongoRepositoryTestBaseWithIdentity<Error, IErrorRepository> {
        private readonly ResetDataHelper _resetDataHelper = IoC.GetInstance<ResetDataHelper>();
        private readonly ErrorStatsHelper _errorStatsHelper = IoC.GetInstance<ErrorStatsHelper>();
        private readonly IProjectRepository _projectRepository = IoC.GetInstance<IProjectRepository>();
        private readonly IOrganizationRepository _organizationRepository = IoC.GetInstance<IOrganizationRepository>();
        private readonly UserRepository _userRepository = IoC.GetInstance<UserRepository>();
        private readonly ErrorStackRepository _errorStackRepository = IoC.GetInstance<ErrorStackRepository>();

        private readonly DayStackStatsRepository _dayStackStats = IoC.GetInstance<DayStackStatsRepository>();
        private readonly MonthStackStatsRepository _monthStackStats = IoC.GetInstance<MonthStackStatsRepository>();
        private readonly DayProjectStatsRepository _dayProjectStats = IoC.GetInstance<DayProjectStatsRepository>();
        private readonly MonthProjectStatsRepository _monthProjectStats = IoC.GetInstance<MonthProjectStatsRepository>();

        private readonly ErrorPipeline _errorPipeline = IoC.GetInstance<ErrorPipeline>();

        public ErrorStackStatsTests() : base(IoC.GetInstance<IErrorRepository>(), true) {}

        [Fact]
        public void CanGetMinuteStats() {
            TimeSpan timeOffset = _projectRepository.GetDefaultTimeOffset(TestConstants.ProjectId);
            DateTime startDate = new DateTime(DateTime.UtcNow.Ticks, DateTimeKind.Unspecified).Add(timeOffset);
            DateTime utcStartDate = new DateTimeOffset(startDate, timeOffset).UtcDateTime;
            DateTime endDate = startDate.AddDays(2);
            DateTime utcEndDate = new DateTimeOffset(endDate, timeOffset).UtcDateTime;
            const int count = 100;

            List<Error> errors = ErrorData.GenerateErrors(count, startDate: startDate, endDate: endDate, errorStackId: TestConstants.ErrorStackId, projectId: TestConstants.ProjectId, timeZoneOffset: timeOffset).ToList();
            DateTimeOffset first = errors.Min(e => e.OccurrenceDate);
            Assert.True(first >= utcStartDate);
            DateTimeOffset last = errors.Max(e => e.OccurrenceDate);
            Assert.True(last <= utcEndDate);
            _errorPipeline.Run(errors);

            var info = _errorStatsHelper.GetProjectErrorStatsByMinuteBlock(TestConstants.ProjectId, timeOffset, startDate, endDate);
            Assert.Equal(count, info.Total);
            Assert.Equal(1, info.UniqueTotal);
            Assert.Equal(0, info.NewTotal);
            //Assert.Equal(1, info.Stats.Count);
            Assert.Equal(count, info.Stats.Sum(ds => ds.Total));
            Assert.True(info.Stats.All(ds => ds.UniqueTotal <= 1));
            Assert.Equal(0, info.Stats.Sum(ds => ds.NewTotal));
        }

        [Fact]
        public void CanAggregateStatsOverSmallTime() {
            TimeSpan timeOffset = _projectRepository.GetDefaultTimeOffset(TestConstants.ProjectId);
            DateTime startDate = DateTime.UtcNow.Add(timeOffset).Date;
            DateTime endDate = DateTime.UtcNow.Add(timeOffset).Date.AddMinutes(5);
            const int count = 25;

            List<Error> errors = ErrorData.GenerateErrors(count, startDate: startDate, endDate: endDate, errorStackId: TestConstants.ErrorStackId, projectId: TestConstants.ProjectId, timeZoneOffset: timeOffset).ToList();
            _errorPipeline.Run(errors);

            var info = _errorStatsHelper.GetProjectErrorStats(TestConstants.ProjectId, timeOffset, startDate, endDate);
            Assert.Equal(count, info.Total);
            Assert.Equal(1, info.UniqueTotal);
            Assert.Equal(0, info.NewTotal);
            //Assert.Equal(1, info.Stats.Count);
            Assert.Equal(count, info.Stats.Sum(ds => ds.Total));
            Assert.True(info.Stats.All(ds => ds.UniqueTotal <= 1));
            Assert.Equal(0, info.Stats.Sum(ds => ds.NewTotal));
        }

        [Fact]
        public void CanAggregateStatsOverTwoMonths() {
            _resetDataHelper.ResetProjectData(TestConstants.ProjectId);
            TimeSpan timeOffset = _projectRepository.GetDefaultTimeOffset(TestConstants.ProjectId);
            var overallsw = new Stopwatch();
            var sw = new Stopwatch();
            DateTime startDate = DateTime.UtcNow.Add(timeOffset).Date.AddMonths(-2);
            DateTime endDate = DateTime.UtcNow.Add(timeOffset).Date;
            const int count = 100;

            overallsw.Start();
            sw.Start();
            List<Error> errors = ErrorData.GenerateErrors(count, organizationId: TestConstants.OrganizationId, projectId: TestConstants.ProjectId, startDate: startDate, endDate: endDate, errorStackId: TestConstants.ErrorStackId, timeZoneOffset: timeOffset).ToList();
            sw.Stop();
            Console.WriteLine("Generate Errors: {0}", sw.Elapsed.ToWords(true));

            sw.Restart();
            _errorPipeline.Run(errors);
            sw.Stop();
            Console.WriteLine("Add Errors: {0}", sw.Elapsed.ToWords(true));

            sw.Restart();
            var info = _errorStatsHelper.GetProjectErrorStats(TestConstants.ProjectId, timeOffset, startDate, endDate);
            sw.Stop();
            Console.WriteLine("Get Stats: {0}", sw.Elapsed.ToWords(true));
            overallsw.Stop();
            Console.WriteLine("Overall: {0}", overallsw.Elapsed.ToWords(true));

            Assert.Equal(count, info.Total);
            Assert.InRange(info.UniqueTotal, 1, count);
            Assert.Equal(0, info.NewTotal);
            Assert.Equal(info.EndDate.Subtract(info.StartDate).TotalDays + 1, info.Stats.Count);
            Assert.Equal(count, info.Stats.Sum(ds => ds.Total));
            Assert.Equal(0, info.Stats.Sum(ds => ds.NewTotal));
        }

        [Fact]
        public void CanAggregateStatsOverSeveralMonths() {
            _resetDataHelper.ResetProjectData(TestConstants.ProjectId);
            TimeSpan timeOffset = _projectRepository.GetDefaultTimeOffset(TestConstants.ProjectId);
            DateTime startDate = DateTime.UtcNow.Add(timeOffset).Date.AddDays(-120);
            DateTime endDate = DateTime.UtcNow.Add(timeOffset).Date;
            const int count = 25;

            List<Error> errors = ErrorData.GenerateErrors(count, organizationId: TestConstants.OrganizationId, startDate: startDate, endDate: endDate, errorStackId: TestConstants.ErrorStackId, projectId: TestConstants.ProjectId, timeZoneOffset: timeOffset).ToList();
            _errorPipeline.Run(errors);

            var info = _errorStatsHelper.GetProjectErrorStats(TestConstants.ProjectId, timeOffset, startDate, endDate);
            Assert.Equal(count, info.Total);
            Assert.InRange(info.UniqueTotal, 1, count);
            Assert.Equal(0, info.NewTotal);
            Assert.True(info.Stats.Count > 40);
            Assert.Equal(count, info.Stats.Sum(ds => ds.Total));
            Assert.Equal(0, info.Stats.Sum(ds => ds.NewTotal));
        }

        [Fact]
        public void CanAggregateStatsOverSeveralMonthsForMultipleProjects() {
            _resetDataHelper.ResetProjectData(TestConstants.ProjectId);
            TimeSpan timeOffset = _projectRepository.GetDefaultTimeOffset(TestConstants.ProjectId);
            DateTime startDate = DateTime.UtcNow.Add(timeOffset).Date.AddDays(-120);
            DateTime endDate = DateTime.UtcNow.Add(timeOffset).Date;
            const int count1 = 50;
            const int count2 = 50;

            List<Error> errors1 = ErrorData.GenerateErrors(count1, organizationId: TestConstants.OrganizationId, startDate: startDate, endDate: endDate, errorStackId: TestConstants.ErrorStackId, projectId: TestConstants.ProjectId, timeZoneOffset: timeOffset).ToList();
            _errorPipeline.Run(errors1);

            List<Error> errors2 = ErrorData.GenerateErrors(count2, organizationId: TestConstants.OrganizationId, startDate: startDate, endDate: endDate, errorStackId: TestConstants.ErrorStackId2, projectId: TestConstants.ProjectIdWithNoRoles, timeZoneOffset: timeOffset).ToList();
            _errorPipeline.Run(errors2);

            var info1 = _errorStatsHelper.GetProjectErrorStats(TestConstants.ProjectId, timeOffset, startDate, endDate);
            Assert.Equal(count1, info1.Total);
            Assert.InRange(info1.UniqueTotal, 1, count1);
            Assert.Equal(0, info1.NewTotal);
            Assert.True(info1.Stats.Count > 40);
            Assert.Equal(count1, info1.Stats.Sum(ds => ds.Total));
            Assert.Equal(0, info1.Stats.Sum(ds => ds.NewTotal));

            var info2 = _errorStatsHelper.GetProjectErrorStats(TestConstants.ProjectIdWithNoRoles, timeOffset, startDate, endDate);
            Assert.Equal(count2, info2.Total);
            Assert.InRange(info2.UniqueTotal, 1, count2);
            Assert.Equal(0, info2.NewTotal);
            Assert.True(info2.Stats.Count > 40);
            Assert.Equal(count2, info2.Stats.Sum(ds => ds.Total));
            Assert.Equal(0, info2.Stats.Sum(ds => ds.NewTotal));
        }

        [Fact]
        public void CanStackErrors() {
            _resetDataHelper.ResetProjectData(TestConstants.ProjectId);
            TimeSpan timeOffset = _projectRepository.GetDefaultTimeOffset(TestConstants.ProjectId);
            DateTime startDate = DateTime.UtcNow.Add(timeOffset).Date.AddDays(-120);
            DateTime endDate = DateTime.UtcNow.Add(timeOffset).Date;
            const int count = 25;

            List<Error> errors = ErrorData.GenerateErrors(count, organizationId: TestConstants.OrganizationId, startDate: startDate, endDate: endDate, projectId: TestConstants.ProjectId, timeZoneOffset: timeOffset).ToList();
            _errorPipeline.Run(errors);

            long stackCount = _errorStackRepository.Count();

            var info = _errorStatsHelper.GetProjectErrorStats(TestConstants.ProjectId, timeOffset, startDate, endDate);
            Assert.Equal(count, info.Total);
            Assert.InRange(info.UniqueTotal, 1, count);
            Assert.Equal(stackCount, info.NewTotal);
            Assert.True(info.Stats.Count > 40);
            Assert.Equal(count, info.Stats.Sum(ds => ds.Total));
            Assert.Equal(stackCount, info.Stats.Sum(ds => ds.NewTotal));
        }

        [Fact]
        public void CanStackErrorsForMultipleProjects() {
            _resetDataHelper.ResetProjectData(TestConstants.ProjectId);
            TimeSpan timeOffset = _projectRepository.GetDefaultTimeOffset(TestConstants.ProjectId);
            DateTime startDate = DateTime.UtcNow.Add(timeOffset).Date.AddDays(-120);
            DateTime endDate = DateTime.UtcNow.Add(timeOffset).Date;
            const int count = 25;

            List<Error> errors1 = ErrorData.GenerateErrors(count, organizationId: TestConstants.OrganizationId, startDate: startDate, endDate: endDate, projectId: TestConstants.ProjectId, timeZoneOffset: timeOffset).ToList();
            _errorPipeline.Run(errors1);

            List<Error> errors2 = ErrorData.GenerateErrors(count, organizationId: TestConstants.OrganizationId, startDate: startDate, endDate: endDate, projectId: TestConstants.ProjectIdWithNoRoles, timeZoneOffset: timeOffset).ToList();
            _errorPipeline.Run(errors2);

            long stackCount = _errorStackRepository.Where(es => es.ProjectId == TestConstants.ProjectId).Count();

            var info = _errorStatsHelper.GetProjectErrorStats(TestConstants.ProjectId, timeOffset, startDate, endDate);
            Assert.Equal(count, info.Total);
            Assert.InRange(info.UniqueTotal, 1, count);
            Assert.Equal(stackCount, info.NewTotal);
            Assert.True(info.Stats.Count > 40);
            Assert.Equal(count, info.Stats.Sum(ds => ds.Total));
            Assert.Equal(stackCount, info.Stats.Sum(ds => ds.NewTotal));
        }

        [Fact]
        public void CanCalculateTimeBuckets() {
            var bucket = ErrorStatsHelper.GetTimeBucket(new DateTimeOffset(2012, 11, 16, 0, 13, 43, TimeSpan.FromHours(-0)));
            Assert.Equal(0, bucket);

            bucket = ErrorStatsHelper.GetTimeBucket(new DateTimeOffset(2012, 11, 16, 0, 15, 43, TimeSpan.FromHours(-0)));
            Assert.Equal(15, bucket);

            bucket = ErrorStatsHelper.GetTimeBucket(new DateTimeOffset(2012, 11, 16, 23, 59, 59, TimeSpan.FromHours(-0)));
            Assert.Equal(1425, bucket);

            var buckets = new List<int>();
            for (int i = 0; i < 1440; i += 15)
                buckets.Add(i);

            Assert.Equal(96, buckets.Count);
        }

        [Fact]
        public void CanResetStackStats() {
            _resetDataHelper.ResetProjectData(TestConstants.ProjectId);
            TimeSpan timeOffset = _projectRepository.GetDefaultTimeOffset(TestConstants.ProjectId);
            DateTime startDate = DateTime.UtcNow.Add(timeOffset).Date.AddDays(-45);
            DateTime endDate = DateTime.UtcNow.Add(timeOffset).Date;
            const int count = 100;

            List<Error> errors1 = ErrorData.GenerateErrors(count, organizationId: TestConstants.OrganizationId, startDate: startDate, endDate: endDate, projectId: TestConstants.ProjectId, timeZoneOffset: timeOffset).ToList();
            _errorPipeline.Run(errors1);

            long stackCount = _errorStackRepository.Where(es => es.ProjectId == TestConstants.ProjectId).Count();
            var firstStack = _errorStackRepository.Where(es => es.ProjectId == TestConstants.ProjectId).OrderBy(es => es.FirstOccurrence).First();
            Console.WriteLine("Count: " + firstStack.TotalOccurrences);

            var info = _errorStatsHelper.GetProjectErrorStats(TestConstants.ProjectId, timeOffset, startDate, endDate);
            Assert.Equal(count, info.Total);
            Assert.InRange(info.UniqueTotal, 1, count);
            Assert.Equal(stackCount, info.NewTotal);
            Assert.Equal(info.EndDate.Subtract(info.StartDate).TotalDays + 1, info.Stats.Count);
            Assert.Equal(count, info.Stats.Sum(ds => ds.Total));
            Assert.Equal(stackCount, info.Stats.Sum(ds => ds.NewTotal));

            _errorStatsHelper.DecrementDayProjectStatsByStackId(TestConstants.ProjectId, firstStack.Id);
            _errorStatsHelper.DecrementMonthProjectStatsByStackId(TestConstants.ProjectId, firstStack.Id);

            info = _errorStatsHelper.GetProjectErrorStats(TestConstants.ProjectId, timeOffset, startDate, endDate);
            Assert.Equal(count - firstStack.TotalOccurrences, info.Total);
            Assert.InRange(info.UniqueTotal - 1, 1, count);
            Assert.Equal(stackCount - 1, info.NewTotal);
            Assert.Equal(info.EndDate.Subtract(info.StartDate).TotalDays + 1, info.Stats.Count);
            Assert.Equal(count - firstStack.TotalOccurrences, info.Stats.Sum(ds => ds.Total));
            Assert.Equal(stackCount - 1, info.Stats.Sum(ds => ds.NewTotal));
        }

        [Fact]
        public void CanHideStacksFromStats() {
            _resetDataHelper.ResetProjectData(TestConstants.ProjectId);
            TimeSpan timeOffset = _projectRepository.GetDefaultTimeOffset(TestConstants.ProjectId);
            DateTime startDate = DateTime.UtcNow.Add(timeOffset).Date.AddDays(-120);
            DateTime endDate = DateTime.UtcNow.Add(timeOffset).Date;
            const int count = 50;

            List<Error> errors = ErrorData.GenerateErrors(count, organizationId: TestConstants.OrganizationId, startDate: startDate, endDate: endDate, projectId: TestConstants.ProjectId, timeZoneOffset: timeOffset).ToList();
            _errorPipeline.Run(errors);

            var firstStack = _errorStackRepository.Where(es => es.ProjectId == TestConstants.ProjectId).OrderBy(es => es.FirstOccurrence).First();
            firstStack.IsHidden = true;
            _errorStackRepository.Update(firstStack);

            var biggestStack = _errorStackRepository.Where(es => es.ProjectId == TestConstants.ProjectId && !es.IsHidden).OrderByDescending(es => es.TotalOccurrences).First();
            biggestStack.IsHidden = true;
            _errorStackRepository.Update(biggestStack);
            _errorStackRepository.InvalidateHiddenIdsCache(TestConstants.ProjectId);

            long stackCount = _errorStackRepository.Where(s => !s.IsHidden).Count();
            int countWithoutHidden = count - firstStack.TotalOccurrences - biggestStack.TotalOccurrences;

            var info = _errorStatsHelper.GetProjectErrorStats(TestConstants.ProjectId, timeOffset, startDate, endDate);
            Assert.Equal(countWithoutHidden, info.Total);
            Assert.InRange(info.UniqueTotal, 1, count);
            Assert.Equal(stackCount, info.NewTotal);
            Assert.True(info.Stats.Count > 40);
            Assert.Equal(countWithoutHidden, info.Stats.Sum(ds => ds.Total));
            Assert.Equal(stackCount, info.Stats.Sum(ds => ds.NewTotal));
        }

        protected override void CreateData() {
            var membershipProvider = new MembershipProvider(_userRepository.Collection);
            foreach (User user in UserData.GenerateSampleUsers())
                membershipProvider.CreateAccount(user);
            _projectRepository.Add(ProjectData.GenerateSampleProjects());
            _organizationRepository.Add(OrganizationData.GenerateSampleOrganizations());

            //_errorStackRepository.Add(ErrorStackData.GenerateErrorStack(id: TestConstants.ErrorStackId, organizationId: TestConstants.OrganizationId, projectId: TestConstants.ProjectId));
            //_errorStackRepository.Add(ErrorStackData.GenerateErrorStack(id: TestConstants.ErrorStackId2, organizationId: TestConstants.OrganizationId, projectId: TestConstants.ProjectIdWithNoRoles));
        }

        protected override void RemoveData() {
            base.RemoveData();
            _errorStackRepository.DeleteAll();
            _projectRepository.DeleteAll();
            _organizationRepository.DeleteAll();
            _userRepository.DeleteAll();

            _dayStackStats.DeleteAll();
            _monthStackStats.DeleteAll();
            _dayProjectStats.DeleteAll();
            _monthProjectStats.DeleteAll();
        }
    }
}