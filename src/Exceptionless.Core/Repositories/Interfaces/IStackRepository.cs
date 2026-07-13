using Exceptionless.Core.Models;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;
using Exceptionless.Core.Models.Ingestion;

namespace Exceptionless.Core.Repositories;

public interface IStackRepository : IRepositoryOwnedByOrganizationAndProject<Stack>
{
    Task<Stack?> GetStackBySignatureHashAsync(string projectId, string signatureHash);
    Task<IReadOnlyDictionary<string, StackRoute>> GetStackRoutesBySignatureHashAsync(string projectId, IReadOnlyCollection<string> signatureHashes);
    Task<FindResults<Stack>> GetIdsByQueryAsync(RepositoryQueryDescriptor<Stack> query, CommandOptionsDescriptor<Stack>? options = null);
    Task<FindResults<Stack>> GetExpiredSnoozedStatuses(DateTime utcNow, CommandOptionsDescriptor<Stack>? options = null);
    Task MarkAsRegressedAsync(string stackId);
    Task<bool> IncrementEventCounterAsync(string organizationId, string projectId, string stackId, DateTime minOccurrenceDateUtc, DateTime maxOccurrenceDateUtc, int count, bool sendNotifications = true);
    Task ApplyIngestionStackUsageAsync(string organizationId, string projectId, string stackId, DateTime minOccurrenceDateUtc, DateTime maxOccurrenceDateUtc, int count, long settlementSequence, bool sendNotifications = true);
    Task<bool> SetEventCounterAsync(string stackId, DateTime firstOccurrenceUtc, DateTime lastOccurrenceUtc, long totalOccurrences, bool sendNotifications = true);
    Task<FindResults<Stack>> GetStacksForCleanupAsync(string organizationId, DateTime cutoff);
    Task<FindResults<Stack>> GetSoftDeleted();
    Task<long> SoftDeleteByProjectIdAsync(string organizationId, string projectId);
}
