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
        public EventRepository(ExceptionlessElasticConfiguration configuration, AppOptions options, IValidator<PersistentEvent> validator)
            : base(configuration.Events, validator, options) {
            DisableCache(); // NOTE: If cache is ever enabled, then fast paths for patching/deleting with scripts will be super slow!
            BatchNotifications = true;
            DefaultPipeline = "events-pipeline";

            AddDefaultExclude(e => e.Idx);
            // copy to fields
            AddDefaultExclude(EventIndex.Alias.IpAddress);
            AddDefaultExclude(EventIndex.Alias.OperatingSystem);
            AddDefaultExclude("error");

            AddPropertyRequiredForRemove(e => e.Date);
        }

        public Task<FindResults<PersistentEvent>> GetOpenSessionsAsync(DateTime createdBeforeUtc, CommandOptionsDescriptor<PersistentEvent> options = null) {
            var filter = Query<PersistentEvent>.Term(e => e.Type, Event.KnownTypes.Session) && !Query<PersistentEvent>.Exists(f => f.Field(e => e.Idx[Event.KnownDataKeys.SessionEnd + "-d"]));
            if (createdBeforeUtc.Ticks > 0)
                filter &= Query<PersistentEvent>.DateRange(r => r.Field(e => e.Date).LessThanOrEquals(createdBeforeUtc));

            return FindAsync(q => q.ElasticFilter(filter).SortDescending(e => e.Date), options);
        }

        public async Task<bool> UpdateSessionStartLastActivityAsync(string id, DateTime lastActivityUtc, bool isSessionEnd = false, bool hasError = false, bool sendNotifications = true) {
            var ev = await GetByIdAsync(id).AnyContext();
            if (!ev.UpdateSessionStart(lastActivityUtc, isSessionEnd))
                return false;

            await SaveAsync(ev, o => o.Notifications(sendNotifications)).AnyContext();
            return true;
        }

        public Task<long> RemoveAllAsync(string organizationId, string clientIpAddress, DateTime? utcStart, DateTime? utcEnd, CommandOptionsDescriptor<PersistentEvent> options = null) {
            if (String.IsNullOrEmpty(organizationId))
                throw new ArgumentNullException(nameof(organizationId));
            
            var query = new RepositoryQuery<PersistentEvent>().Organization(organizationId);
            if (utcStart.HasValue && utcEnd.HasValue)
                query = query.DateRange(utcStart, utcEnd, InferField(e => e.Date)).Index(utcStart, utcEnd);
            else if (utcEnd.HasValue)
                query = query.ElasticFilter(Query<PersistentEvent>.DateRange(r => r.Field(e => e.Date).LessThan(utcEnd)));
            else if (utcStart.HasValue)
                query = query.ElasticFilter(Query<PersistentEvent>.DateRange(r => r.Field(e => e.Date).GreaterThan(utcStart)));

            if (!String.IsNullOrEmpty(clientIpAddress))
                query = query.FieldEquals(EventIndex.Alias.IpAddress, clientIpAddress);

            return RemoveAllAsync(q => query, options);
        }

        public Task<FindResults<PersistentEvent>> GetByReferenceIdAsync(string projectId, string referenceId) {
            var filter = Query<PersistentEvent>.Term(e => e.ReferenceId, referenceId);
            return FindAsync(q => q.Project(projectId).ElasticFilter(filter).SortDescending(e => e.Date), o => o.PageLimit(10));
        }

        public async Task<PreviousAndNextEventIdResult> GetPreviousAndNextEventIdsAsync(PersistentEvent ev, AppFilter systemFilter, string userFilter, DateTime? utcStart, DateTime? utcEnd) {
            var previous = GetPreviousEventIdAsync(ev, systemFilter, userFilter, utcStart, utcEnd);
            var next = GetNextEventIdAsync(ev, systemFilter, userFilter, utcStart, utcEnd);
            await Task.WhenAll(previous, next).AnyContext();

            return new PreviousAndNextEventIdResult {
                Previous = previous.Result,
                Next = next.Result
            };
        }

        private async Task<string> GetPreviousEventIdAsync(PersistentEvent ev, AppFilter systemFilter = null, string userFilter = null, DateTime? utcStart = null, DateTime? utcEnd = null) {
            if (ev == null)
                return null;

            var retentionDate = _options.MaximumRetentionDays > 0 ? SystemClock.UtcNow.Date.SubtractDays(_options.MaximumRetentionDays) : DateTime.MinValue;
            if (!utcStart.HasValue || utcStart.Value.IsBefore(retentionDate))
                utcStart = retentionDate;

            if (!utcEnd.HasValue || utcEnd.Value.IsAfter(ev.Date.UtcDateTime))
                utcEnd = ev.Date.UtcDateTime;

            var utcEventDate = ev.Date.UtcDateTime;
            // utcEnd is before the current event date.
            if (utcStart > utcEventDate || utcEnd < utcEventDate)
                return null;

            if (String.IsNullOrEmpty(userFilter))
                userFilter = String.Concat(EventIndex.Alias.StackId, ":", ev.StackId);

            var results = await FindAsync(q => q
                .DateRange(utcStart, utcEventDate, (PersistentEvent e) => e.Date)
                .Index(utcStart, utcEventDate)
                .SortDescending(e => e.Date)
                .Include(e => e.Id, e => e.Date)
                .AppFilter(systemFilter)
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

        private async Task<string> GetNextEventIdAsync(PersistentEvent ev, AppFilter systemFilter = null, string userFilter = null, DateTime? utcStart = null, DateTime? utcEnd = null) {
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
                userFilter = String.Concat(EventIndex.Alias.StackId, ":", ev.StackId);

            var results = await FindAsync(q => q
                .DateRange(utcEventDate, utcEnd, (PersistentEvent e) => e.Date)
                .Index(utcEventDate, utcEnd)
                .SortAscending(e => e.Date)
                .Include(e => e.Id, e => e.Date)
                .AppFilter(systemFilter)
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
            return CountAsync(q => q.Project(projectId));
        }
        
        public Task<long> RemoveAllByStackIdAsync(string organizationId, string projectId, string stackId) {
            if (String.IsNullOrEmpty(organizationId))
                throw new ArgumentNullException(nameof(organizationId));

            if (String.IsNullOrEmpty(projectId))
                throw new ArgumentNullException(nameof(projectId));
            
            return RemoveAllAsync(q => q.Organization(organizationId).Project(projectId).Stack(stackId));
        }
    }
}
