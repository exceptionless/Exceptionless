using System;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Queries;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Repositories {
    public interface IEventRepository : IRepositoryOwnedByOrganizationAndProjectAndStack<PersistentEvent> {
        Task<IFindResults<PersistentEvent>> GetByReferenceIdAsync(string projectId, string referenceId);
        Task<IFindResults<PersistentEvent>> GetByFilterAsync(IExceptionlessSystemFilterQuery systemFilter, string userFilter, SortingOptions sorting, string field, DateTime utcStart, DateTime utcEnd, PagingOptions paging);
        Task<PreviousAndNextEventIdResult> GetPreviousAndNextEventIdsAsync(PersistentEvent ev, IExceptionlessSystemFilterQuery systemFilter = null, string userFilter = null, DateTime? utcStart = null, DateTime? utcEnd = null);

        Task<IFindResults<PersistentEvent>> GetOpenSessionsAsync(DateTime createdBeforeUtc, PagingOptions paging = null);
        Task<bool> UpdateSessionStartLastActivityAsync(string id, DateTime lastActivityUtc, bool isSessionEnd = false, bool hasError = false, bool sendNotifications = true);

        Task<long> UpdateFixedByStackAsync(string organizationId, string projectId, string stackId, bool isFixed, bool sendNotifications = true);
        Task<long> UpdateHiddenByStackAsync(string organizationId, string projectId, string stackId, bool isHidden, bool sendNotifications = true);
        Task<long> RemoveAllByDateAsync(string organizationId, DateTime utcCutoffDate);
        Task<long> HideAllByClientIpAndDateAsync(string organizationId, string clientIp, DateTime utcStart, DateTime utcEnd);

        Task<CountResult> GetCountByProjectIdAsync(string projectId);
        Task<CountResult> GetCountByStackIdAsync(string stackId);
    }

    public static class EventRepositoryExtensions {
        public static async Task<PreviousAndNextEventIdResult> GetPreviousAndNextEventIdsAsync(this IEventRepository repository, string id, IExceptionlessSystemFilterQuery systemFilter = null, string userFilter = null, DateTime? utcStart = null, DateTime? utcEnd = null) {
            var ev = await repository.GetByIdAsync(id, true).AnyContext();
            return await repository.GetPreviousAndNextEventIdsAsync(ev, systemFilter, userFilter, utcStart, utcEnd).AnyContext();
        }
    }
}