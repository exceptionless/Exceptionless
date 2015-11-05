using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Results;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Repositories.Queries;
using Foundatio.Elasticsearch.Repositories;
using Foundatio.Elasticsearch.Repositories.Queries;
using Foundatio.Repositories.Models;
using Foundatio.Repositories.Queries;
using Foundatio.Utility;
using Nest;
using SortOrder = Foundatio.Repositories.Models.SortOrder;

namespace Exceptionless.Core.Repositories {
    public class EventRepository : RepositoryOwnedByOrganizationAndProjectAndStack<PersistentEvent>, IEventRepository {
        private static readonly DateTime MIN_OBJECTID_DATE = new DateTime(2000, 1, 1);

        public EventRepository(RepositoryContext<PersistentEvent> context, EventIndex index) : base(context, index) {
            DisableCache();
            BatchNotifications = true;
        }

        protected override string[] DefaultExcludes => new[] { "idx" };

        protected override Func<PersistentEvent, string> GetDocumentIndexFunc {
            get { return document => GetIndexById(document.Id); }
        }

        protected override string GetIndexById(string id) {
            ObjectId objectId;
            if (ObjectId.TryParse(id, out objectId) && objectId.CreationTime > MIN_OBJECTID_DATE)
                return String.Concat(_index.VersionedName, "-", objectId.CreationTime.ToString("yyyyMM"));

            return null;
        }

        public Task UpdateFixedByStackAsync(string organizationId, string stackId, bool value) {
            if (String.IsNullOrEmpty(stackId))
                throw new ArgumentNullException(nameof(stackId));

            return UpdateAllAsync(organizationId, NewQuery().WithStackId(stackId), new { is_fixed = value });
        }

        public Task UpdateHiddenByStackAsync(string organizationId, string stackId, bool value) {
            if (String.IsNullOrEmpty(stackId))
                throw new ArgumentNullException(nameof(stackId));

            return UpdateAllAsync(organizationId, NewQuery().WithStackId(stackId), new { is_hidden = value });
        }

        public Task RemoveAllByDateAsync(string organizationId, DateTime utcCutoffDate) {
            var filter = Filter<PersistentEvent>.Range(r => r.OnField(e => e.Date).Lower(utcCutoffDate));
            return RemoveAllAsync(NewQuery().WithOrganizationId(organizationId).WithElasticFilter(filter), false);
        }

        public Task HideAllByClientIpAndDateAsync(string organizationId, string clientIp, DateTime utcStartDate, DateTime utcEndDate) {
            var filter = Filter<PersistentEvent>.Term("client_ip_address", clientIp)
                && Filter<PersistentEvent>.Range(r => r.OnField(e => e.Date).GreaterOrEquals(utcStartDate))
                && Filter<PersistentEvent>.Range(r => r.OnField(e => e.Date).LowerOrEquals(utcEndDate));

            return UpdateAllAsync(organizationId, NewQuery().WithElasticFilter(filter), new { is_hidden = true });
        }
        
        public Task<FindResults<PersistentEvent>> GetByFilterAsync(string systemFilter, string userFilter, string sort, SortOrder sortOrder, string field, DateTime utcStart, DateTime utcEnd, PagingOptions paging) {
            if (String.IsNullOrEmpty(sort)) {
                sort = "date";
                sortOrder = SortOrder.Descending;
            }

            var search = NewQuery()
                .WithDateRange(utcStart, utcEnd, field ?? "date")
                .WithIndices(utcStart, utcStart, $"'{Settings.Current.AppScopePrefix}{_index.VersionedName}-'yyyyMM")
                .WithSystemFilter(systemFilter)
                .WithFilter(userFilter)
                .WithPaging(paging)
                .WithSort(e => e.OnField(sort).Order(sortOrder == SortOrder.Descending ? Nest.SortOrder.Descending : Nest.SortOrder.Ascending));

            return FindAsync(search);
        }

        public Task<FindResults<PersistentEvent>> GetMostRecentAsync(string projectId, DateTime utcStart, DateTime utcEnd, PagingOptions paging, bool includeHidden = false, bool includeFixed = false, bool includeNotFound = true) {
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

            return FindAsync(NewQuery().WithProjectId(projectId).WithElasticFilter(filter).WithIndices(utcStart, utcEnd).WithPaging(paging).WithSort(s => s.OnField(e => e.Date).Descending()));
        }

        public Task<FindResults<PersistentEvent>> GetByStackIdOccurrenceDateAsync(string stackId, DateTime utcStart, DateTime utcEnd, PagingOptions paging) {
            var filter = new FilterContainer();

            if (utcStart != DateTime.MinValue)
                filter &= Filter<PersistentEvent>.Range(r => r.OnField(e => e.Date).GreaterOrEquals(utcStart));
            if (utcEnd != DateTime.MaxValue)
                filter &= Filter<PersistentEvent>.Range(r => r.OnField(e => e.Date).LowerOrEquals(utcEnd));

            return FindAsync(NewQuery().WithStackId(stackId).WithFilter(filter).WithIndices(utcStart, utcEnd).WithPaging(paging).WithSort(s => s.OnField(e => e.Date).Descending()));
        }

        public Task<FindResults<PersistentEvent>> GetByReferenceIdAsync(string projectId, string referenceId) {
            var filter = Filter<PersistentEvent>.Term(e => e.ReferenceId, referenceId);
            return FindAsync(NewQuery()
                .WithProjectId(projectId)
                .WithElasticFilter(filter)
                .WithSort(s => s.OnField(e => e.Date).Descending())
                .WithLimit(10));
        }

        public async Task RemoveOldestEventsAsync(string stackId, int maxEventsPerStack) {
            var options = new PagingOptions { Limit = maxEventsPerStack, Page = 2 };
            var events = await GetOldestEventsAsync(stackId, options).AnyContext();
            while (events.Total > 0) {
                await RemoveAsync(events.Documents).AnyContext();

                if (!options.HasMore)
                    break;

                events = await GetOldestEventsAsync(stackId, options).AnyContext();
            }
        }

        private Task<FindResults<PersistentEvent>> GetOldestEventsAsync(string stackId, PagingOptions options) {
            return FindAsync(NewQuery()
                .WithStackId(stackId)
                .WithSelectedFields("id", "organization_id", "project_id", "stack_id")
                .WithSort(s => s.OnField(e => e.Date).Descending())
                .WithPaging(options));
        }
        
        public async Task<PreviousAndNextEventIdResult> GetPreviousAndNextEventIdsAsync(PersistentEvent ev, string systemFilter, string userFilter, DateTime? utcStart, DateTime? utcEnd) {
            var previous = await GetPreviousEventIdAsync(ev, systemFilter, userFilter, utcStart, utcEnd).AnyContext();
            var next = await GetNextEventIdAsync(ev, systemFilter, userFilter, utcStart, utcEnd).AnyContext();

            return new PreviousAndNextEventIdResult {
                Previous = previous,
                Next = next
            };
        }

        private async Task<string> GetPreviousEventIdAsync(PersistentEvent ev, string systemFilter = null, string userFilter = null, DateTime? utcStart = null, DateTime? utcEnd = null) {
            if (ev == null)
                return null;
            
            if (!utcStart.HasValue)
                utcStart = DateTime.MinValue;

            if (!utcEnd.HasValue)
                utcEnd = DateTime.MaxValue;

            var utcEventDate = ev.Date.ToUniversalTime().DateTime;
            // utcEnd is before the current event date.
            if (utcStart > utcEventDate || utcEnd < utcEventDate)
                return null;

            if (String.IsNullOrEmpty(userFilter))
                userFilter = "stack:" + ev.StackId;
            
            var results = await FindAsync(NewQuery()
                .WithDateRange(utcStart, utcEventDate, "date")
                .WithIndices(utcStart, utcEventDate, $"'{Settings.Current.AppScopePrefix}{_index.VersionedName}-'yyyyMM")
                .WithSort("date", Foundatio.Repositories.Models.SortOrder.Descending)
                .WithLimit(10)
                .WithSelectedFields("id", "date")
                .WithSystemFilder(systemFilter)
                .WithElasticFilter(!Filter<PersistentEvent>.Ids(new[] { ev.Id }))
                .WithQuery(userFilter)).AnyContext();

            if (results.Total == 0)
                return null;

            // make sure we don't have records with the exact same occurrence date
            if (results.Documents.All(t => t.Date != ev.Date))
                return results.Documents.OrderByDescending(t => t.Date).ThenByDescending(t => t.Id).First().Id;

            // we have records with the exact same occurrence date, we need to figure out the order of those
            // put our target error into the mix, sort it and return the result before the target
            var unionResults = results.Documents.Union(new[] { ev })
                .OrderBy(t => t.Date.UtcTicks).ThenBy(t => t.Id)
                .ToList();

            var index = unionResults.FindIndex(t => t.Id == ev.Id);
            return index == 0 ? null : unionResults[index - 1].Id;
        }
        
        private async Task<string> GetNextEventIdAsync(PersistentEvent ev, string systemFilter = null, string userFilter = null, DateTime? utcStart = null, DateTime? utcEnd = null) {
            if (ev == null)
                return null;

            if (!utcStart.HasValue)
                utcStart = DateTime.MinValue;

            if (!utcEnd.HasValue)
                utcEnd = DateTime.MaxValue;

            var utcEventDate = ev.Date.ToUniversalTime().DateTime;
            // utcEnd is before the current event date.
            if (utcStart > utcEventDate || utcEnd < utcEventDate)
                return null;

            if (String.IsNullOrEmpty(userFilter))
                userFilter = "stack:" + ev.StackId;
            
            var results = await FindAsync(NewQuery()
                .WithDateRange(utcEventDate, utcEnd, "date")
                .WithIndices(utcStart, utcEventDate, $"'{Settings.Current.AppScopePrefix}{_index.VersionedName}-'yyyyMM")
                .WithSort(s => s.OnField(e => e.Date).Ascending())
                .WithLimit(10)
                .WithSelectedFields("id", "date")
                .WithSystemFilter(systemFilter)
                .WithElasticFilter(!Filter<PersistentEvent>.Ids(new[] { ev.Id }))
                .WithQuery(userFilter)).AnyContext();

            if (results.Total == 0)
                return null;

            // make sure we don't have records with the exact same occurrence date
            if (results.Documents.All(t => t.Date != ev.Date))
                return results.Documents.OrderBy(t => t.Date).ThenBy(t => t.Id).First().Id;

            // we have records with the exact same occurrence date, we need to figure out the order of those
            // put our target error into the mix, sort it and return the result after the target
            var unionResults = results.Documents.Union(new[] { ev })
                .OrderBy(t => t.Date.Ticks).ThenBy(t => t.Id)
                .ToList();

            var index = unionResults.FindIndex(t => t.Id == ev.Id);
            return index == unionResults.Count - 1 ? null : unionResults[index + 1].Id;
        }
        
        public override Task<FindResults<PersistentEvent>> GetByOrganizationIdAsync(string organizationId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            return GetByOrganizationIdsAsync(new[] { organizationId }, paging, useCache, expiresIn);
        }

        public Task<long> GetCountByOrganizationIdAsync(string organizationId) {
            return CountAsync(NewQuery().WithOrganizationId(organizationId));
        }

        public Task<long> GetCountByStackIdAsync(string stackId) {
            return CountAsync(NewQuery().WithStackId(stackId));
        }

        public override Task<FindResults<PersistentEvent>> GetByOrganizationIdsAsync(ICollection<string> organizationIds, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            return base.GetByOrganizationIdsAsync(organizationIds, GetPagingWithSortingOptions(paging), useCache, expiresIn);
        }

        public Task<FindResults<PersistentEvent>> GetByOrganizationIdsAsync(ICollection<string> organizationIds, string filter = null, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            if (organizationIds == null || organizationIds.Count == 0)
                return Task.FromResult(new FindResults<PersistentEvent>());

            string cacheKey = String.Concat("org:", String.Join("", organizationIds).GetHashCode().ToString());
            return FindAsync(NewQuery()
                .WithOrganizationIds(organizationIds)
                .WithFilter(filter)
                .WithPaging(GetPagingWithSortingOptions(paging))
                .WithCacheKey(useCache ? cacheKey : null)
                .WithExpiresIn(expiresIn));
        }

        public override Task<FindResults<PersistentEvent>> GetByStackIdAsync(string stackId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            return base.GetByStackIdAsync(stackId, GetPagingWithSortingOptions(paging), useCache, expiresIn);
        }

        public override Task<FindResults<PersistentEvent>> GetByProjectIdAsync(string projectId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            return base.GetByProjectIdAsync(projectId, GetPagingWithSortingOptions(paging), useCache, expiresIn);
        }

        public Task<long> GetCountByProjectIdAsync(string projectId) {
            return CountAsync(NewQuery().WithProjectId(projectId));
        }

        private ElasticSearchPagingOptions<PersistentEvent> GetPagingWithSortingOptions(PagingOptions paging) {
            var pagingOptions = new ElasticSearchPagingOptions<PersistentEvent>(paging);
            pagingOptions.SortBy.Add(s => s.OnField(f => f.Date).Descending());
            pagingOptions.SortBy.Add(s => s.OnField("_uid").Descending());

            return pagingOptions;
        }
    }
}