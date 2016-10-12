using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Repositories.Queries;
using FluentValidation;
using Foundatio.Repositories.Elasticsearch.Queries;
using Foundatio.Repositories.Elasticsearch.Queries.Builders;
using Foundatio.Repositories.Models;
using Nest;

namespace Exceptionless.Core.Repositories {
    public class WebHookRepository : RepositoryOwnedByOrganizationAndProject<WebHook>, IWebHookRepository {
        public WebHookRepository(ExceptionlessElasticConfiguration configuration, IValidator<WebHook> validator)
            : base(configuration.Organizations.WebHook, validator) {}

        public Task<FindResults<WebHook>> GetByUrlAsync(string targetUrl) {
            return FindAsync(new ExceptionlessQuery().WithFieldEquals(GetPropertyName(nameof(WebHook.Url)), targetUrl));
        }

        public Task<FindResults<WebHook>> GetByOrganizationIdOrProjectIdAsync(string organizationId, string projectId) {
            var filter = (Query<WebHook>.Term(e => e.OrganizationId, organizationId) && !Query<WebHook>.Exists(e => e.Field(f => f.ProjectId))) || Query<WebHook>.Term(e => e.ProjectId, projectId);

            // TODO: This cache key may not always be cleared out if the webhook doesn't have both a org and project id.
            return FindAsync(new ExceptionlessQuery()
                .WithElasticFilter(filter)
                .WithCacheKey(String.Concat("paged:Organization:", organizationId, ":Project:", projectId)));
        }

        public static class EventTypes {
            // TODO: Add support for these new web hook types.
            public const string NewError = "NewError";
            public const string CriticalError = "CriticalError";
            public const string NewEvent = "NewEvent";
            public const string CriticalEvent = "CriticalEvent";
            public const string StackRegression = "StackRegression";
            public const string StackPromoted = "StackPromoted";
        }

        protected override async Task InvalidateCacheAsync(IReadOnlyCollection<ModifiedDocument<WebHook>> documents) {
            if (!IsCacheEnabled)
                return;

            await Cache.RemoveAllAsync(documents.Select(d => d.Value)
                .Union(documents.Select(d => d.Original).Where(d => d != null))
                .Select(h => String.Concat("Organization:", h.OrganizationId, ":Project:", h.ProjectId))
                .Distinct()).AnyContext();

            await base.InvalidateCacheAsync(documents).AnyContext();
        }

        protected override async Task InvalidateCachedQueriesAsync(IReadOnlyCollection<WebHook> documents) {
            var keysToRemove = documents.Select(d => $"paged:Organization:{d.OrganizationId}:Project:{d.ProjectId}:*").Distinct();
            foreach (var key in keysToRemove)
                await Cache.RemoveByPrefixAsync(key).AnyContext();

            await base.InvalidateCachedQueriesAsync(documents).AnyContext();
        }
    }
}