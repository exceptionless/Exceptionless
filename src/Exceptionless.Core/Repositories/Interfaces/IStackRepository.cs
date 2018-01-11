using System;
using System.Threading.Tasks;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Queries;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Repositories {
    public interface IStackRepository : IRepositoryOwnedByOrganizationAndProject<Stack> {
        Task<Stack> GetStackBySignatureHashAsync(string projectId, string signatureHash);
        Task<FindResults<Stack>> GetByFilterAsync(ExceptionlessSystemFilter systemFilter, string userFilter, string sort, string field, DateTime utcStart, DateTime utcEnd, CommandOptionsDescriptor<Stack> options = null);

        Task MarkAsRegressedAsync(string stackId);
        Task<bool> IncrementEventCounterAsync(string organizationId, string projectId, string stackId, DateTime minOccurrenceDateUtc, DateTime maxOccurrenceDateUtc, int count, bool sendNotifications = true);
    }
}