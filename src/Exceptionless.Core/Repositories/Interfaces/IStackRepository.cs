using Exceptionless.Core.Models;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Repositories;

public interface IStackRepository : IRepositoryOwnedByOrganizationAndProject<Stack>
{
    Task<Stack?> GetStackBySignatureHashAsync(string projectId, string signatureHash);
    Task<Stack?> GetCanonicalStackAsync(string stackId);
    Task<FindResults<Stack>> GetIdsByQueryAsync(RepositoryQueryDescriptor<Stack> query, CommandOptionsDescriptor<Stack>? options = null);
    Task<FindResults<Stack>> GetExpiredSnoozedStatuses(DateTime utcNow, CommandOptionsDescriptor<Stack>? options = null);
    Task MarkAsRegressedAsync(string stackId);
    Task<bool> IncrementEventCounterAsync(string organizationId, string projectId, string stackId, DateTime minOccurrenceDateUtc, DateTime maxOccurrenceDateUtc, int count, bool sendNotifications = true);
    Task<bool> SetEventCounterAsync(string stackId, DateTime firstOccurrenceUtc, DateTime lastOccurrenceUtc, long totalOccurrences, bool sendNotifications = true);
    Task<FindResults<Stack>> GetStacksForCleanupAsync(string organizationId, DateTime cutoff);
    Task<FindResults<Stack>> GetSoftDeleted();
    Task<FindResults<Stack>> GetRedirectedStacksNeedingReconciliationAsync();
    Task<long> SoftDeleteByProjectIdAsync(string organizationId, string projectId);
    Task<IReadOnlyCollection<string>> GetDuplicateSignaturesAsync(int maxResults = 10000);
    Task<bool> AddEventTagsAsync(string stackId, IEnumerable<string?> tags);
    Task<long> MarkOpenAsync(IEnumerable<string> stackIds);
    Task SetDuplicateStackRedirectAsync(Stack sourceStack, string targetStackId, bool isDeleted = false);
    Task MarkDuplicateStackReconciledAsync(Stack sourceStack);
    Task<bool> MergeDuplicateStackAsync(string targetStackId, Stack sourceStack);
}
