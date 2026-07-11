using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Validation;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Repositories;

public class SavedViewRepository : RepositoryOwnedByOrganization<SavedView>, ISavedViewRepository
{
    public SavedViewRepository(ExceptionlessElasticConfiguration configuration, MiniValidationValidator validator, AppOptions options)
        : base(configuration.SavedViews, validator, options)
    {
    }

    public Task<FindResults<SavedView>> GetByViewAsync(string organizationId, string viewType, CommandOptionsDescriptor<SavedView>? options = null)
    {
        return FindAsync(q => q
            .Organization(organizationId)
            .FieldEquals(e => e.ViewType, viewType)
            .SortAscending(e => e.Name), options);
    }

    public Task<FindResults<SavedView>> GetByViewForUserAsync(string organizationId, string viewType, string userId, CommandOptionsDescriptor<SavedView>? options = null)
    {
        return FindAsync(q => q
            .Organization(organizationId)
            .FieldEquals(e => e.ViewType, viewType)
            .SortAscending(e => e.Name)
            .FieldOr(g => g
                .FieldEmpty(e => e.UserId!)
                .FieldEquals(e => e.UserId!, userId)), options);
    }

    public Task<FindResults<SavedView>> GetByOrganizationForUserAsync(string organizationId, string userId, CommandOptionsDescriptor<SavedView>? options = null)
    {
        return FindAsync(q => q
            .Organization(organizationId)
            .SortAscending(e => e.Name)
            .FieldOr(g => g
                .FieldEmpty(e => e.UserId!)
                .FieldEquals(e => e.UserId!, userId)), options);
    }

    public Task<FindResults<SavedView>> GetPredefinedForForceUpdateAsync(
        string systemOrganizationId,
        IReadOnlyCollection<string> predefinedKeys,
        CommandOptionsDescriptor<SavedView>? options = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(systemOrganizationId);
        ArgumentNullException.ThrowIfNull(predefinedKeys);

        if (predefinedKeys.Count == 0)
            return FindAsync(q => q.FieldEquals(e => e.Id, String.Empty), options);

        return FindAsync(q =>
        {
            q.FieldEmpty(e => e.UserId!)
                .FieldNotEquals(e => e.OrganizationId, systemOrganizationId)
                .FieldOr(g =>
                {
                    foreach (string predefinedKey in predefinedKeys)
                        g.FieldContains(e => e.PredefinedKey!, predefinedKey);
                });

            return q.SortAscending(e => e.OrganizationId).SortAscending(e => e.Id);
        }, options);
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
