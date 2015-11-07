using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Results;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Repositories {
    public interface IEventRepository : IRepositoryOwnedByOrganizationAndProjectAndStack<PersistentEvent> {
        Task<FindResults<PersistentEvent>> GetMostRecentAsync(string projectId, DateTime utcStart, DateTime utcEnd, PagingOptions paging, bool includeHidden = false, bool includeFixed = false, bool includeNotFound = true);
        Task<FindResults<PersistentEvent>> GetByStackIdOccurrenceDateAsync(string stackId, DateTime utcStart, DateTime utcEnd, PagingOptions paging);
        Task<FindResults<PersistentEvent>> GetByReferenceIdAsync(string projectId, string referenceId);
        Task<FindResults<PersistentEvent>> GetByFilterAsync(string systemFilter, string userFilter, string sort, SortOrder sortOrder, string field, DateTime utcStart, DateTime utcEnd, PagingOptions paging);
        Task<PreviousAndNextEventIdResult> GetPreviousAndNextEventIdsAsync(PersistentEvent ev, string systemFilter = null, string userFilter = null, DateTime? utcStart = null, DateTime? utcEnd = null);
        Task UpdateFixedByStackAsync(string organizationId, string stackId, bool value);
        Task UpdateHiddenByStackAsync(string organizationId, string stackId, bool value);
        Task RemoveOldestEventsAsync(string stackId, int maxEventsPerStack);
        Task RemoveAllByDateAsync(string organizationId, DateTime utcCutoffDate);
        Task HideAllByClientIpAndDateAsync(string organizationId, string clientIp, DateTime utcStart, DateTime utcEnd);

        Task<FindResults<PersistentEvent>> GetByOrganizationIdsAsync(ICollection<string> organizationIds, string filter = null, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null);
        Task<long> GetCountByOrganizationIdAsync(string organizationId);
        Task<long> GetCountByProjectIdAsync(string projectId);
        Task<long> GetCountByStackIdAsync(string stackId);
    }

    public static class EventRepositoryExtensions {
        public static async Task<PreviousAndNextEventIdResult> GetPreviousAndNextEventIdsAsync(this IEventRepository repository, string id, string systemFilter = null, string userFilter = null, DateTime? utcStart = null, DateTime? utcEnd = null) {
            var ev = await repository.GetByIdAsync(id, true).AnyContext();
            return await repository.GetPreviousAndNextEventIdsAsync(ev, systemFilter, userFilter, utcStart, utcEnd).AnyContext();
        }
    }
}