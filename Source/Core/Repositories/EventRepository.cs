using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Results;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Repositories.Queries;
using Exceptionless.DateTimeExtensions;
using Foundatio.Elasticsearch.Repositories;
using Foundatio.Elasticsearch.Repositories.Queries;
using Foundatio.Elasticsearch.Repositories.Queries.Options;
using Foundatio.Logging;
using Foundatio.Repositories.Models;
using Foundatio.Repositories.Queries;
using Foundatio.Repositories.Utility;
using Nest;
using SortOrder = Foundatio.Repositories.Models.SortOrder;
using Fields = Exceptionless.Core.Repositories.Configuration.EventIndex.Fields.PersistentEvent;

namespace Exceptionless.Core.Repositories {
    public class EventRepository : RepositoryOwnedByOrganizationAndProjectAndStack<PersistentEvent>, IEventRepository {
        // NOTE: v1 event submission allowed users to specify there own id which may have been created with invalid date times.
        private static readonly DateTime _minObjectidDate = new DateTime(2000, 1, 1);

        public EventRepository(ElasticRepositoryContext<PersistentEvent> context, EventIndex index, ILoggerFactory loggerFactory = null) : base(context, index, loggerFactory) {
            DisableCache();
            BatchNotifications = true;

            GetDocumentIdFunc = GetDocumentId;
            GetDocumentIndexFunc = GetDocumentIndex;
        }

        protected override object Options { get; } = new QueryOptions(typeof(PersistentEvent)) {
            DefaultExcludes = new[] { Fields.IDX }
        };

        private string GetDocumentId(PersistentEvent ev) {
            // if date falls in the current months index then return a new object id.
            var date = ev.Date.ToUniversalTime();
            if (date.IntersectsMonth(DateTime.UtcNow))
                return ObjectId.GenerateNewId().ToString();

            // GenerateNewId will translate it to utc.
            return ObjectId.GenerateNewId(ev.Date.DateTime).ToString();
        }

        private string GetDocumentIndex(PersistentEvent ev) {
            return GetIndexById(ev.Id);
        }

        protected override string GetIndexById(string id) {
            ObjectId objectId;
            if (ObjectId.TryParse(id, out objectId) && objectId.CreationTime.ToUniversalTime() > _minObjectidDate)
                return String.Concat(_index.VersionedName, "-", objectId.CreationTime.ToString("yyyyMM"));

            return null;
        }
        
        // TODO: We need to index and search by the created time.
        public Task<FindResults<PersistentEvent>> GetOpenSessionsAsync(DateTime createdBeforeUtc, PagingOptions paging = null) {
            var filter = Query<PersistentEvent>.Term(e => e.Type, Event.KnownTypes.Session) && Query<PersistentEvent>.Missing(e => e.Idx[Event.KnownDataKeys.SessionEnd + "-d"]);
            if (createdBeforeUtc.Ticks > 0)
                filter &= Query<PersistentEvent>.DateRange(r => r.Field(e => e.Date).LessThanOrEquals(createdBeforeUtc));

            return FindAsync(new ExceptionlessQuery()
                .WithElasticFilter(filter)
                .WithSort(EventIndex.Fields.PersistentEvent.Date, SortOrder.Descending)
                .WithPaging(paging));
        }

        public async Task<bool> UpdateSessionStartLastActivityAsync(string id, DateTime lastActivityUtc, bool isSessionEnd = false, bool hasError = false, bool sendNotifications = true) {
            var ev = await GetByIdAsync(id).AnyContext();
            if (!ev.UpdateSessionStart(lastActivityUtc, isSessionEnd, hasError))
                return false;

            await SaveAsync(ev, sendNotifications: sendNotifications).AnyContext();
            return true;
        }

        public Task UpdateFixedByStackAsync(string organizationId, string stackId, bool value) {
            if (String.IsNullOrEmpty(stackId))
                throw new ArgumentNullException(nameof(stackId));

            return UpdateAllAsync(organizationId, new ExceptionlessQuery().WithStackId(stackId), new { is_fixed = value });
        }

        public Task UpdateHiddenByStackAsync(string organizationId, string stackId, bool value) {
            if (String.IsNullOrEmpty(stackId))
                throw new ArgumentNullException(nameof(stackId));

            return UpdateAllAsync(organizationId, new ExceptionlessQuery().WithStackId(stackId), new { is_hidden = value });
        }

        public Task RemoveAllByDateAsync(string organizationId, DateTime utcCutoffDate) {
            var filter = Query<PersistentEvent>.DateRange(r => r.Field(e => e.Date).LessThan(utcCutoffDate));
            return RemoveAllAsync(new ExceptionlessQuery().WithOrganizationId(organizationId).WithElasticFilter(filter), false);
        }

        public Task HideAllByClientIpAndDateAsync(string organizationId, string clientIp, DateTime utcStart, DateTime utcEnd) {
            // TODO: Ask about client_ip_address
            var query = new ExceptionlessQuery()
                .WithElasticFilter(Query<PersistentEvent>.Term(t => "client_ip_address", clientIp))
                .WithDateRange(utcStart, utcEnd, Fields.Date)
                .WithIndices(utcStart, utcEnd, $"'{_index.VersionedName}-'yyyyMM");

            return UpdateAllAsync(organizationId, query, new { is_hidden = true });
        }

        public Task<FindResults<PersistentEvent>> GetByFilterAsync(string systemFilter, string userFilter, SortingOptions sorting, string field, DateTime utcStart, DateTime utcEnd, PagingOptions paging) {
            if (sorting.Fields.Count == 0)
                sorting.Fields.Add(new FieldSort { Field = Fields.Date, Order = SortOrder.Descending });
            
            var search = new ExceptionlessQuery()
                .WithDateRange(utcStart, utcEnd, field ?? Fields.Date)
                .WithIndices(utcStart, utcEnd, $"'{_index.VersionedName}-'yyyyMM")
                .WithSystemFilter(systemFilter)
                .WithFilter(userFilter)
                .WithPaging(paging)
                .WithSort(sorting);

            return FindAsync(search);
        }

        public Task<FindResults<PersistentEvent>> GetMostRecentAsync(string projectId, DateTime utcStart, DateTime utcEnd, PagingOptions paging, bool includeHidden = false, bool includeFixed = false, bool includeNotFound = true) {
            var filter = new QueryContainer();

            if (!includeHidden)
                filter &= !Query<PersistentEvent>.Term(e => e.IsHidden, true);

            if (!includeFixed)
                filter &= !Query<PersistentEvent>.Term(e => e.IsFixed, true);

            if (!includeNotFound)
                filter &= !Query<PersistentEvent>.Term(e => e.Type, "404");

            return FindAsync(new ExceptionlessQuery()
                .WithProjectId(projectId)
                .WithElasticFilter(filter)
                .WithDateRange(utcStart, utcEnd, Fields.Date)
                .WithIndices(utcStart, utcEnd, $"'{_index.VersionedName}-'yyyyMM")
                .WithPaging(paging)
                .WithSort(EventIndex.Fields.PersistentEvent.Date, SortOrder.Descending));
        }

        public Task<FindResults<PersistentEvent>> GetByStackIdOccurrenceDateAsync(string stackId, DateTime utcStart, DateTime utcEnd, PagingOptions paging) {
            return FindAsync(new ExceptionlessQuery()
                .WithStackId(stackId)
                .WithDateRange(utcStart, utcEnd, Fields.Date)
                .WithIndices(utcStart, utcEnd, $"'{_index.VersionedName}-'yyyyMM")
                .WithPaging(paging)
                .WithSort(EventIndex.Fields.PersistentEvent.Date, SortOrder.Descending));
        }

        public Task<FindResults<PersistentEvent>> GetByReferenceIdAsync(string projectId, string referenceId) {
            var filter = Query<PersistentEvent>.Term(e => e.ReferenceId, referenceId);
            return FindAsync(new ExceptionlessQuery()
                .WithProjectId(projectId)
                .WithElasticFilter(filter)
                .WithSort(EventIndex.Fields.PersistentEvent.Date, SortOrder.Descending)
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

        private Task<FindResults<PersistentEvent>> GetOldestEventsAsync(string stackId, PagingOptions options) {
            // TODO: Ask about selected fields.
            return FindAsync(new ExceptionlessQuery()
                .WithStackId(stackId)
                .WithSelectedFields("id", "organization_id", "project_id", "stack_id")
                .WithSort(EventIndex.Fields.PersistentEvent.Date, SortOrder.Descending)
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

            var results = await FindAsync(new ExceptionlessQuery()
                .WithDateRange(utcStart, utcEventDate, Fields.Date)
                .WithIndices(utcStart, utcEventDate, $"'{_index.VersionedName}-'yyyyMM")
                .WithSort(FieldNamesFieldlds.Date, SortOrder.Descending)
                .WithLimit(10)
                .WithSelectedFields("id", "date")
                .WithSystemFilter(systemFilter)
                .WithElasticFilter(!Query<PersistentEvent>.Ids(new[] { ev.Id }))
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
                userFilter = $"{Fields.StackId}:{ev.StackId}";

            var results = await FindAsync(new ExceptionlessQuery()
                .WithDateRange(utcEventDate, utcEnd, Fields.Date)
                .WithIndices(utcStart, utcEventDate, $"'{_index.VersionedName}-'yyyyMM")
                .WithSort(EventIndex.Fields.PersistentEvent.Date, SortOrder.Ascending)
                .WithLimit(10)
                .WithSelectedFields("id", "date")
                .WithSystemFilter(systemFilter)
                .WithElasticFilter(!Query<PersistentEvent>.Ids(new[] { ev.Id }))
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

        public override Task<FindResults<PersistentEvent>> GetByOrganizationIdAsync(string organizationId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            return GetByOrganizationIdsAsync(new[] { organizationId }, paging, useCache, expiresIn);
        }

        public Task<long> GetCountByOrganizationIdAsync(string organizationId) {
            return CountAsync(new ExceptionlessQuery().WithOrganizationId(organizationId));
        }

        public Task<long> GetCountByStackIdAsync(string stackId) {
            return CountAsync(new ExceptionlessQuery().WithStackId(stackId));
        }

        public override Task<FindResults<PersistentEvent>> GetByOrganizationIdsAsync(ICollection<string> organizationIds, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            if (organizationIds == null || organizationIds.Count == 0)
                return Task.FromResult(new FindResults<PersistentEvent> { Documents = new List<PersistentEvent>(), Total = 0 });

            // NOTE: There is no way to currently invalidate this.. If you try and cache this result, you should expect it to be dirty.
            string cacheKey = String.Concat("org:", String.Join("", organizationIds).GetHashCode().ToString());
            return FindAsync(new ExceptionlessQuery()
                .WithOrganizationIds(organizationIds)
                .WithPaging(paging)
                .WithSort(EventIndex.Fields.PersistentEvent.Date, SortOrder.Descending)
                .WithSort("_uid", SortOrder.Descending)
                .WithCacheKey(useCache ? cacheKey : null)
                .WithExpiresIn(expiresIn));
        }

        public Task<FindResults<PersistentEvent>> GetByOrganizationIdsAsync(ICollection<string> organizationIds, string filter = null, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            if (organizationIds == null || organizationIds.Count == 0)
                return Task.FromResult(new FindResults<PersistentEvent>());

            string cacheKey = String.Concat("org:", String.Join("", organizationIds).GetHashCode().ToString());
            return FindAsync(new ExceptionlessQuery()
                .WithOrganizationIds(organizationIds)
                .WithPaging(paging)
                .WithFilter(filter)
                .WithSort(EventIndex.Fields.PersistentEvent.Date, SortOrder.Descending)
                .WithSort("_uid", SortOrder.Descending)
                .WithCacheKey(useCache ? cacheKey : null)
                .WithExpiresIn(expiresIn));
        }

        public override Task<FindResults<PersistentEvent>> GetByStackIdAsync(string stackId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            return FindAsync(new ExceptionlessQuery()
                .WithStackId(stackId)
                .WithPaging(paging)
                .WithSort(EventIndex.Fields.PersistentEvent.Date, SortOrder.Descending)
                .WithSort("_uid", SortOrder.Descending)
                .WithCacheKey(useCache ? String.Concat("stack:", stackId) : null)
                .WithExpiresIn(expiresIn));
        }

        public override Task<FindResults<PersistentEvent>> GetByProjectIdAsync(string projectId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            return FindAsync(new ExceptionlessQuery()
                .WithProjectId(projectId)
                .WithPaging(paging)
                .WithSort(EventIndex.Fields.PersistentEvent.Date, SortOrder.Descending)
                .WithSort("_uid", SortOrder.Descending)
                .WithCacheKey(useCache ? String.Concat("project:", projectId) : null)
                .WithExpiresIn(expiresIn));
        }

        public Task<long> GetCountByProjectIdAsync(string projectId) {
            return CountAsync(new ExceptionlessQuery().WithProjectId(projectId));
        }
    }
}
