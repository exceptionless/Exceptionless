using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Validation;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Repositories;

public class OAuthApplicationRepository : RepositoryBase<OAuthApplication>, IOAuthApplicationRepository
{
    public OAuthApplicationRepository(ExceptionlessElasticConfiguration configuration, MiniValidationValidator validator, AppOptions options)
        : base(configuration.OAuthApplications, validator, options)
    {
        DefaultConsistency = Consistency.Immediate;
    }

    public async Task<OAuthApplication?> GetByClientIdAsync(string clientId, CommandOptionsDescriptor<OAuthApplication>? options = null)
    {
        var hit = await FindOneAsync(q => q.FieldEquals(a => a.ClientId, clientId.Trim()), options);
        return hit?.Document;
    }
}
