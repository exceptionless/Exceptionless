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
        // Eligibility reads use the document ID, which is real-time and needs no search refresh.
    }

    public Task<long> RemoveCreatedBeforeAsync(DateTime utcCutoff)
        => RemoveAllAsync(q => q.DateRange(null, utcCutoff, InferField(c => c.CreatedUtc)));
}
