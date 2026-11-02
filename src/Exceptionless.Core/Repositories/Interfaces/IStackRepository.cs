using Exceptionless.Core.Models;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Repositories;

public interface IStackRepository : IRepositoryOwnedByOrganizationAndProject<Stack>
{
    Task<Stack?> GetStackBySignatureHashAsync(string projectId, string signatureHash);
    Task<FindResults<Stack>> GetIdsByQueryAsync(RepositoryQueryDescriptor<Stack> query, CommandOptionsDescriptor<Stack>? options = null);
    Task<FindResults<Stack>> GetExpiredSnoozedStatuses(DateTime utcNow, CommandOptionsDescriptor<Stack>? options = null);
    Task MarkAsRegressedAsync(string stackId);

    /// <summary>
    /// Atomically clears <see cref="Stack.IsNewNotificationPending"/>. Returns true only for the single caller that cleared it.
    /// </summary>
    Task<bool> TryClaimNewNotificationAsync(Stack stack);
    Task<bool> IncrementEventCounterAsync(string organizationId, string projectId, string stackId, DateTime minOccurrenceDateUtc, DateTime maxOccurrenceDateUtc, int count, bool sendNotifications = true);
    Task<bool> SetEventCounterAsync(string stackId, DateTime firstOccurrenceUtc, DateTime lastOccurrenceUtc, long totalOccurrences, bool sendNotifications = true);
    Task<FindResults<Stack>> GetStacksForCleanupAsync(string organizationId, DateTime cutoff);
    Task<FindResults<Stack>> GetSoftDeleted();
    Task<long> SoftDeleteByProjectIdAsync(string organizationId, string projectId);
}
