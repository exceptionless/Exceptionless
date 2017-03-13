using System;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Queries;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Repositories {
    public interface IEventRepository : IRepositoryOwnedByOrganizationAndProject<PersistentEvent> {
        Task<FindResults<PersistentEvent>> GetByReferenceIdAsync(string projectId, string referenceId);
        Task<FindResults<PersistentEvent>> GetByFilterAsync(ExceptionlessSystemFilter systemFilter, string userFilter, string sort, string field, DateTime utcStart, DateTime utcEnd, CommandOptionsDescriptor<PersistentEvent> options = null);
        Task<PreviousAndNextEventIdResult> GetPreviousAndNextEventIdsAsync(PersistentEvent ev, ExceptionlessSystemFilter systemFilter = null, string userFilter = null, DateTime? utcStart = null, DateTime? utcEnd = null);

        Task<FindResults<PersistentEvent>> GetOpenSessionsAsync(DateTime createdBeforeUtc, CommandOptionsDescriptor<PersistentEvent> options = null);
        Task<bool> UpdateSessionStartLastActivityAsync(string id, DateTime lastActivityUtc, bool isSessionEnd = false, bool hasError = false, bool sendNotifications = true);

        Task<long> UpdateFixedByStackAsync(string organizationId, string projectId, string stackId, bool isFixed, bool sendNotifications = true);
        Task<long> UpdateHiddenByStackAsync(string organizationId, string projectId, string stackId, bool isHidden, bool sendNotifications = true);
        Task<long> RemoveAllByDateAsync(string organizationId, DateTime utcCutoffDate);
        Task<long> RemoveAllByStackIdAsync(string organizationId, string projectId, string stackId);
        Task<long> HideAllByClientIpAndDateAsync(string organizationId, string clientIp, DateTime utcStart, DateTime utcEnd);

        Task<CountResult> GetCountByProjectIdAsync(string projectId, bool includeDeleted = false);
        Task<CountResult> GetCountByStackIdAsync(string stackId, bool includeDeleted = false);
    }

    public static class EventRepositoryExtensions {
        public static async Task<PreviousAndNextEventIdResult> GetPreviousAndNextEventIdsAsync(this IEventRepository repository, string id, ExceptionlessSystemFilter systemFilter = null, string userFilter = null, DateTime? utcStart = null, DateTime? utcEnd = null) {
            var ev = await repository.GetByIdAsync(id, o => o.Cache()).AnyContext();
            return await repository.GetPreviousAndNextEventIdsAsync(ev, systemFilter, userFilter, utcStart, utcEnd).AnyContext();
        }
    }
}