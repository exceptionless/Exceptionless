using Exceptionless.Core.Models;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Repositories;

public interface IOAuthApplicationRepository : ISearchableRepository<OAuthApplication>
{
    Task<long> AddOrganizationIdsAsync(string clientId, IReadOnlyCollection<string> organizationIds, CommandOptionsDescriptor<OAuthApplication>? options = null);
    Task<OAuthApplication?> GetByClientIdAsync(string clientId, CommandOptionsDescriptor<OAuthApplication>? options = null);
    Task<FindResults<OAuthApplication>> GetByCriteriaAsync(string? criteria, IReadOnlyCollection<string>? organizationIds, CommandOptionsDescriptor<OAuthApplication>? options = null);
}
