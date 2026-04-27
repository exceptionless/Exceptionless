using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;
using Nest;

namespace Exceptionless.Core.Repositories;

public class SavedViewRepository : RepositoryOwnedByOrganization<SavedView>, ISavedViewRepository
{
    public SavedViewRepository(ExceptionlessElasticConfiguration configuration, AppOptions options)
        : base(configuration.SavedViews, null!, options)
    {
    }

    public Task<FindResults<SavedView>> GetByViewAsync(string organizationId, string view, CommandOptionsDescriptor<SavedView>? options = null)
    {
        return FindAsync(q => q
            .Organization(organizationId)
            .FieldEquals(e => e.View, view)
            .SortAscending(e => e.Name.Suffix("keyword")), options);
    }

    public Task<FindResults<SavedView>> GetByViewForUserAsync(string organizationId, string view, string userId, CommandOptionsDescriptor<SavedView>? options = null)
    {
        var userFilter = !Query<SavedView>.Exists(e => e.Field(f => f.UserId))
                         || Query<SavedView>.Term(e => e.UserId, userId);

        return FindAsync(q => q
            .Organization(organizationId)
            .FieldEquals(e => e.View, view)
            .ElasticFilter(userFilter)
            .SortAscending(e => e.Name.Suffix("keyword")), options);
    }

    public Task<FindResults<SavedView>> GetByOrganizationForUserAsync(string organizationId, string userId, CommandOptionsDescriptor<SavedView>? options = null)
    {
        var userFilter = !Query<SavedView>.Exists(e => e.Field(f => f.UserId))
                         || Query<SavedView>.Term(e => e.UserId, userId);

        return FindAsync(q => q
            .Organization(organizationId)
            .ElasticFilter(userFilter)
            .SortAscending(e => e.Name.Suffix("keyword")), options);
    }

    public async Task<long> RemovePrivateByUserIdAsync(string organizationId, string userId)
    {
        var results = await FindAsync(q => q
            .Organization(organizationId)
            .FieldEquals(e => e.UserId, userId), o => o.PageLimit(1000));

        if (results.Total == 0)
            return 0;

        await RemoveAsync(results.Documents);
        return results.Total;
    }

    public async Task<long> CountByOrganizationIdAsync(string organizationId)
    {
        return await CountAsync(q => q.Organization(organizationId));
    }
}
