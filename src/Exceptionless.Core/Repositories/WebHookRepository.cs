using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using FluentValidation;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;
using Nest;

namespace Exceptionless.Core.Repositories;

public sealed class WebHookRepository : RepositoryOwnedByOrganizationAndProject<WebHook>, IWebHookRepository
{
    public WebHookRepository(ExceptionlessElasticConfiguration configuration, IValidator<WebHook> validator,
        AppOptions options)
        : base(configuration.WebHooks, validator, options)
    {
    }

    public Task<FindResults<WebHook>> GetByUrlAsync(string targetUrl)
    {
        return FindAsync(q => q.FieldEquals(w => w.Url, targetUrl).Sort(f => f.CreatedUtc));
    }

    public Task<FindResults<WebHook>> GetByOrganizationIdOrProjectIdAsync(string organizationId, string projectId)
    {
        ArgumentException.ThrowIfNullOrEmpty(organizationId);
        ArgumentException.ThrowIfNullOrEmpty(projectId);

        var filter = (Query<WebHook>.Term(e => e.OrganizationId, organizationId) && !Query<WebHook>.Exists(e => e.Field(f => f.ProjectId))) || Query<WebHook>.Term(e => e.ProjectId, projectId);
        return FindAsync(q => q.ElasticFilter(filter).Sort(f => f.CreatedUtc), o => o.Cache(PagedCacheKey(organizationId, projectId)));
    }

    public override Task<FindResults<WebHook>> GetByProjectIdAsync(string projectId, CommandOptionsDescriptor<WebHook>? options = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectId);

        return FindAsync(q => q.Project(projectId).Sort(f => f.CreatedUtc), options);
    }

    public async Task MarkDisabledAsync(string id)
    {
        var webHook = await GetByIdAsync(id);
        if (!webHook.IsEnabled)
            return;

        webHook.IsEnabled = false;
        await SaveAsync(webHook, o => o.Cache());
    }

    protected override async Task InvalidateCacheAsync(IReadOnlyCollection<ModifiedDocument<WebHook>> documents, ChangeType? changeType = null)
    {
        var originalAndModifiedDocuments = documents.UnionOriginalAndModified();
        var keysToRemove = originalAndModifiedDocuments.Select(CacheKey).Distinct();
        await Cache.RemoveAllAsync(keysToRemove);

        var pagedKeysToRemove = originalAndModifiedDocuments.Select(d => PagedCacheKey(d.OrganizationId, d.ProjectId)).Distinct();
        foreach (string key in pagedKeysToRemove)
            await Cache.RemoveByPrefixAsync(key);

        await base.InvalidateCacheAsync(documents, changeType);
    }

    private static string CacheKey(WebHook webHook) => String.Concat("Organization:", webHook.OrganizationId, ":Project:", webHook.ProjectId);
    private static string PagedCacheKey(string organizationId, string projectId) => String.Concat("paged:Organization:", organizationId, ":Project:", projectId);
}
