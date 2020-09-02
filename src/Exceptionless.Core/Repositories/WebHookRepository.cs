using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using FluentValidation;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;
using Nest;

namespace Exceptionless.Core.Repositories {
    public sealed class WebHookRepository : RepositoryOwnedByOrganizationAndProject<WebHook>, IWebHookRepository {
        public WebHookRepository(ExceptionlessElasticConfiguration configuration, IValidator<WebHook> validator, AppOptions options)
            : base(configuration.WebHooks, validator, options) {}

        public Task<FindResults<WebHook>> GetByUrlAsync(string targetUrl) {
            return FindAsync(q => q.FieldEquals(w => w.Url, targetUrl));
        }

        public Task<FindResults<WebHook>> GetByOrganizationIdOrProjectIdAsync(string organizationId, string projectId) {
            var filter = (Query<WebHook>.Term(e => e.OrganizationId, organizationId) && !Query<WebHook>.Exists(e => e.Field(f => f.ProjectId))) || Query<WebHook>.Term(e => e.ProjectId, projectId);

            // TODO: This cache key may not always be cleared out if the web hook doesn't have both a org and project id.
            return FindAsync(q => q.ElasticFilter(filter), o => o.CacheKey(PagedCacheKey(organizationId, projectId)));
        }

        public async Task MarkDisabledAsync(string id) {
            var webHook = await GetByIdAsync(id).AnyContext();
            if (!webHook.IsEnabled)
                return;
            
            webHook.IsEnabled = false;
            await SaveAsync(webHook, o => o.Cache()).AnyContext();
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

        protected override async Task InvalidateCacheAsync(IReadOnlyCollection<ModifiedDocument<WebHook>> documents, ChangeType? changeType = null) {
            var keysToRemove = documents.Select(d => d.Value).Select(CacheKey).Distinct();
            await Cache.RemoveAllAsync(keysToRemove).AnyContext();
            
            var pagedKeysToRemove = documents.Select(d => PagedCacheKey(d.Value.OrganizationId, d.Value.ProjectId)).Distinct();
            foreach (string key in pagedKeysToRemove) 
                await Cache.RemoveByPrefixAsync(key).AnyContext();

            await base.InvalidateCacheAsync(documents, changeType).AnyContext();
        }

        private string CacheKey(WebHook webHook) => String.Concat("Organization:", webHook.OrganizationId, ":Project:", webHook.ProjectId);
        private string PagedCacheKey(string organizationId, string projectId) => String.Concat("paged:Organization:", organizationId, ":Project:", projectId);
    }
}