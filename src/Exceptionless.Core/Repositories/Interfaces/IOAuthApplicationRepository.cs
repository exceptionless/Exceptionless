using Exceptionless.Core.Models;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Repositories;

public interface IOAuthApplicationRepository : ISearchableRepository<OAuthApplication>
{
    Task<OAuthApplication?> GetByClientIdAsync(string clientId, CommandOptionsDescriptor<OAuthApplication>? options = null);
}
