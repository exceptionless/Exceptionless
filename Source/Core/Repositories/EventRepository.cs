using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Repositories.Queries;
using FluentValidation;
using Foundatio.Caching;
using Foundatio.Logging;
using Foundatio.Messaging;
using Foundatio.Repositories.Elasticsearch.Queries;
using Foundatio.Repositories.Elasticsearch.Queries.Builders;
using Foundatio.Repositories.Elasticsearch.Queries.Options;
using Foundatio.Repositories.Models;
using Foundatio.Repositories.Queries;
using Nest;
using SortOrder = Foundatio.Repositories.Models.SortOrder;

namespace Exceptionless.Core.Repositories {
    public class EventRepository : RepositoryOwnedByOrganizationAndProjectAndStack<PersistentEvent>, IEventRepository {
        public EventRepository(ExceptionlessElasticConfiguration configuration, IValidator<PersistentEvent> validator, ICacheClient cache, IMessagePublisher messagePublisher, ILogger<EventRepository> logger) 
            : base(configuration.Client, validator, cache, messagePublisher, logger) {
            ElasticType = configuration.Events.Event;

            DisableCache();
            BatchNotifications = true;
            DefaultExcludes.Add("idx");
        }
        
        // TODO: We need to index and search by the created time.
        public Task<IFindResults<PersistentEvent>> GetOpenSessionsAsync(DateTime createdBeforeUtc, PagingOptions paging = null) {
            var filter = Filter<PersistentEvent>.Term(e => e.Type, Event.KnownTypes.Session) && Filter<PersistentEvent>.Missing(e => e.Idx[Event.KnownDataKeys.SessionEnd + "-d"]);
            if (createdBeforeUtc.Ticks > 0)
                filter &= Filter<PersistentEvent>.Range(r => r.OnField(e => e.Date).LowerOrEquals(createdBeforeUtc));

            return FindAsync(new ExceptionlessQuery()
                .WithElasticFilter(filter)
                .WithSort(EventIndexType.Fields.Date, SortOrder.Descending)
                .WithPaging(paging));
        }

        public async Task<bool> UpdateSessionStartLastActivityAsync(string id, DateTime lastActivityUtc, bool isSessionEnd = false, bool hasError = false, bool sendNotifications = true) {
            var ev = await GetByIdAsync(id).AnyContext();
            if (!ev.UpdateSessionStart(lastActivityUtc, isSessionEnd, hasError))
                return false;

            await SaveAsync(ev, sendNotifications: sendNotifications).AnyContext();
            return true;
        }
        
        public Task UpdateFixedByStackAsync(string organizationId, string stackId, bool isFixed, bool sendNotifications = true) {
            if (String.IsNullOrEmpty(stackId))
                throw new ArgumentNullException(nameof(stackId));

            var query = new ExceptionlessQuery()
                .WithOrganizationId(organizationId)
                .WithStackId(stackId)
                .WithFieldEquals(EventIndexType.Fields.IsFixed, !isFixed);

            // TODO: Update this to use the update by query syntax that's coming in 2.3.
            return PatchAllAsync(organizationId, query, new { is_fixed = isFixed }, sendNotifications);
        }

        public Task UpdateHiddenByStackAsync(string organizationId, string stackId, bool isHidden, bool sendNotifications = true) {
            if (String.IsNullOrEmpty(stackId))
                throw new ArgumentNullException(nameof(stackId));
            
            var query = new ExceptionlessQuery()
                .WithOrganizationId(organizationId)
                .WithStackId(stackId)
                .WithFieldEquals(EventIndexType.Fields.IsHidden, !isHidden);

            // TODO: Update this to use the update by query syntax that's coming in 2.3.
            return PatchAllAsync(organizationId, query, new { is_hidden = isHidden }, sendNotifications);
        }

        public Task RemoveAllByDateAsync(string organizationId, DateTime utcCutoffDate) {
            var filter = Filter<PersistentEvent>.Range(r => r.OnField(e => e.Date).Lower(utcCutoffDate));
            return RemoveAllAsync(new ExceptionlessQuery().WithOrganizationId(organizationId).WithElasticFilter(filter), false);
        }

        public Task HideAllByClientIpAndDateAsync(string organizationId, string clientIp, DateTime utcStart, DateTime utcEnd) {
            var query = new ExceptionlessQuery()
                .WithElasticFilter(Filter<PersistentEvent>.Term("client_ip_address", clientIp))
                .WithDateRange(utcStart, utcEnd, EventIndexType.Fields.Date)
                .WithIndexes(utcStart, utcEnd);

            return PatchAllAsync(organizationId, query, new { is_hidden = true });
        }

        public Task<IFindResults<PersistentEvent>> GetByFilterAsync(IRepositoryQuery systemFilter, string userFilter, SortingOptions sorting, string field, DateTime utcStart, DateTime utcEnd, PagingOptions paging) {
            if (sorting.Fields.Count == 0)
                sorting.Fields.Add(new FieldSort { Field = EventIndexType.Fields.Date, Order = SortOrder.Descending });
            
            var search = new ExceptionlessQuery()
                .WithDateRange(utcStart, utcEnd, field ?? EventIndexType.Fields.Date)
                .WithIndexes(utcStart, utcEnd)
                .WithSystemFilter(systemFilter)
                .WithFilter(userFilter)
                .WithPaging(paging)
                .WithSort(sorting);

            return FindAsync(search);
        }

        public Task<IFindResults<PersistentEvent>> GetMostRecentAsync(string projectId, DateTime utcStart, DateTime utcEnd, PagingOptions paging, bool includeHidden = false, bool includeFixed = false, bool includeNotFound = true) {
            var filter = new FilterContainer();

            if (!includeHidden)
                filter &= !Filter<PersistentEvent>.Term(e => e.IsHidden, true);

            if (!includeFixed)
                filter &= !Filter<PersistentEvent>.Term(e => e.IsFixed, true);

            if (!includeNotFound)
                filter &= !Filter<PersistentEvent>.Term(e => e.Type, "404");

            return FindAsync(new ExceptionlessQuery()
                .WithProjectId(projectId)
                .WithElasticFilter(filter)
                .WithDateRange(utcStart, utcEnd, EventIndexType.Fields.Date)
                .WithIndexes(utcStart, utcEnd)
                .WithPaging(paging)
                .WithSort(EventIndexType.Fields.Date, SortOrder.Descending));
        }

        public Task<IFindResults<PersistentEvent>> GetByStackIdOccurrenceDateAsync(string stackId, DateTime utcStart, DateTime utcEnd, PagingOptions paging) {
            return FindAsync(new ExceptionlessQuery()
                .WithStackId(stackId)
                .WithDateRange(utcStart, utcEnd, EventIndexType.Fields.Date)
                .WithIndexes(utcStart, utcEnd)
                .WithPaging(paging)
                .WithSort(EventIndexType.Fields.Date, SortOrder.Descending));
        }

        public Task<IFindResults<PersistentEvent>> GetByReferenceIdAsync(string projectId, string referenceId) {
            var filter = Filter<PersistentEvent>.Term(e => e.ReferenceId, referenceId);
            return FindAsync(new ExceptionlessQuery()
                .WithProjectId(projectId)
                .WithElasticFilter(filter)
                .WithSort(EventIndexType.Fields.Date, SortOrder.Descending)
                .WithLimit(10));
        }

        public async Task RemoveOldestEventsAsync(string stackId, int maxEventsPerStack) {
            var options = new PagingOptions { Limit = maxEventsPerStack, Page = 2 };
            var events = await GetOldestEventsAsync(stackId, options).AnyContext();
            while (events.Total > 0) {
                await RemoveAsync(events.Documents).AnyContext();

                if (!events.HasMore)
                    break;

                events = await GetOldestEventsAsync(stackId, options).AnyContext();
            }
        }

        private Task<IFindResults<PersistentEvent>> GetOldestEventsAsync(string stackId, PagingOptions options) {
            return FindAsync(new ExceptionlessQuery()
                .WithStackId(stackId)
                .WithSelectedFields("id", "organization_id", "project_id", "stack_id")
                .WithSort(EventIndexType.Fields.Date, SortOrder.Descending)
                .WithPaging(options));
        }

        public async Task<PreviousAndNextEventIdResult> GetPreviousAndNextEventIdsAsync(PersistentEvent ev, IRepositoryQuery systemFilter, string userFilter, DateTime? utcStart, DateTime? utcEnd) {
            var previous = await GetPreviousEventIdAsync(ev, systemFilter, userFilter, utcStart, utcEnd).AnyContext();
            var next = await GetNextEventIdAsync(ev, systemFilter, userFilter, utcStart, utcEnd).AnyContext();

            return new PreviousAndNextEventIdResult {
                Previous = previous,
                Next = next
            };
        }

        private async Task<string> GetPreviousEventIdAsync(PersistentEvent ev, IRepositoryQuery systemFilter = null, string userFilter = null, DateTime? utcStart = null, DateTime? utcEnd = null) {
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

            var results = await FindAsync(new ExceptionlessQuery()
                .WithDateRange(utcStart, utcEventDate, EventIndexType.Fields.Date)
                .WithIndexes(utcStart, utcEventDate)
                .WithSort(EventIndexType.Fields.Date, SortOrder.Descending)
                .WithLimit(10)
                .WithSelectedFields("id", "date")
                .WithSystemFilter(systemFilter)
                .WithElasticFilter(!Filter<PersistentEvent>.Ids(new[] { ev.Id }))
                .WithFilter(userFilter)).AnyContext();

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

        private async Task<string> GetNextEventIdAsync(PersistentEvent ev, IRepositoryQuery systemFilter = null, string userFilter = null, DateTime? utcStart = null, DateTime? utcEnd = null) {
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

            var results = await FindAsync(new ExceptionlessQuery()
                .WithDateRange(utcEventDate, utcEnd, EventIndexType.Fields.Date)
                .WithIndexes(utcStart, utcEventDate)
                .WithSort(EventIndexType.Fields.Date, SortOrder.Ascending)
                .WithLimit(10)
                .WithSelectedFields("id", "date")
                .WithSystemFilter(systemFilter)
                .WithElasticFilter(!Filter<PersistentEvent>.Ids(new[] { ev.Id }))
                .WithFilter(userFilter)).AnyContext();

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

        public override Task<IFindResults<PersistentEvent>> GetByOrganizationIdAsync(string organizationId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            return GetByOrganizationIdsAsync(new[] { organizationId }, paging, useCache, expiresIn);
        }
        
        public override Task<IFindResults<PersistentEvent>> GetByOrganizationIdsAsync(ICollection<string> organizationIds, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            if (organizationIds == null || organizationIds.Count == 0)
                return Task.FromResult<IFindResults<PersistentEvent>>(new FindResults<PersistentEvent>());

            // NOTE: There is no way to currently invalidate this.. If you try and cache this result, you should expect it to be dirty.
            string cacheKey = String.Concat("org:", String.Join("", organizationIds).GetHashCode().ToString());
            return FindAsync(new ExceptionlessQuery()
                .WithOrganizationIds(organizationIds)
                .WithPaging(paging)
                .WithSort(EventIndexType.Fields.Date, SortOrder.Descending)
                .WithSort("_uid", SortOrder.Descending)
                .WithCacheKey(useCache ? cacheKey : null)
                .WithExpiresIn(expiresIn));
        }

        public Task<IFindResults<PersistentEvent>> GetByOrganizationIdsAsync(ICollection<string> organizationIds, string filter = null, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            if (organizationIds == null || organizationIds.Count == 0)
                return Task.FromResult<IFindResults<PersistentEvent>>(new FindResults<PersistentEvent>());

            string cacheKey = String.Concat("org:", String.Join("", organizationIds).GetHashCode().ToString());
            return FindAsync(new ExceptionlessQuery()
                .WithOrganizationIds(organizationIds)
                .WithPaging(paging)
                .WithFilter(filter)
                .WithSort(EventIndexType.Fields.Date, SortOrder.Descending)
                .WithSort("_uid", SortOrder.Descending)
                .WithCacheKey(useCache ? cacheKey : null)
                .WithExpiresIn(expiresIn));
        }

        public override Task<IFindResults<PersistentEvent>> GetByStackIdAsync(string stackId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            return FindAsync(new ExceptionlessQuery()
                .WithStackId(stackId)
                .WithPaging(paging)
                .WithSort(EventIndexType.Fields.Date, SortOrder.Descending)
                .WithSort("_uid", SortOrder.Descending)
                .WithCacheKey(useCache ? String.Concat("stack:", stackId) : null)
                .WithExpiresIn(expiresIn));
        }

        public override Task<IFindResults<PersistentEvent>> GetByProjectIdAsync(string projectId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            return FindAsync(new ExceptionlessQuery()
                .WithProjectId(projectId)
                .WithPaging(paging)
                .WithSort(EventIndexType.Fields.Date, SortOrder.Descending)
                .WithSort("_uid", SortOrder.Descending)
                .WithCacheKey(useCache ? String.Concat("project:", projectId) : null)
                .WithExpiresIn(expiresIn));
        }

        public Task<CountResult> GetCountByProjectIdAsync(string projectId) {
            return CountAsync(new ExceptionlessQuery().WithProjectId(projectId));
        }

        public Task<CountResult> GetCountByStackIdAsync(string stackId) {
            return CountAsync(new ExceptionlessQuery().WithStackId(stackId));
        }
    }
}
