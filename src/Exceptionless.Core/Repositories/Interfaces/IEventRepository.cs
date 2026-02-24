using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Queries;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Repositories;

public interface IEventRepository : IRepositoryOwnedByOrganizationAndProject<PersistentEvent>
{
    Task<FindResults<PersistentEvent>> GetByReferenceIdAsync(string projectId, string referenceId);
    Task<PreviousAndNextEventIdResult> GetPreviousAndNextEventIdsAsync(PersistentEvent ev, AppFilter? systemFilter = null, DateTime? utcStart = null, DateTime? utcEnd = null);
    Task<Dictionary<string, StackEventStats>> GetEventStatsForStacksAsync(IReadOnlyCollection<string> stackIds);
    Task<FindResults<PersistentEvent>> GetOpenSessionsAsync(DateTime createdBeforeUtc, CommandOptionsDescriptor<PersistentEvent>? options = null);
    Task<bool> UpdateSessionStartLastActivityAsync(string id, DateTime lastActivityUtc, bool isSessionEnd = false, bool hasError = false, bool sendNotifications = true);
    Task<long> RemoveAllAsync(string organizationId, string? clientIpAddress, DateTime? utcStart, DateTime? utcEnd, CommandOptionsDescriptor<PersistentEvent>? options = null);
    Task<long> RemoveAllByStackIdsAsync(string[] stackIds);
}

public record StackEventStats(DateTime FirstOccurrence, DateTime LastOccurrence, long TotalOccurrences);

public static class EventRepositoryExtensions
{
    public static async Task<PreviousAndNextEventIdResult> GetPreviousAndNextEventIdsAsync(this IEventRepository repository, string id, AppFilter? systemFilter = null, DateTime? utcStart = null, DateTime? utcEnd = null)
    {
        var ev = await repository.GetByIdAsync(id, o => o.Cache());
        return await repository.GetPreviousAndNextEventIdsAsync(ev, systemFilter, utcStart, utcEnd);
    }
}
