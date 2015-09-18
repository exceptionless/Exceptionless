using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using FluentValidation;
using Foundatio.Caching;
using Foundatio.Messaging;
using Nest;

namespace Exceptionless.Core.Repositories {
    public class WebHookRepository : ElasticSearchRepositoryOwnedByOrganizationAndProject<WebHook>, IWebHookRepository {
        public WebHookRepository(IElasticClient elasticClient, OrganizationIndex index, IValidator<WebHook> validator = null, ICacheClient cacheClient = null, IMessagePublisher messagePublisher = null) 
            : base(elasticClient, index, validator, cacheClient, messagePublisher) { }

        public Task RemoveByUrlAsync(string targetUrl) {
            var filter = Filter<WebHook>.Term(e => e.Url, targetUrl);
            return RemoveAllAsync(new ElasticSearchOptions<WebHook>().WithFilter(filter));
        }

        public Task<FindResults<WebHook>> GetByOrganizationIdOrProjectIdAsync(string organizationId, string projectId) {
            var filter = (Filter<WebHook>.Term(e => e.OrganizationId, organizationId) && Filter<WebHook>.Missing(e => e.ProjectId)) || Filter<WebHook>.Term(e => e.ProjectId, projectId);
            return FindAsync(new ElasticSearchOptions<WebHook>()
                .WithFilter(filter)
                .WithCacheKey(String.Concat("org:", organizationId, "-project:", projectId))
                .WithExpiresIn(TimeSpan.FromMinutes(5)));
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
        
        protected override async Task InvalidateCacheAsync(ICollection<WebHook> hooks, ICollection<WebHook> originalHooks) {
            if (!EnableCache)
                return;

            hooks.ForEach(async h => await InvalidateCacheAsync(String.Concat("org:", h.OrganizationId, "-project:", h.ProjectId)).AnyContext());
            await base.InvalidateCacheAsync(hooks, originalHooks).AnyContext();
        }
    }
}