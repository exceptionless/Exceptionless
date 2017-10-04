using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using FluentValidation;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;
using Nest;

namespace Exceptionless.Core.Repositories {
    public sealed class WebHookRepository : RepositoryOwnedByOrganizationAndProject<WebHook>, IWebHookRepository {
        public WebHookRepository(ExceptionlessElasticConfiguration configuration, IValidator<WebHook> validator)
            : base(configuration.Organizations.WebHook, validator) {}

        public Task<FindResults<WebHook>> GetByUrlAsync(string targetUrl) {
            return FindAsync(q => q.FieldEquals(w => w.Url, targetUrl));
        }

        public Task<FindResults<WebHook>> GetByOrganizationIdOrProjectIdAsync(string organizationId, string projectId) {
            var filter = (Query<WebHook>.Term(e => e.OrganizationId, organizationId) && !Query<WebHook>.Exists(e => e.Field(f => f.ProjectId))) || Query<WebHook>.Term(e => e.ProjectId, projectId);

            // TODO: This cache key may not always be cleared out if the web hook doesn't have both a org and project id.
            return FindAsync(q => q.ElasticFilter(filter), o => o.CacheKey(String.Concat("paged:Organization:", organizationId, ":Project:", projectId)));
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

        protected override Task InvalidateCacheAsync(IReadOnlyCollection<ModifiedDocument<WebHook>> documents, ICommandOptions options = null) {
            if (!IsCacheEnabled)
                return Task.CompletedTask;

            var keys = documents.Select(d => d.Value).Union(documents.Select(d => d.Original).Where(d => d != null)).Select(h => String.Concat("Organization:", h.OrganizationId, ":Project:", h.ProjectId)).Distinct();
            return Task.WhenAll(Cache.RemoveAllAsync(keys), base.InvalidateCacheAsync(documents, options));
        }

        protected override Task InvalidateCachedQueriesAsync(IReadOnlyCollection<WebHook> documents, ICommandOptions options = null) {
            var keysToRemove = documents.Select(d => $"paged:Organization:{d.OrganizationId}:Project:{d.ProjectId}").Distinct();
            var tasks = new List<Task>();
            foreach (string key in keysToRemove)
                tasks.Add(Cache.RemoveByPrefixAsync(key));

            tasks.Add(base.InvalidateCachedQueriesAsync(documents, options));
            return Task.WhenAll(tasks);
        }
    }
}