using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Validation;
using Foundatio.Repositories;

namespace Exceptionless.Core.Repositories;

public sealed class TrialClaimRepository : RepositoryBase<TrialClaim>, ITrialClaimRepository
{
    public TrialClaimRepository(ExceptionlessElasticConfiguration configuration, MiniValidationValidator validator, AppOptions options)
        : base(configuration.TrialClaims, validator, options)
    {
        DefaultConsistency = Consistency.Immediate;
    }
}
