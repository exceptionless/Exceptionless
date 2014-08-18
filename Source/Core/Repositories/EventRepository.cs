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
            var filter = Filter<PersistentEvent>.Range(r => r.OnField(e => e.Date).Lower(utcCutoffDate));
            RemoveAll(new ElasticSearchOptions<PersistentEvent>().WithOrganizationId(organizationId).WithFilter(filter));
        }

        public void RemoveAllByClientIpAndDate(string clientIp, DateTime utcStartDate, DateTime utcEndDate) {
            var filter = Filter<PersistentEvent>.Term("data.request.client_ip_address", clientIp)
                && Filter<PersistentEvent>.Range(r => r.OnField(e => e.Date).GreaterOrEquals(utcStartDate))
                && Filter<PersistentEvent>.Range(r => r.OnField(e => e.Date).LowerOrEquals(utcEndDate));

            RemoveAll(new ElasticSearchOptions<PersistentEvent>().WithFilter(filter));
        }

        public async Task RemoveAllByClientIpAndDateAsync(string clientIp, DateTime utcStartDate, DateTime utcEndDate) {
            await Task.Run(() => RemoveAllByClientIpAndDate(clientIp, utcStartDate, utcEndDate));
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

            return Find(new ElasticSearchOptions<PersistentEvent>().WithProjectId(projectId).WithFilter(filter).WithIndices(GetTargetIndex(utcStart, utcEnd)).WithPaging(paging).WithSort(s => s.OnField(e => e.Date).Descending()));
        }

        private List<string> GetTargetIndex(DateTime utcStart, DateTime utcEnd) {
            DateTime current = new DateTime(utcStart.Year, utcStart.Month, 1);
            var indices = new List<string>();
            while (current <= utcEnd) {
                indices.Add("events_v1_" + current.ToString("yyyyMM"));
                current = current.AddMonths(1);
            }

            return indices;
        }

        public ICollection<PersistentEvent> GetByStackIdOccurrenceDate(string stackId, DateTime utcStart, DateTime utcEnd, PagingOptions paging) {
            var filter = new FilterContainer();

            if (utcStart != DateTime.MinValue)
                filter &= Filter<PersistentEvent>.Range(r => r.OnField(e => e.Date).GreaterOrEquals(utcStart));
            if (utcEnd != DateTime.MaxValue)
                filter &= Filter<PersistentEvent>.Range(r => r.OnField(e => e.Date).LowerOrEquals(utcEnd));

            return Find(new ElasticSearchOptions<PersistentEvent>().WithStackId(stackId).WithFilter(filter).WithIndices(GetTargetIndex(utcStart, utcEnd)).WithPaging(paging).WithSort(s => s.OnField(e => e.Date).Descending()));
        }

        public ICollection<PersistentEvent> GetByReferenceId(string projectId, string referenceId) {
            var filter = Filter<PersistentEvent>.Bool(b => b.Must(m => m.Term(e => e.ReferenceId, referenceId)));
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

        public string GetPreviousEventIdInStack(string id) {
            PersistentEvent data = GetById(id, true);
            if (data == null)
                return null;

            var filter = !Filter<PersistentEvent>.Ids(new[] { id })
                && Filter<PersistentEvent>.Range(r => r.OnField(e => e.Date).LowerOrEquals(data.Date.ToUniversalTime().DateTime));
            var documents = Find(new ElasticSearchOptions<PersistentEvent>()
                .WithStackId(data.StackId)
                .WithSort(s => s.OnField(e => e.Date).Descending())
                .WithLimit(10)
                .WithFields("id", "date")// FieldNames.Id, FieldNames.Date)
                .WithFilter(filter));

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

            var filter = !Filter<PersistentEvent>.Ids(new[] { id })
                && Filter<PersistentEvent>.Range(r => r.OnField(e => e.Date).GreaterOrEquals(data.Date.ToUniversalTime().DateTime));
            var documents = Find(new ElasticSearchOptions<PersistentEvent>()
                .WithStackId(data.StackId)
                .WithSort(s => s.OnField(e => e.Date).Ascending())
                .WithLimit(10)
                .WithFields("id", "date")// FieldNames.Id, FieldNames.Date)
                .WithFilter(filter));

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
            var pagingOptions = new ElasticSearchPagingOptions<PersistentEvent>(paging);
            if (sortByAscending) {
                pagingOptions.SortBy.Add(s => s.OnField(f => f.Date).Ascending());
                pagingOptions.SortBy.Add(s => s.OnField(f => f.Id).Ascending());
            } else {
                pagingOptions.SortBy.Add(s => s.OnField(f => f.Date).Descending());
                pagingOptions.SortBy.Add(s => s.OnField(f => f.Id).Descending());
            }

            if (!String.IsNullOrEmpty(pagingOptions.Before) && pagingOptions.Before.IndexOf('-') > 0) {
                var parts = pagingOptions.Before.Split(new[] { '-' }, StringSplitOptions.RemoveEmptyEntries);

                long beforeUtcTicks;
                if (parts.Length == 2 && Int64.TryParse(parts[0], out beforeUtcTicks) && !String.IsNullOrEmpty(parts[1]))
                    pagingOptions.BeforeFilter = (
                            Filter<PersistentEvent>.Term(e => e.Date, new DateTime(beforeUtcTicks, DateTimeKind.Utc))
                            && Filter<PersistentEvent>.Range(r => r.OnField(e => e.Id).Lower(parts[1]))
                        ) || Filter<PersistentEvent>.Range(r => r.OnField(e => e.Date).Lower(new DateTime(beforeUtcTicks, DateTimeKind.Utc)));
            }
            
            if (!String.IsNullOrEmpty(pagingOptions.After) && pagingOptions.After.IndexOf('-') > 0) {
                var parts = pagingOptions.After.Split(new[] { '-' }, StringSplitOptions.RemoveEmptyEntries);

                long afterUtcTicks;
                if (parts.Length == 2 && Int64.TryParse(parts[0], out afterUtcTicks) && !String.IsNullOrEmpty(parts[1]))
                    pagingOptions.AfterFilter = (
                            Filter<PersistentEvent>.Term(e => e.Date, new DateTime(afterUtcTicks, DateTimeKind.Utc))
                            && Filter<PersistentEvent>.Range(r => r.OnField(e => e.Id).Greater(parts[1]))
                        ) || Filter<PersistentEvent>.Range(r => r.OnField(e => e.Date).Greater(new DateTime(afterUtcTicks, DateTimeKind.Utc)));
            }

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