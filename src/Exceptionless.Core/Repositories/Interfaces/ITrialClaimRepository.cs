using Exceptionless.Core.Models;
using Foundatio.Repositories;

namespace Exceptionless.Core.Repositories;

public interface ITrialClaimRepository : IRepository<TrialClaim>
{
    Task<long> RemoveCreatedBeforeAsync(DateTime utcCutoff);
}
