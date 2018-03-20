using System;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Repositories.Queries;
using Exceptionless.DateTimeExtensions;
using FluentValidation;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;
using Foundatio.Utility;
using Nest;

namespace Exceptionless.Core.Repositories {
    public class EventRepository : RepositoryOwnedByOrganizationAndProject<PersistentEvent>, IEventRepository {
        public EventRepository(ExceptionlessElasticConfiguration configuration, IValidator<PersistentEvent> validator)
            : base(configuration.Events.Event, validator) {
            DisableCache();
            BatchNotifications = true;
            DefaultExcludes.Add(ElasticType.GetFieldName(e => e.Idx));
            // copy to fields
            DefaultExcludes.Add(EventIndexType.Alias.IpAddress);
            DefaultExcludes.Add(EventIndexType.Alias.OperatingSystem);
            DefaultExcludes.Add("error");

            FieldsRequiredForRemove.Add(ElasticType.GetFieldName(e => e.Date));
        }

        // TODO: We need to index and search by the created time.
        public Task<FindResults<PersistentEvent>> GetOpenSessionsAsync(DateTime createdBeforeUtc, CommandOptionsDescriptor<PersistentEvent> options = null) {
            var filter = Query<PersistentEvent>.Term(e => e.Type, Event.KnownTypes.Session) && !Query<PersistentEvent>.Exists(f => f.Field(e => e.Idx[Event.KnownDataKeys.SessionEnd + "-d"]));
            if (createdBeforeUtc.Ticks > 0)
                filter &= Query<PersistentEvent>.DateRange(r => r.Field(e => e.Date).LessThanOrEquals(createdBeforeUtc));

            return FindAsync(q => q.ElasticFilter(filter).SortDescending(e => e.Date), options);
        }

        public async Task<bool> UpdateSessionStartLastActivityAsync(string id, DateTime lastActivityUtc, bool isSessionEnd = false, bool hasError = false, bool sendNotifications = true) {
            var ev = await GetByIdAsync(id).AnyContext();
            if (!ev.UpdateSessionStart(lastActivityUtc, isSessionEnd, hasError))
                return false;

            await this.SaveAsync(ev, o => o.Notifications(sendNotifications)).AnyContext();
            return true;
        }

        public Task<long> UpdateFixedByStackAsync(string organizationId, string projectId, string stackId, bool isFixed, bool sendNotifications = true) {
            if (String.IsNullOrEmpty(stackId))
                throw new ArgumentNullException(nameof(stackId));

            // TODO: Update this to use the update by query syntax that's coming in 2.3.
            return PatchAllAsync(q => q.Organization(organizationId).Project(projectId).Stack(stackId).FieldEquals(e => e.IsFixed, !isFixed), new PartialPatch(new { is_fixed = isFixed, updated_utc = SystemClock.UtcNow }));
        }

        public Task<long> UpdateHiddenByStackAsync(string organizationId, string projectId, string stackId, bool isHidden, bool sendNotifications = true) {
            if (String.IsNullOrEmpty(stackId))
                throw new ArgumentNullException(nameof(stackId));

            // TODO: Update this to use the update by query syntax that's coming in 2.3.
            return PatchAllAsync(q => q.Organization(organizationId).Project(projectId).Stack(stackId).FieldEquals(e => e.IsHidden, !isHidden), new PartialPatch(new { is_hidden = isHidden, updated_utc = SystemClock.UtcNow }));
        }

        public Task<long> RemoveAllByDateAsync(string organizationId, DateTime utcCutoffDate) {
            var filter = Query<PersistentEvent>.DateRange(r => r.Field(e => e.Date).LessThan(utcCutoffDate));
            return RemoveAllAsync(q => q.Organization(organizationId).ElasticFilter(filter).SoftDeleteMode(SoftDeleteQueryMode.All));
        }

        public override Task<long> RemoveAllByOrganizationIdAsync(string organizationId) {
            if (String.IsNullOrEmpty(organizationId))
                throw new ArgumentNullException(nameof(organizationId));

            return PatchAllAsync(q => q.Organization(organizationId), new PartialPatch(new { is_deleted = true, updated_utc = SystemClock.UtcNow }));
        }

        public override Task<long> RemoveAllByProjectIdAsync(string organizationId, string projectId) {
            if (String.IsNullOrEmpty(organizationId))
                throw new ArgumentNullException(nameof(organizationId));

            if (String.IsNullOrEmpty(projectId))
                throw new ArgumentNullException(nameof(projectId));

            return PatchAllAsync(q => q.Organization(organizationId).Project(projectId), new PartialPatch(new { is_deleted = true, updated_utc = SystemClock.UtcNow }));
        }

        public Task<long> RemoveAllByStackIdAsync(string organizationId, string projectId, string stackId) {
            return PatchAllAsync(q => q.Organization(organizationId).Project(projectId).Stack(stackId), new PartialPatch(new { is_deleted = true, updated_utc = SystemClock.UtcNow }), o => o.Consistency(Consistency.Wait));
        }

        public Task<long> HideAllByClientIpAndDateAsync(string organizationId, string clientIp, DateTime utcStart, DateTime utcEnd) {
            return PatchAllAsync(q => q
                    .Organization(organizationId)
                    .ElasticFilter(Query<PersistentEvent>.Term(EventIndexType.Alias.IpAddress, clientIp))
                    .DateRange(utcStart, utcEnd, (PersistentEvent e) => e.Date)
                    .Index(utcStart, utcEnd)
                , new PartialPatch(new { is_hidden = true, updated_utc = SystemClock.UtcNow }));
        }

        public Task<FindResults<PersistentEvent>> GetByFilterAsync(ExceptionlessSystemFilter systemFilter, string userFilter, string sort, string field, DateTime utcStart, DateTime utcEnd, CommandOptionsDescriptor<PersistentEvent> options = null) {
            IRepositoryQuery<PersistentEvent> query = new RepositoryQuery<PersistentEvent>()
                .DateRange(utcStart, utcEnd, field ?? ElasticType.GetFieldName(e => e.Date))
                .Index(utcStart, utcEnd)
                .SystemFilter(systemFilter)
                .FilterExpression(userFilter);

            query = !String.IsNullOrEmpty(sort) ? query.SortExpression(sort) : query.SortDescending(e => e.Date);
            return FindAsync(q => query, options);
        }

        public Task<FindResults<PersistentEvent>> GetByReferenceIdAsync(string projectId, string referenceId) {
            var filter = Query<PersistentEvent>.Term(e => e.ReferenceId, referenceId);
            return FindAsync(q => q.Project(projectId).ElasticFilter(filter).SortDescending(e => e.Date), o => o.PageLimit(10));
        }

        public async Task<PreviousAndNextEventIdResult> GetPreviousAndNextEventIdsAsync(PersistentEvent ev, ExceptionlessSystemFilter systemFilter, string userFilter, DateTime? utcStart, DateTime? utcEnd) {
            var previous = GetPreviousEventIdAsync(ev, systemFilter, userFilter, utcStart, utcEnd);
            var next = GetNextEventIdAsync(ev, systemFilter, userFilter, utcStart, utcEnd);
            await Task.WhenAll(previous, next).AnyContext();

            return new PreviousAndNextEventIdResult {
                Previous = previous.Result,
                Next = next.Result
            };
        }

        private async Task<string> GetPreviousEventIdAsync(PersistentEvent ev, ExceptionlessSystemFilter systemFilter = null, string userFilter = null, DateTime? utcStart = null, DateTime? utcEnd = null) {
            if (ev == null)
                return null;

            var retentionDate = Settings.Current.MaximumRetentionDays > 0 ? SystemClock.UtcNow.Date.SubtractDays(Settings.Current.MaximumRetentionDays) : DateTime.MinValue;
            if (!utcStart.HasValue || utcStart.Value.IsBefore(retentionDate))
                utcStart = retentionDate;

            if (!utcEnd.HasValue || utcEnd.Value.IsAfter(ev.Date.UtcDateTime))
                utcEnd = ev.Date.UtcDateTime;

            var utcEventDate = ev.Date.UtcDateTime;
            // utcEnd is before the current event date.
            if (utcStart > utcEventDate || utcEnd < utcEventDate)
                return null;

            if (String.IsNullOrEmpty(userFilter))
                userFilter = String.Concat(EventIndexType.Alias.StackId, ":", ev.StackId);

            var results = await FindAsync(q => q
                .DateRange(utcStart, utcEventDate, (PersistentEvent e) => e.Date)
                .Index(utcStart, utcEventDate)
                .SortDescending(e => e.Date)
                .Include(e => e.Id, e => e.Date)
                .SystemFilter(systemFilter)
                .ElasticFilter(!Query<PersistentEvent>.Ids(ids => ids.Values(ev.Id)))
                .FilterExpression(userFilter), o => o.PageLimit(10)).AnyContext();

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

            int index = unionResults.FindIndex(t => t.Id == ev.Id);
            return index == 0 ? null : unionResults[index - 1].Id;
        }

        private async Task<string> GetNextEventIdAsync(PersistentEvent ev, ExceptionlessSystemFilter systemFilter = null, string userFilter = null, DateTime? utcStart = null, DateTime? utcEnd = null) {
            if (ev == null)
                return null;

            if (!utcStart.HasValue || utcStart.Value.IsBefore(ev.Date.UtcDateTime))
                utcStart = ev.Date.UtcDateTime;

            if (!utcEnd.HasValue || utcEnd.Value.IsAfter(SystemClock.UtcNow))
                utcEnd = SystemClock.UtcNow;

            var utcEventDate = ev.Date.UtcDateTime;
            // utcEnd is before the current event date.
            if (utcStart > utcEventDate || utcEnd < utcEventDate)
                return null;

            if (String.IsNullOrEmpty(userFilter))
                userFilter = String.Concat(EventIndexType.Alias.StackId, ":", ev.StackId);

            var results = await FindAsync(q => q
                .DateRange(utcEventDate, utcEnd, (PersistentEvent e) => e.Date)
                .Index(utcEventDate, utcEnd)
                .SortAscending(e => e.Date)
                .Include(e => e.Id, e => e.Date)
                .SystemFilter(systemFilter)
                .ElasticFilter(!Query<PersistentEvent>.Ids(ids => ids.Values(ev.Id)))
                .FilterExpression(userFilter), o => o.PageLimit(10)).AnyContext();

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

            int index = unionResults.FindIndex(t => t.Id == ev.Id);
            return index == unionResults.Count - 1 ? null : unionResults[index + 1].Id;
        }

        public override Task<FindResults<PersistentEvent>> GetByOrganizationIdAsync(string organizationId, CommandOptionsDescriptor<PersistentEvent> options = null) {
            if (String.IsNullOrEmpty(organizationId))
                throw new ArgumentNullException(nameof(organizationId));

            return FindAsync(q => q.Organization(organizationId).SortDescending(e => e.Date).SortDescending(e => e.Id), options);
        }

        public override Task<FindResults<PersistentEvent>> GetByProjectIdAsync(string projectId, CommandOptionsDescriptor<PersistentEvent> options = null) {
            return FindAsync(q => q.Project(projectId).SortDescending(e => e.Date).SortDescending(e => e.Id), options);
        }

        public Task<CountResult> GetCountByProjectIdAsync(string projectId, bool includeDeleted = false) {
            return CountAsync(q => q.Project(projectId).SoftDeleteMode(includeDeleted ? SoftDeleteQueryMode.All : SoftDeleteQueryMode.ActiveOnly));
        }

        public Task<CountResult> GetCountByStackIdAsync(string stackId, bool includeDeleted = false) {
            return CountAsync(q => q.Stack(stackId).SoftDeleteMode(includeDeleted ? SoftDeleteQueryMode.All : SoftDeleteQueryMode.ActiveOnly));
        }
    }
}
