using Exceptionless.Core.Extensions;
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

    public Task<long> AddOrganizationIdsAsync(string clientId, IReadOnlyCollection<string> organizationIds, CommandOptionsDescriptor<OAuthApplication>? options = null)
    {
        if (organizationIds.Count == 0)
            return Task.FromResult(0L);

        const string script = """
            if (ctx._source.organization_ids == null) {
              ctx._source.organization_ids = [];
            }
            for (organizationId in params.organizationIds) {
              if (!ctx._source.organization_ids.contains(organizationId)) {
                ctx._source.organization_ids.add(organizationId);
              }
            }
            """;
        var patch = new ScriptPatch(script.TrimScript())
        {
            Params = new Dictionary<string, object>
            {
                ["organizationIds"] = organizationIds
                    .Where(id => !String.IsNullOrWhiteSpace(id))
                    .Select(id => id.Trim())
                    .Distinct(StringComparer.Ordinal)
                    .ToArray()
            }
        };

        return PatchAllAsync(query => query.FieldEquals(application => application.ClientId, clientId.Trim()), patch, options);
    }

    public async Task<OAuthApplication?> GetByClientIdAsync(string clientId, CommandOptionsDescriptor<OAuthApplication>? options = null)
    {
        var hit = await FindOneAsync(q => q.FieldEquals(a => a.ClientId, clientId.Trim()), options);
        return hit?.Document;
    }

    public Task<FindResults<OAuthApplication>> GetByCriteriaAsync(string? criteria, IReadOnlyCollection<string>? organizationIds, CommandOptionsDescriptor<OAuthApplication>? options = null)
    {
        var query = new RepositoryQuery<OAuthApplication>();

        if (!String.IsNullOrWhiteSpace(criteria))
        {
            string normalizedCriteria = criteria.Trim();
            query.FieldOr(group => group
                .FieldContains(application => application.Name, normalizedCriteria)
                .FieldEquals(application => application.ClientId, normalizedCriteria));
        }

        if (organizationIds is { Count: > 0 })
            query.FieldEquals(application => application.OrganizationIds, organizationIds);

        query.SortAscending(application => application.Name);
        return FindAsync(q => query, options);
    }
}
