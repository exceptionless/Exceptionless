using Exceptionless.Core.Models;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Repositories;

public interface IStackRepository : IRepositoryOwnedByOrganizationAndProject<Stack>
{
    Task<Stack?> GetStackBySignatureHashAsync(string projectId, string signatureHash);
    Task<FindResults<Stack>> GetIdsByQueryAsync(RepositoryQueryDescriptor<Stack> query, CommandOptionsDescriptor<Stack>? options = null);
    Task<FindResults<Stack>> GetExpiredSnoozedStatuses(DateTime utcNow, CommandOptionsDescriptor<Stack>? options = null);
    /// <summary>
    /// Marks the stack as regressed. When <paramref name="notificationPending"/> is true the regression came from an environment that
    /// does not notify and is reported by the first notifying occurrence.
    /// </summary>
    Task MarkAsRegressedAsync(Stack stack, bool notificationPending = false);

    /// <summary>
    /// Atomically clears a pending notification. Returns true only for the single caller that should send it.
    /// </summary>
    Task<bool> TryClaimNotificationAsync(Stack stack, PendingStackNotification kind);

    /// <summary>
    /// Records that a notification is being sent right away instead of through a pending claim. Returns false if the notification
    /// was already sent for the stack's current cycle.
    /// </summary>
    Task<bool> TryReserveNotificationAsync(Stack stack, PendingStackNotification kind);

    /// <summary>
    /// Updates only the tags of the stack.
    /// </summary>
    Task UpdateTagsAsync(Stack stack);
    Task<bool> IncrementEventCounterAsync(string organizationId, string projectId, string stackId, DateTime minOccurrenceDateUtc, DateTime maxOccurrenceDateUtc, int count, bool sendNotifications = true);
    Task<bool> SetEventCounterAsync(string stackId, DateTime firstOccurrenceUtc, DateTime lastOccurrenceUtc, long totalOccurrences, bool sendNotifications = true);
    Task<FindResults<Stack>> GetStacksForCleanupAsync(string organizationId, DateTime cutoff);
    Task<FindResults<Stack>> GetSoftDeleted();
    Task<long> SoftDeleteByProjectIdAsync(string organizationId, string projectId);
}
