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
using Exceptionless.Core.Caching;
using Exceptionless.Core.Messaging;
using Exceptionless.Models;
using FluentValidation;
using Nest;

namespace Exceptionless.Core.Repositories {
    public class EventRepository : ElasticSearchRepositoryOwnedByOrganizationAndProjectAndStack<PersistentEvent>, IEventRepository {
        private readonly IProjectRepository _projectRepository;
        private readonly IOrganizationRepository _organizationRepository;

        public EventRepository(ElasticClient elasticClient, IProjectRepository projectRepository, IOrganizationRepository organizationRepository, IValidator<PersistentEvent> validator = null, ICacheClient cacheClient = null, IMessagePublisher messagePublisher = null)
            : base(elasticClient, validator, cacheClient, messagePublisher) {
            _projectRepository = projectRepository;
            _organizationRepository = organizationRepository;

            EnableNotifications = false;
        }

        protected override void BeforeAdd(ICollection<PersistentEvent> documents) {
            // TODO: Remove this dependency on the mongo lib.
            foreach (var ev in documents.Where(ev => ev.Id == null))
                ev.Id = MongoDB.Bson.ObjectId.GenerateNewId().ToString();

            base.BeforeAdd(documents);
        }

        public void UpdateFixedByStackId(string stackId, bool value) {
            if (String.IsNullOrEmpty(stackId))
                throw new ArgumentNullException("stackId");

            UpdateAll(new QueryOptions().WithStackId(stackId), String.Concat("ctx._source.is_fixed = ", value ? "true" : "false"));
        }

        public void UpdateHiddenByStackId(string stackId, bool value) {
            if (String.IsNullOrEmpty(stackId))
                throw new ArgumentNullException("stackId");

            UpdateAll(new QueryOptions().WithStackId(stackId), String.Concat("ctx._source.is_hidden = ", value ? "true" : "false"));
        }

        public void RemoveAllByDate(string organizationId, DateTime utcCutoffDate) {
            var query = Query<PersistentEvent>.Range(r => r.OnField("date").Lower(utcCutoffDate));
            RemoveAll(new ElasticSearchOptions<PersistentEvent>().WithOrganizationId(organizationId).WithQuery(query));
        }

        public void RemoveAllByClientIpAndDate(string clientIp, DateTime utcStartDate, DateTime utcEndDate) {
            // TODO Verify this query.
            var query = Query<PersistentEvent>.Bool(b => 
                b.Must(m => m.Term("data.request.clientipaddress", clientIp) 
                && m.Range(r => r.OnField("date").GreaterOrEquals(utcStartDate))
                && m.Range(r => r.OnField("date").LowerOrEquals(utcEndDate))));

            RemoveAll(new ElasticSearchOptions<PersistentEvent>().WithQuery(query));
        }

        public async Task RemoveAllByClientIpAndDateAsync(string clientIp, DateTime utcStartDate, DateTime utcEndDate) {
            await Task.Run(() => RemoveAllByClientIpAndDate(clientIp, utcStartDate, utcEndDate));
        }

        private void IncrementOrganizationAndProjectEventCounts(string organizationId, string projectId, long count) {
            _organizationRepository.IncrementStats(organizationId, eventCount: -count);
            _projectRepository.IncrementStats(projectId, eventCount: -count);
        }

        public ICollection<PersistentEvent> GetMostRecent(string projectId, DateTime utcStart, DateTime utcEnd, PagingOptions paging, bool includeHidden = false, bool includeFixed = false, bool includeNotFound = true) {
            var query = new QueryContainer();

            if (!includeHidden)
                query &= Query<PersistentEvent>.Bool(b => b.MustNot(m => m.Term("is_hidden", true)));

            if (!includeFixed)
                query &= Query<PersistentEvent>.Bool(b => b.MustNot(m => m.Term("is_fixed", true)));

            if (!includeNotFound)
                query &= Query<PersistentEvent>.Bool(b => b.MustNot(m => m.Term("type", "404")));

            if (utcStart != DateTime.MinValue)
                query &= Query<PersistentEvent>.Range(r => r.OnField("date").GreaterOrEquals(utcStart));
            if (utcEnd != DateTime.MaxValue)
                query &= Query<PersistentEvent>.Range(r => r.OnField("date").LowerOrEquals(utcEnd));

            return Find<PersistentEvent>(new ElasticSearchOptions<PersistentEvent>().WithProjectId(projectId).WithQuery(query).WithPaging(paging).WithSort(s => s.OnField("date").Descending()));
        }

        public ICollection<PersistentEvent> GetByStackIdOccurrenceDate(string stackId, DateTime utcStart, DateTime utcEnd, PagingOptions paging) {
            var query = new QueryContainer();

            if (utcStart != DateTime.MinValue)
                query &= Query<PersistentEvent>.Range(r => r.OnField("date").GreaterOrEquals(utcStart));
            if (utcEnd != DateTime.MaxValue)
                query &= Query<PersistentEvent>.Range(r => r.OnField("date").LowerOrEquals(utcEnd));

            return Find<PersistentEvent>(new ElasticSearchOptions<PersistentEvent>().WithStackId(stackId).WithQuery(query).WithPaging(paging).WithSort(s => s.OnField("date").Descending()));
        }

        public ICollection<PersistentEvent> GetByReferenceId(string projectId, string referenceId) {
            var query = Query<PersistentEvent>.Bool(b => b.Must(m => m.Term("reference_id", referenceId)));
            return Find<PersistentEvent>(new ElasticSearchOptions<PersistentEvent>()
                .WithProjectId(projectId)
                .WithQuery(query)
                .WithSort(s => s.OnField("date").Descending())
                .WithLimit(10));
        }

        public void RemoveOldestEvents(string stackId, int maxEventsPerStack) {
            var options = new PagingOptions { Limit = maxEventsPerStack, Page = 2 };
            var events = GetOldestEvents(stackId, options);
            while (events.Count > 0) {
                Remove(events);

                if (!options.HasMore)
                    break;

                events = GetOldestEvents(stackId, options);
            }
        }

        private ICollection<PersistentEvent> GetOldestEvents(string stackId, PagingOptions options) {
            return Find<PersistentEvent>(new ElasticSearchOptions<PersistentEvent>()
                .WithStackId(stackId)
                .WithFields(FieldNames.Id, FieldNames.OrganizationId, FieldNames.ProjectId, FieldNames.StackId)
                .WithSort(s => s.OnField("date").Descending())
                .WithPaging(options));
        }

        public string GetPreviousEventIdInStack(string id) {
            PersistentEvent data = GetById(id, true);
            if (data == null)
                return null;

            var query = Query<PersistentEvent>.Bool(b => b.Must(m => !m.Term("id", id) && m.Range(r => r.OnField("date").LowerOrEquals(data.Date.DateTime))));
            var documents = Find<PersistentEvent>(new ElasticSearchOptions<PersistentEvent>()
                .WithStackId(data.StackId)
                .WithSort(s => s.OnField("date").Descending())
                .WithLimit(10)
                .WithFields("id", "date")// FieldNames.Id, FieldNames.Date)
                .WithQuery(query));

            if (documents.Count == 0)
                return null;

            // make sure we don't have records with the exact same occurrence date
            if (documents.All(t => t.Date != data.Date))
                return documents.OrderByDescending(t => t.Date).ThenByDescending(t => t.Id).First().Id;

            // we have records with the exact same occurrence date, we need to figure out the order of those
            // put our target error into the mix, sort it and return the result before the target
            var unionResults = documents.Union(new[] { data })
                .OrderBy(t => t.Date.UtcTicks).ThenBy(t => t.Id)
                .ToList();

            var index = unionResults.FindIndex(t => t.Id == data.Id);
            return index == 0 ? null : unionResults[index - 1].Id;
        }

        public string GetNextEventIdInStack(string id) {
            PersistentEvent data = GetById(id, true);
            if (data == null)
                return null;

            var query = Query<PersistentEvent>.Bool(b => b.Must(m => !m.Term("id", id) && m.Range(r => r.OnField("date").GreaterOrEquals(data.Date.DateTime))));
            var documents = Find<PersistentEvent>(new ElasticSearchOptions<PersistentEvent>()
                .WithStackId(data.StackId)
                .WithSort(s => s.OnField("date").Descending())
                .WithLimit(10)
                .WithFields("id", "date")// FieldNames.Id, FieldNames.Date)
                .WithQuery(query));

            if (documents.Count == 0)
                return null;

            // make sure we don't have records with the exact same occurrence date
            if (documents.All(t => t.Date != data.Date))
                return documents.OrderBy(t => t.Date).ThenBy(t => t.Id).First().Id;

            // we have records with the exact same occurrence date, we need to figure out the order of those
            // put our target error into the mix, sort it and return the result after the target
            var unionResults = documents.Union(new[] { data })
                .OrderBy(t => t.Date.Ticks).ThenBy(t => t.Id)
                .ToList();

            var index = unionResults.FindIndex(t => t.Id == data.Id);
            return index == unionResults.Count - 1 ? null : unionResults[index + 1].Id;
        }

        public void MarkAsRegressedByStack(string id) {
            UpdateAll(new QueryOptions().WithStackId(id), "ctx._source.is_fixed = false");
        }

        public override ICollection<PersistentEvent> GetByOrganizationId(string organizationId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            bool sortByAscending = paging != null && !String.IsNullOrEmpty(paging.After);
            var results = base.GetByOrganizationId(organizationId, GetPagingWithSortingOptions(paging, sortByAscending), useCache, expiresIn);
            return !sortByAscending ? results : results.OrderByDescending(e => e.Date).ThenByDescending(se => se.Id).ToList();
        }

        public override ICollection<PersistentEvent> GetByOrganizationIds(ICollection<string> organizationIds, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            bool sortByAscending = paging != null && !String.IsNullOrEmpty(paging.After);
            var results = base.GetByOrganizationIds(organizationIds, GetPagingWithSortingOptions(paging, sortByAscending), useCache, expiresIn);
            return !sortByAscending ? results : results.OrderByDescending(e => e.Date).ThenByDescending(se => se.Id).ToList();
        }

        public override ICollection<PersistentEvent> GetByStackId(string stackId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            bool sortByAscending = paging != null && !String.IsNullOrEmpty(paging.After);
            var results = base.GetByStackId(stackId, GetPagingWithSortingOptions(paging, sortByAscending), useCache, expiresIn);
            return !sortByAscending ? results : results.OrderByDescending(e => e.Date).ThenByDescending(se => se.Id).ToList();
        }

        public override ICollection<PersistentEvent> GetByProjectId(string projectId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            bool sortByAscending = paging != null && !String.IsNullOrEmpty(paging.After);
            var results = base.GetByProjectId(projectId, GetPagingWithSortingOptions(paging, sortByAscending), useCache, expiresIn);
            return !sortByAscending ? results : results.OrderByDescending(e => e.Date).ThenByDescending(se => se.Id).ToList();
        }

        private ElasticSearchPagingOptions<PersistentEvent> GetPagingWithSortingOptions(PagingOptions paging, bool sortByAscending) {
            var pagingOptions = new ElasticSearchPagingOptions<PersistentEvent>(paging) {
                SortBy = sortByAscending 
                ? new Func<SortFieldDescriptor<PersistentEvent>, IFieldSort>(s => s.Ascending().OnField(f => f.Date).OnField(f => f.Id))
                : new Func<SortFieldDescriptor<PersistentEvent>, IFieldSort>(s => s.Descending().OnField(f => f.Date).OnField(f => f.Id))
            };

            if (!String.IsNullOrEmpty(pagingOptions.Before) && pagingOptions.Before.IndexOf('-') > 0) {
                var parts = pagingOptions.Before.Split(new[] { '-' }, StringSplitOptions.RemoveEmptyEntries);

                long beforeUtcTicks;
                if (parts.Length == 2 && Int64.TryParse(parts[0], out beforeUtcTicks) && !String.IsNullOrEmpty(parts[1]))
                    pagingOptions.BeforeQuery = Query<PersistentEvent>.Bool(b => b.Must(m => (m.Term("date", beforeUtcTicks) && m.Range(r => r.OnField("id").Lower(parts[1]))) || m.Range(r => r.OnField("date").Lower(beforeUtcTicks))));
            }

            if (!String.IsNullOrEmpty(pagingOptions.After) && pagingOptions.After.IndexOf('-') > 0) {
                var parts = pagingOptions.After.Split(new[] { '-' }, StringSplitOptions.RemoveEmptyEntries);

                long afterUtcTicks;
                if (parts.Length == 2 && Int64.TryParse(parts[0], out afterUtcTicks) && !String.IsNullOrEmpty(parts[1]))
                    pagingOptions.AfterQuery = Query<PersistentEvent>.Bool(b => b.Must(m => (m.Term("date", afterUtcTicks) && m.Range(r => r.OnField("id").Greater(parts[1]))) || m.Range(r => r.OnField("date").Greater(afterUtcTicks))));
            }

            return pagingOptions;
        }

        protected override void AfterRemove(ICollection<PersistentEvent> documents, bool sendNotification = true) {
            base.AfterRemove(documents, sendNotification);

            var groups = documents.GroupBy(e => new {
                    e.OrganizationId,
                    e.ProjectId
                }).ToList();

            foreach (var grouping in groups) {
                if (!grouping.Any())
                    continue;

                IncrementOrganizationAndProjectEventCounts(grouping.Key.OrganizationId, grouping.Key.ProjectId, grouping.Count());
                // TODO: Should be updating stack
            }

            // TODO: Need to decrement stats time bucket by the number of errors we removed. Add flag to delete to tell it to decrement stats docs.

            //var groups = errors.GroupBy(e => new {
            //    e.OrganizationId,
            //    e.ProjectId,
            //    e.ErrorStackId
            //}).ToList();
            //foreach (var grouping in groups) {
            //    if (_statsHelper == null)
            //        continue;

            //    _statsHelper.DecrementDayProjectStatsForTimeBucket(grouping.Key.ErrorStackId, grouping.Count());
            //}
        }

        protected override void AfterAdd(ICollection<PersistentEvent> documents, bool addToCache = false, TimeSpan? expiresIn = null) {
            bool enableNotifications = EnableNotifications;
            EnableNotifications = false;
            base.AfterAdd(documents, addToCache, expiresIn);
            EnableNotifications = enableNotifications;
        }

        private static class FieldNames {
            public const string Id = CommonFieldNames.Id;
            public const string OrganizationId = CommonFieldNames.OrganizationId;
            public const string ProjectId = CommonFieldNames.ProjectId;
            public const string StackId = CommonFieldNames.StackId;
            public const string Type = "typ";
            public const string Source = "src";
            public const string Date = CommonFieldNames.Date;
            public const string Date_UTC = CommonFieldNames.Date_UTC;
            public const string Tags = "tag";
            public const string Message = "msg";
            public const string Data = CommonFieldNames.Data;
            public const string ReferenceId = "ref";
            public const string SessionId = "xid";
            public const string SummaryHtml = "html";
            public const string IsFixed = "fix";
            public const string IsHidden = "hid";
            public const string RequestInfo = "req";
            public const string RequestInfo_ClientIpAddress = RequestInfo + ".ip";
        }
    }
}