using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Results;
using Exceptionless.Core.Repositories.Configuration;
using FluentValidation;
using Foundatio.Messaging;
using Nest;

namespace Exceptionless.Core.Repositories {
    public class EventRepository : ElasticSearchRepositoryOwnedByOrganizationAndProjectAndStack<PersistentEvent>, IEventRepository {
        public EventRepository(IElasticClient elasticClient, EventIndex index,  IValidator<PersistentEvent> validator = null, IMessagePublisher messagePublisher = null)
            : base(elasticClient, index, validator, null, messagePublisher) {
            EnableCache = false;
            BatchNotifications = true;
        }

        public Task UpdateFixedByStackAsync(string organizationId, string stackId, bool value) {
            if (String.IsNullOrEmpty(stackId))
                throw new ArgumentNullException(nameof(stackId));

            return UpdateAllAsync(organizationId, new QueryOptions().WithStackId(stackId), new { is_fixed = value });
        }

        public Task UpdateHiddenByStackAsync(string organizationId, string stackId, bool value) {
            if (String.IsNullOrEmpty(stackId))
                throw new ArgumentNullException(nameof(stackId));

            return UpdateAllAsync(organizationId, new QueryOptions().WithStackId(stackId), new { is_hidden = value });
        }

        public Task RemoveAllByDateAsync(string organizationId, DateTime utcCutoffDate) {
            var filter = Filter<PersistentEvent>.Range(r => r.OnField(e => e.Date).Lower(utcCutoffDate));
            return RemoveAllAsync(new ElasticSearchOptions<PersistentEvent>().WithOrganizationId(organizationId).WithFilter(filter), false);
        }

        public Task HideAllByClientIpAndDateAsync(string organizationId, string clientIp, DateTime utcStartDate, DateTime utcEndDate) {
            var filter = Filter<PersistentEvent>.Term("client_ip_address", clientIp)
                && Filter<PersistentEvent>.Range(r => r.OnField(e => e.Date).GreaterOrEquals(utcStartDate))
                && Filter<PersistentEvent>.Range(r => r.OnField(e => e.Date).LowerOrEquals(utcEndDate));

            return UpdateAllAsync(organizationId, new ElasticSearchOptions<PersistentEvent>().WithFilter(filter), new { is_hidden = true });
        }
        
        public Task<FindResults<PersistentEvent>> GetByFilterAsync(string systemFilter, string userFilter, string sort, SortOrder sortOrder, string field, DateTime utcStart, DateTime utcEnd, PagingOptions paging) {
            if (String.IsNullOrEmpty(sort)) {
                sort = "date";
                sortOrder = SortOrder.Descending;
            }

            var search = new ElasticSearchOptions<PersistentEvent>()
                .WithDateRange(utcStart, utcEnd, field ?? "date")
                .WithIndicesFromDateRange($"'{_index.VersionedName}-'yyyyMM")
                .WithFilter(!String.IsNullOrEmpty(systemFilter) ? Filter<PersistentEvent>.Query(q => q.QueryString(qs => qs.DefaultOperator(Operator.And).Query(systemFilter))) : null)
                .WithQuery(userFilter)
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

            return FindAsync(new ElasticSearchOptions<PersistentEvent>().WithProjectId(projectId).WithFilter(filter).WithIndices(utcStart, utcEnd).WithPaging(paging).WithSort(s => s.OnField(e => e.Date).Descending()));
        }

        public Task<FindResults<PersistentEvent>> GetByStackIdOccurrenceDateAsync(string stackId, DateTime utcStart, DateTime utcEnd, PagingOptions paging) {
            var filter = new FilterContainer();

            if (utcStart != DateTime.MinValue)
                filter &= Filter<PersistentEvent>.Range(r => r.OnField(e => e.Date).GreaterOrEquals(utcStart));
            if (utcEnd != DateTime.MaxValue)
                filter &= Filter<PersistentEvent>.Range(r => r.OnField(e => e.Date).LowerOrEquals(utcEnd));

            return FindAsync(new ElasticSearchOptions<PersistentEvent>().WithStackId(stackId).WithFilter(filter).WithIndices(utcStart, utcEnd).WithPaging(paging).WithSort(s => s.OnField(e => e.Date).Descending()));
        }

        public Task<FindResults<PersistentEvent>> GetByReferenceIdAsync(string projectId, string referenceId) {
            var filter = Filter<PersistentEvent>.Term(e => e.ReferenceId, referenceId);
            return FindAsync(new ElasticSearchOptions<PersistentEvent>()
                .WithProjectId(projectId)
                .WithFilter(filter)
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
            return FindAsync(new ElasticSearchOptions<PersistentEvent>()
                .WithStackId(stackId)
                .WithFields("id", "organization_id", "project_id", "stack_id")
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

            var filter = !Filter<PersistentEvent>.Ids(new[] { ev.Id })
                && Filter<PersistentEvent>.Query(q => q.QueryString(qs => qs.DefaultOperator(Operator.And).Query(systemFilter)));

            var results = await FindAsync(new ElasticSearchOptions<PersistentEvent>()
                .WithDateRange(utcStart, utcEventDate, "date")
                .WithIndicesFromDateRange($"'{_index.VersionedName}-'yyyyMM")
                .WithSort(s => s.OnField(e => e.Date).Descending())
                .WithLimit(10)
                .WithFields("id", "date")
                .WithFilter(filter)
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

            var filter = !Filter<PersistentEvent>.Ids(new[] { ev.Id })
                && Filter<PersistentEvent>.Query(q => q.QueryString(qs => qs.DefaultOperator(Operator.And).Query(systemFilter)));

            var results = await FindAsync(new ElasticSearchOptions<PersistentEvent>()
                .WithDateRange(utcEventDate, utcEnd, "date")
                .WithIndicesFromDateRange($"'{_index.VersionedName}-'yyyyMM")
                .WithSort(s => s.OnField(e => e.Date).Ascending())
                .WithLimit(10)
                .WithFields("id", "date")
                .WithFilter(filter)
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
            return CountAsync(new ElasticSearchOptions<PersistentEvent>().WithOrganizationId(organizationId));
        }

        public Task<long> GetCountByStackIdAsync(string stackId) {
            return CountAsync(new ElasticSearchOptions<PersistentEvent>().WithStackId(stackId));
        }

        public override Task<FindResults<PersistentEvent>> GetByOrganizationIdsAsync(ICollection<string> organizationIds, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            return base.GetByOrganizationIdsAsync(organizationIds, GetPagingWithSortingOptions(paging), useCache, expiresIn);
        }

        public Task<FindResults<PersistentEvent>> GetByOrganizationIdsAsync(ICollection<string> organizationIds, string query = null, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            if (organizationIds == null || organizationIds.Count == 0)
                return Task.FromResult(new FindResults<PersistentEvent>());

            string cacheKey = String.Concat("org:", String.Join("", organizationIds).GetHashCode().ToString());
            return FindAsync(new ElasticSearchOptions<PersistentEvent>()
                .WithOrganizationIds(organizationIds)
                .WithQuery(query)
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
            return CountAsync(new ElasticSearchOptions<PersistentEvent>().WithProjectId(projectId));
        }

        private ElasticSearchPagingOptions<PersistentEvent> GetPagingWithSortingOptions(PagingOptions paging) {
            var pagingOptions = new ElasticSearchPagingOptions<PersistentEvent>(paging);
            pagingOptions.SortBy.Add(s => s.OnField(f => f.Date).Descending());
            pagingOptions.SortBy.Add(s => s.OnField("_uid").Descending());

            return pagingOptions;
        }
    }
}