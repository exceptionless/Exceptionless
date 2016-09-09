using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Queries;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Repositories {
    public interface IEventRepository : IRepositoryOwnedByOrganizationAndProjectAndStack<PersistentEvent> {
        Task<IFindResults<PersistentEvent>> GetMostRecentAsync(string projectId, DateTime utcStart, DateTime utcEnd, PagingOptions paging, bool includeHidden = false, bool includeFixed = false, bool includeNotFound = true);
        Task<IFindResults<PersistentEvent>> GetByStackIdOccurrenceDateAsync(string stackId, DateTime utcStart, DateTime utcEnd, PagingOptions paging);
        Task<IFindResults<PersistentEvent>> GetByReferenceIdAsync(string projectId, string referenceId);
        Task<IFindResults<PersistentEvent>> GetByFilterAsync(IExceptionlessSystemFilterQuery systemFilter, string userFilter, SortingOptions sorting, string field, DateTime utcStart, DateTime utcEnd, PagingOptions paging);
        Task<PreviousAndNextEventIdResult> GetPreviousAndNextEventIdsAsync(PersistentEvent ev, IExceptionlessSystemFilterQuery systemFilter = null, string userFilter = null, DateTime? utcStart = null, DateTime? utcEnd = null);

        Task<IFindResults<PersistentEvent>> GetOpenSessionsAsync(DateTime createdBeforeUtc, PagingOptions paging = null);
        Task<bool> UpdateSessionStartLastActivityAsync(string id, DateTime lastActivityUtc, bool isSessionEnd = false, bool hasError = false, bool sendNotifications = true);
        
        Task UpdateFixedByStackAsync(string organizationId, string stackId, bool isFixed, bool sendNotifications = true);
        Task UpdateHiddenByStackAsync(string organizationId, string stackId, bool isHidden, bool sendNotifications = true);
        Task RemoveOldestEventsAsync(string stackId, int maxEventsPerStack);
        Task RemoveAllByDateAsync(string organizationId, DateTime utcCutoffDate);
        Task HideAllByClientIpAndDateAsync(string organizationId, string clientIp, DateTime utcStart, DateTime utcEnd);

        Task<IFindResults<PersistentEvent>> GetByOrganizationIdsAsync(ICollection<string> organizationIds, string filter = null, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null);
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