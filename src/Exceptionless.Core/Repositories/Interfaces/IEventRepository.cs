using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Queries;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Repositories;

public interface IEventRepository : IRepositoryOwnedByOrganizationAndProject<PersistentEvent>
{
    Task<FindResults<PersistentEvent>> GetByReferenceIdAsync(string projectId, string referenceId);
    Task<PreviousAndNextEventIdResult> GetPreviousAndNextEventIdsAsync(PersistentEvent ev, AppFilter? systemFilter = null, DateTime? utcStart = null, DateTime? utcEnd = null);
    Task<FindResults<PersistentEvent>> GetOpenSessionsAsync(DateTime createdBeforeUtc, CommandOptionsDescriptor<PersistentEvent>? options = null);
    Task<bool> UpdateSessionStartLastActivityAsync(string id, DateTime lastActivityUtc, bool isSessionEnd = false, bool hasError = false, bool sendNotifications = true);
    Task<long> RemoveAllByOrganizationAndClientIpAsync(string organizationId, string? clientIpAddress, DateTime? startUtc, DateTime? endUtc, CommandOptionsDescriptor<PersistentEvent>? options = null);
    Task<long> RemoveAllByProjectAndClientIpAsync(string organizationId, string projectId, string clientIpAddress, DateTime? startUtc, DateTime? endUtc, CommandOptionsDescriptor<PersistentEvent>? options = null);
    Task<long> RemoveAllByStackIdsAsync(string[] stackIds);

    /// <summary>
    /// Pages the distinct stack ids of the events that match the query in ascending id order. The position is the last id of the
    /// previous page, so paging stays correct while events or stacks change between pages.
    /// </summary>
    Task<IdPage> GetStackIdPageAsync(IRepositoryQuery query, string? afterStackId, int size);

    /// <summary>
    /// Pages the ids of the events that match the query in the order of the query's sort. The cursor is the sort position of the last
    /// event of the previous page. Throws when the search answered only partly (failed shards or a timeout).
    /// </summary>
    Task<IdPage> GetEventIdPageAsync(IRepositoryQuery query, string? cursor, int size);

    /// <summary>Approximately how many distinct stacks the events matching the query belong to.</summary>
    Task<long> CountStacksAsync(IRepositoryQuery query);
}

/// <summary>A page of ids. <see cref="NextCursor"/> is null when there are no more pages.</summary>
public record IdPage(IReadOnlyList<string> Ids, string? NextCursor);

public static class EventRepositoryExtensions
{
    public static async Task<PreviousAndNextEventIdResult?> GetPreviousAndNextEventIdsAsync(this IEventRepository repository, string id, AppFilter? systemFilter = null, DateTime? utcStart = null, DateTime? utcEnd = null)
    {
        var ev = await repository.GetByIdAsync(id, o => o.Cache());
        if (ev is null)
            return null;

        return await repository.GetPreviousAndNextEventIdsAsync(ev, systemFilter, utcStart, utcEnd);
    }
}
