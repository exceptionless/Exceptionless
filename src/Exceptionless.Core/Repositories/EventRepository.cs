using System;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Repositories.Queries;
using FluentValidation;
using Foundatio.Repositories.Elasticsearch.Queries;
using Foundatio.Repositories.Elasticsearch.Queries.Builders;
using Foundatio.Repositories.Models;
using Foundatio.Repositories.Queries;
using Foundatio.Utility;
using Nest;

namespace Exceptionless.Core.Repositories {
    public class EventRepository : RepositoryOwnedByOrganizationAndProject<PersistentEvent>, IEventRepository {
        public EventRepository(ExceptionlessElasticConfiguration configuration, IValidator<PersistentEvent> validator)
            : base(configuration.Events.Event, validator) {
            DisableCache();
            BatchNotifications = true;
            DefaultExcludes.Add(ElasticType.GetFieldName(e => e.Idx));
            DefaultExcludes.Add(EventIndexType.Alias.IpAddress);
            DefaultExcludes.Add("error");
            FieldsRequiredForRemove.Add(ElasticType.GetFieldName(e => e.Date));
        }

        // TODO: We need to index and search by the created time.
        public Task<FindResults<PersistentEvent>> GetOpenSessionsAsync(DateTime createdBeforeUtc, PagingOptions paging = null) {
            var filter = Query<PersistentEvent>.Term(e => e.Type, Event.KnownTypes.Session) && !Query<PersistentEvent>.Exists(f => f.Field(e => e.Idx[Event.KnownDataKeys.SessionEnd + "-d"]));
            if (createdBeforeUtc.Ticks > 0)
                filter &= Query<PersistentEvent>.DateRange(r => r.Field(e => e.Date).LessThanOrEquals(createdBeforeUtc));

            return FindAsync(new ExceptionlessQuery()
                .WithElasticFilter(filter)
                .WithSortDescending((PersistentEvent e) => e.Date)
                .WithPaging(paging));
        }

        public async Task<bool> UpdateSessionStartLastActivityAsync(string id, DateTime lastActivityUtc, bool isSessionEnd = false, bool hasError = false, bool sendNotifications = true) {
            var ev = await GetByIdAsync(id).AnyContext();
            if (!ev.UpdateSessionStart(lastActivityUtc, isSessionEnd, hasError))
                return false;

            await SaveAsync(ev, sendNotifications: sendNotifications).AnyContext();
            return true;
        }

        public Task<long> UpdateFixedByStackAsync(string organizationId, string projectId, string stackId, bool isFixed, bool sendNotifications = true) {
            if (String.IsNullOrEmpty(stackId))
                throw new ArgumentNullException(nameof(stackId));

            var query = new ExceptionlessQuery()
                .WithOrganizationId(organizationId)
                .WithProjectId(projectId)
                .WithStackId(stackId)
                .WithFieldEquals((PersistentEvent e) => e.IsFixed, !isFixed);

            // TODO: Update this to use the update by query syntax that's coming in 2.3.
            return PatchAllAsync(query, new { is_fixed = isFixed, updated_utc = SystemClock.UtcNow });
        }

        public Task<long> UpdateHiddenByStackAsync(string organizationId, string projectId, string stackId, bool isHidden, bool sendNotifications = true) {
            if (String.IsNullOrEmpty(stackId))
                throw new ArgumentNullException(nameof(stackId));

            var query = new ExceptionlessQuery()
                .WithOrganizationId(organizationId)
                .WithProjectId(projectId)
                .WithStackId(stackId)
                .WithFieldEquals((PersistentEvent e) => e.IsHidden, !isHidden);

            // TODO: Update this to use the update by query syntax that's coming in 2.3.
            return PatchAllAsync(query, new { is_hidden = isHidden, updated_utc = SystemClock.UtcNow });
        }

        public Task<long> RemoveAllByDateAsync(string organizationId, DateTime utcCutoffDate) {
            var filter = Query<PersistentEvent>.DateRange(r => r.Field(e => e.Date).LessThan(utcCutoffDate));
            return RemoveAllAsync(new ExceptionlessQuery().WithOrganizationId(organizationId).WithElasticFilter(filter).IncludeDeleted(), false);
        }

        public override Task<long> RemoveAllByOrganizationIdAsync(string organizationId) {
            if (String.IsNullOrEmpty(organizationId))
                throw new ArgumentNullException(nameof(organizationId));

            var query = new ExceptionlessQuery().WithOrganizationId(organizationId);
            return PatchAllAsync(query, new { is_deleted = true, updated_utc = SystemClock.UtcNow });
        }

        public override Task<long> RemoveAllByProjectIdAsync(string organizationId, string projectId) {
            if (String.IsNullOrEmpty(organizationId))
                throw new ArgumentNullException(nameof(organizationId));

            if (String.IsNullOrEmpty(projectId))
                throw new ArgumentNullException(nameof(projectId));

            var query = new ExceptionlessQuery().WithOrganizationId(organizationId).WithProjectId(projectId);
            return PatchAllAsync(query, new { is_deleted = true, updated_utc = SystemClock.UtcNow });
        }

        public Task<long> RemoveAllByStackIdAsync(string organizationId, string projectId, string stackId) {
            var query = new ExceptionlessQuery().WithOrganizationId(organizationId).WithProjectId(projectId).WithStackId(stackId);
            return PatchAllAsync(query, new { is_deleted = true, updated_utc = SystemClock.UtcNow });
        }

        public Task<long> HideAllByClientIpAndDateAsync(string organizationId, string clientIp, DateTime utcStart, DateTime utcEnd) {
            var query = new ExceptionlessQuery()
                .WithOrganizationId(organizationId)
                .WithElasticFilter(Query<PersistentEvent>.Term(EventIndexType.Alias.IpAddress, clientIp))
                .WithDateRange(utcStart, utcEnd, (PersistentEvent e) => e.Date)
                .WithIndexes(utcStart, utcEnd);

            return PatchAllAsync(query, new { is_hidden = true, updated_utc = SystemClock.UtcNow });
        }

        public Task<FindResults<PersistentEvent>> GetByFilterAsync(IExceptionlessSystemFilterQuery systemFilter, string userFilter, string sort, string field, DateTime utcStart, DateTime utcEnd, PagingOptions paging) {
            var search = new ExceptionlessQuery()
                .WithDateRange(utcStart, utcEnd, field ?? ElasticType.GetFieldName(e => e.Date))
                .WithIndexes(utcStart, utcEnd)
                .WithSystemFilter(systemFilter)
                .WithFilter(userFilter)
                .WithPaging(paging);

            search = !String.IsNullOrEmpty(sort) ? search.WithSort(sort) : search.WithSortDescending((PersistentEvent e) => e.Date);
            return FindAsync(search);
        }

        public Task<FindResults<PersistentEvent>> GetByReferenceIdAsync(string projectId, string referenceId) {
            var filter = Query<PersistentEvent>.Term(e => e.ReferenceId, referenceId);
            return FindAsync(new ExceptionlessQuery()
                .WithProjectId(projectId)
                .WithElasticFilter(filter)
                .WithSortDescending((PersistentEvent e) => e.Date)
                .WithLimit(10));
        }

        public async Task<PreviousAndNextEventIdResult> GetPreviousAndNextEventIdsAsync(PersistentEvent ev, IExceptionlessSystemFilterQuery systemFilter, string userFilter, DateTime? utcStart, DateTime? utcEnd) {
            var previous = await GetPreviousEventIdAsync(ev, systemFilter, userFilter, utcStart, utcEnd).AnyContext();
            var next = await GetNextEventIdAsync(ev, systemFilter, userFilter, utcStart, utcEnd).AnyContext();

            return new PreviousAndNextEventIdResult {
                Previous = previous,
                Next = next
            };
        }

        private async Task<string> GetPreviousEventIdAsync(PersistentEvent ev, IExceptionlessSystemFilterQuery systemFilter = null, string userFilter = null, DateTime? utcStart = null, DateTime? utcEnd = null) {
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
                userFilter = String.Concat(EventIndexType.Alias.StackId, ":", ev.StackId);

            var results = await FindAsync(new ExceptionlessQuery()
                .WithDateRange(utcStart, utcEventDate, (PersistentEvent e) => e.Date)
                .WithIndexes(utcStart, utcEventDate)
                .WithSortDescending((PersistentEvent e) => e.Date)
                .WithLimit(10)
                .IncludeFields((PersistentEvent e) => e.Id, e => e.Date)
                .WithSystemFilter(systemFilter)
                .WithElasticFilter(!Query<PersistentEvent>.Ids(ids => ids.Values(ev.Id)))
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

        private async Task<string> GetNextEventIdAsync(PersistentEvent ev, IExceptionlessSystemFilterQuery systemFilter = null, string userFilter = null, DateTime? utcStart = null, DateTime? utcEnd = null) {
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
                userFilter = String.Concat(EventIndexType.Alias.StackId, ":", ev.StackId);

            var results = await FindAsync(new ExceptionlessQuery()
                .WithDateRange(utcEventDate, utcEnd, (PersistentEvent e) => e.Date)
                .WithIndexes(utcStart, utcEventDate)
                .WithSortAscending((PersistentEvent e) => e.Date)
                .WithLimit(10)
                .IncludeFields((PersistentEvent e) => e.Id, e => e.Date)
                .WithSystemFilter(systemFilter)
                .WithElasticFilter(!Query<PersistentEvent>.Ids(ids => ids.Values(ev.Id)))
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
            if (String.IsNullOrEmpty(organizationId))
                throw new ArgumentNullException(nameof(organizationId));

            return FindAsync(new ExceptionlessQuery()
                .WithOrganizationId(organizationId)
                .WithPaging(paging)
                .WithSortDescending((PersistentEvent e) => e.Date)
                .WithSortDescending((PersistentEvent e) => e.Id)
                .WithExpiresIn(expiresIn));
        }

        public override Task<FindResults<PersistentEvent>> GetByProjectIdAsync(string projectId, PagingOptions paging = null) {
            return FindAsync(new ExceptionlessQuery()
                .WithProjectId(projectId)
                .WithPaging(paging)
                .WithSortDescending((PersistentEvent e) => e.Date)
                .WithSortDescending((PersistentEvent e) => e.Id));
        }

        public Task<CountResult> GetCountByProjectIdAsync(string projectId, bool includeDeleted = false) {
            return CountAsync(new ExceptionlessQuery().WithProjectId(projectId).IncludeDeleted(includeDeleted));
        }

        public Task<CountResult> GetCountByStackIdAsync(string stackId, bool includeDeleted = false) {
            return CountAsync(new ExceptionlessQuery().WithStackId(stackId).IncludeDeleted(includeDeleted));
        }
    }
}
