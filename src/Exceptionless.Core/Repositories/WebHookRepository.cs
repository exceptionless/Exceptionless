using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using FluentValidation;
using Foundatio.Repositories;
using Foundatio.Repositories.Elasticsearch.Extensions;
using Foundatio.Repositories.Models;
using ElasticInfer = Elastic.Clients.Elasticsearch.Infer;

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

        Query filter = new BoolQuery
        {
            Should = [
                new BoolQuery
                {
                    Must = [
                        new TermQuery { Field = ElasticInfer.Field<WebHook>(w => w.OrganizationId), Value = organizationId },
                        new BoolQuery { MustNot = [new ExistsQuery { Field = ElasticInfer.Field<WebHook>(w => w.ProjectId) }] }
                    ]
                },
                new TermQuery { Field = ElasticInfer.Field<WebHook>(w => w.ProjectId), Value = projectId }
            ],
            MinimumShouldMatch = 1
        };
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
        if (webHook is null)
        {
            _logger.LogWarning("WebHook {WebHookId} not found when marking as disabled", id);
            return;
        }

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
