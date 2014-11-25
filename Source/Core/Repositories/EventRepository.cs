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
        public EventRepository(IElasticClient elasticClient, IValidator<PersistentEvent> validator = null, ICacheClient cacheClient = null, IMessagePublisher messagePublisher = null)
            : base(elasticClient, validator, cacheClient, messagePublisher) {
        }

        protected override void BeforeAdd(ICollection<PersistentEvent> documents) {
            // TODO: Remove this dependency on the mongo lib.
            foreach (var ev in documents.Where(ev => ev.Id == null))
                ev.Id = MongoDB.Bson.ObjectId.GenerateNewId().ToString();

            base.BeforeAdd(documents);
        }

        public void UpdateFixedByStack(string organizationId, string stackId, bool value) {
            if (String.IsNullOrEmpty(stackId))
                throw new ArgumentNullException("stackId");

            UpdateAll(organizationId, new QueryOptions().WithStackId(stackId), new { is_fixed = value });
        }

        public void UpdateHiddenByStack(string organizationId, string stackId, bool value) {
            if (String.IsNullOrEmpty(stackId))
                throw new ArgumentNullException("stackId");

            UpdateAll(organizationId, new QueryOptions().WithStackId(stackId), new { is_hidden = value });
        }

        public void RemoveAllByDate(string organizationId, DateTime utcCutoffDate) {
            var filter = Filter<PersistentEvent>.Range(r => r.OnField(e => e.Date).Lower(utcCutoffDate));
            RemoveAll(new ElasticSearchOptions<PersistentEvent>().WithOrganizationId(organizationId).WithFilter(filter));
        }

        public void HideAllByClientIpAndDate(string organizationId, string clientIp, DateTime utcStartDate, DateTime utcEndDate) {
            var filter = Filter<PersistentEvent>.Term("client_ip_address", clientIp)
                && Filter<PersistentEvent>.Range(r => r.OnField(e => e.Date).GreaterOrEquals(utcStartDate))
                && Filter<PersistentEvent>.Range(r => r.OnField(e => e.Date).LowerOrEquals(utcEndDate));

            UpdateAll(organizationId, new ElasticSearchOptions<PersistentEvent>().WithFilter(filter), new { is_hidden = true });
        }

        public async Task HideAllByClientIpAndDateAsync(string organizationId, string clientIp, DateTime utcStartDate, DateTime utcEndDate) {
            await Task.Run(() => HideAllByClientIpAndDate(organizationId, clientIp, utcStartDate, utcEndDate));
        }

        public ICollection<PersistentEvent> GetByFilter(string systemFilter, string userFilter, string sort, SortOrder sortOrder, string field, DateTime utcStart, DateTime utcEnd, PagingOptions paging) {
            if (String.IsNullOrEmpty(sort)) {
                sort = "date";
                sortOrder = SortOrder.Descending;
            }

            var search = new ElasticSearchOptions<PersistentEvent>()
                .WithDateRange(utcStart, utcEnd, field ?? "date")
                .WithIndicesFromDateRange()
                .WithFilter(!String.IsNullOrEmpty(systemFilter) ? Filter<PersistentEvent>.Query(q => q.QueryString(qs => qs.DefaultOperator(Operator.And).Query(systemFilter))) : null)
                .WithQuery(userFilter)
                .WithPaging(paging)
                .WithSort(e => e.OnField(sort).Order(sortOrder == SortOrder.Descending ? Nest.SortOrder.Descending : Nest.SortOrder.Ascending));

            return Find(search);
        }

        public ICollection<PersistentEvent> GetMostRecent(string projectId, DateTime utcStart, DateTime utcEnd, PagingOptions paging, bool includeHidden = false, bool includeFixed = false, bool includeNotFound = true) {
            var filter = new FilterContainer();

            if (!includeHidden)
                filter &= !Filter<PersistentEvent>.Term(e => e.IsHidden, true);

            if (!includeFixed)
                filter &= !Filter<PersistentEvent>.Term(e => e.IsFixed, true);

            if (!includeNotFound)
                filter &= !Filter<PersistentEvent>.Term(e => e.Type, "404");

            if (utcStart != DateTime.MinValue)
                filter &= Filter<PersistentEvent>.Range(r => r.OnField(e => e.Date).GreaterOrEquals(utcStart));
            if (utcEnd != DateTime.MaxValue)
                filter &= Filter<PersistentEvent>.Range(r => r.OnField(e => e.Date).LowerOrEquals(utcEnd));

            return Find(new ElasticSearchOptions<PersistentEvent>().WithProjectId(projectId).WithFilter(filter).WithIndices(utcStart, utcEnd).WithPaging(paging).WithSort(s => s.OnField(e => e.Date).Descending()));
        }

        public ICollection<PersistentEvent> GetByStackIdOccurrenceDate(string stackId, DateTime utcStart, DateTime utcEnd, PagingOptions paging) {
            var filter = new FilterContainer();

            if (utcStart != DateTime.MinValue)
                filter &= Filter<PersistentEvent>.Range(r => r.OnField(e => e.Date).GreaterOrEquals(utcStart));
            if (utcEnd != DateTime.MaxValue)
                filter &= Filter<PersistentEvent>.Range(r => r.OnField(e => e.Date).LowerOrEquals(utcEnd));

            return Find(new ElasticSearchOptions<PersistentEvent>().WithStackId(stackId).WithFilter(filter).WithIndices(utcStart, utcEnd).WithPaging(paging).WithSort(s => s.OnField(e => e.Date).Descending()));
        }

        public ICollection<PersistentEvent> GetByReferenceId(string projectId, string referenceId) {
            var filter = Filter<PersistentEvent>.Term(e => e.ReferenceId, referenceId);
            return Find(new ElasticSearchOptions<PersistentEvent>()
                .WithProjectId(projectId)
                .WithFilter(filter)
                .WithSort(s => s.OnField(e => e.Date).Descending())
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
            return Find(new ElasticSearchOptions<PersistentEvent>()
                .WithStackId(stackId)
                .WithFields("id", "organization_id", "project_id", "stack_id")
                .WithSort(s => s.OnField(e => e.Date).Descending())
                .WithPaging(options));
        }

        public string GetPreviousEventId(string id, string systemFilter = null, string userFilter = null, DateTime? utcStart = null, DateTime? utcEnd = null) {
            PersistentEvent data = GetById(id, true);
            if (data == null)
                return null;

            if (!utcStart.HasValue)
                utcStart = DateTime.MinValue;

            if (!utcEnd.HasValue)
                utcEnd = DateTime.MaxValue;

            if (String.IsNullOrEmpty(userFilter))
                userFilter = "stack:" + data.StackId;

            var filter = !Filter<PersistentEvent>.Ids(new[] { id })
                && Filter<PersistentEvent>.Range(r => r.OnField(e => e.Date).LowerOrEquals(data.Date.ToUniversalTime().DateTime))
                && Filter<PersistentEvent>.Query(q => q.QueryString(qs => qs.DefaultOperator(Operator.And).Query(systemFilter)));

            var documents = Find(new ElasticSearchOptions<PersistentEvent>()
                .WithDateRange(utcStart, utcEnd, "date")
                .WithIndicesFromDateRange()
                .WithSort(s => s.OnField(e => e.Date).Descending())
                .WithLimit(10)
                .WithFields("id", "date")
                .WithFilter(filter)
                .WithQuery(userFilter));

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

        public string GetNextEventId(string id, string systemFilter = null, string userFilter = null, DateTime? utcStart = null, DateTime? utcEnd = null) {
            PersistentEvent data = GetById(id, true);
            if (data == null)
                return null;

            if (!utcStart.HasValue)
                utcStart = DateTime.MinValue;

            if (!utcEnd.HasValue)
                utcEnd = DateTime.MaxValue;

            if (String.IsNullOrEmpty(userFilter))
                userFilter = "stack:" + data.StackId;

            var filter = !Filter<PersistentEvent>.Ids(new[] { id })
                && Filter<PersistentEvent>.Range(r => r.OnField(e => e.Date).GreaterOrEquals(data.Date.ToUniversalTime().DateTime))
                && Filter<PersistentEvent>.Query(q => q.QueryString(qs => qs.DefaultOperator(Operator.And).Query(systemFilter)));

            var documents = Find(new ElasticSearchOptions<PersistentEvent>()
                .WithDateRange(utcStart, utcEnd, "date")
                .WithIndicesFromDateRange()
                .WithSort(s => s.OnField(e => e.Date).Ascending())
                .WithLimit(10)
                .WithFields("id", "date")
                .WithFilter(filter)
                .WithQuery(userFilter));

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

        public void MarkAsRegressedByStack(string organizationId, string stackId) {
            UpdateAll(organizationId, new QueryOptions().WithStackId(stackId), new { is_fixed = false});
        }

        public override ICollection<PersistentEvent> GetByOrganizationId(string organizationId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            return GetByOrganizationIds(new[] { organizationId }, paging, useCache, expiresIn);
        }

        public override ICollection<PersistentEvent> GetByOrganizationIds(ICollection<string> organizationIds, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            return base.GetByOrganizationIds(organizationIds, GetPagingWithSortingOptions(paging), useCache, expiresIn);
        }

        public ICollection<PersistentEvent> GetByOrganizationIds(ICollection<string> organizationIds, string query = null, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            if (organizationIds == null || organizationIds.Count == 0)
                return new List<PersistentEvent>();

            string cacheKey = String.Concat("org:", String.Join("", organizationIds).GetHashCode().ToString());
            var results = Find(new ElasticSearchOptions<PersistentEvent>()
                .WithOrganizationIds(organizationIds)
                .WithQuery(query)
                .WithPaging(GetPagingWithSortingOptions(paging))
                .WithCacheKey(useCache ? cacheKey : null)
                .WithExpiresIn(expiresIn));

            return results;
        }

        public override ICollection<PersistentEvent> GetByStackId(string stackId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            return base.GetByStackId(stackId, GetPagingWithSortingOptions(paging), useCache, expiresIn);
        }

        public override ICollection<PersistentEvent> GetByProjectId(string projectId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            return base.GetByProjectId(projectId, GetPagingWithSortingOptions(paging), useCache, expiresIn);
        }

        private ElasticSearchPagingOptions<PersistentEvent> GetPagingWithSortingOptions(PagingOptions paging) {
            var pagingOptions = new ElasticSearchPagingOptions<PersistentEvent>(paging);
            pagingOptions.SortBy.Add(s => s.OnField(f => f.Date).Descending());
            pagingOptions.SortBy.Add(s => s.OnField("_uid").Descending());

            return pagingOptions;
        }

        protected override void AfterAdd(ICollection<PersistentEvent> documents, bool addToCache = false, TimeSpan? expiresIn = null) {
            bool enableNotifications = EnableNotifications;
            EnableNotifications = false;
            base.AfterAdd(documents, addToCache, expiresIn);
            EnableNotifications = enableNotifications;
        }
    }
}