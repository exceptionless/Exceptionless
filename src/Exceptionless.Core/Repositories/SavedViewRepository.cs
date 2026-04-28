using Elastic.Clients.Elasticsearch;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Repositories;

public class SavedViewRepository : RepositoryOwnedByOrganization<SavedView>, ISavedViewRepository
{
    public SavedViewRepository(ExceptionlessElasticConfiguration configuration, AppOptions options)
        : base(configuration.SavedViews, null!, options)
    {
    }

    public Task<FindResults<SavedView>> GetByViewAsync(string organizationId, string viewType, CommandOptionsDescriptor<SavedView>? options = null)
    {
        return FindAsync(q => q
            .Organization(organizationId)
            .FieldEquals(e => e.ViewType, viewType)
            .SortAscending((Field)"name.keyword"), options);
    }

    public Task<FindResults<SavedView>> GetByViewForUserAsync(string organizationId, string viewType, string userId, CommandOptionsDescriptor<SavedView>? options = null)
    {
        return FindAsync(q => q
            .Organization(organizationId)
            .FieldEquals(e => e.ViewType, viewType)
            .SortAscending((Field)"name.keyword")
            .FieldOr(g => g
                .FieldEmpty(e => e.UserId!)
                .FieldEquals(e => e.UserId!, userId)), options);
    }

    public Task<FindResults<SavedView>> GetByOrganizationForUserAsync(string organizationId, string userId, CommandOptionsDescriptor<SavedView>? options = null)
    {
        return FindAsync(q => q
            .Organization(organizationId)
            .SortAscending((Field)"name.keyword")
            .FieldOr(g => g
                .FieldEmpty(e => e.UserId!)
                .FieldEquals(e => e.UserId!, userId)), options);
    }

    public async Task<long> RemovePrivateByUserIdAsync(string organizationId, string userId)
    {
        var results = await FindAsync(q => q
            .Organization(organizationId)
            .FieldEquals(e => e.UserId!, userId), o => o.PageLimit(1000));

        if (results.Total is 0)
            return 0;

        await RemoveAsync(results.Documents);
        return results.Total;
    }

    public async Task<long> CountByOrganizationIdAsync(string organizationId)
    {
        return await CountAsync(q => q.Organization(organizationId));
    }
}
