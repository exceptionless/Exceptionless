using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Repositories.Queries;
using Foundatio.Elasticsearch.Repositories;
using Foundatio.Elasticsearch.Repositories.Queries;
using Foundatio.Logging;
using Foundatio.Repositories.Models;
using Nest;

namespace Exceptionless.Core.Repositories {
    public class WebHookRepository : RepositoryOwnedByOrganizationAndProject<WebHook>, IWebHookRepository {
        public WebHookRepository(ElasticRepositoryContext<WebHook> context, OrganizationIndex index, ILoggerFactory loggerFactory = null) : base(context, index, loggerFactory) { }

        public Task RemoveByUrlAsync(string targetUrl) {
            var filter = Filter<WebHook>.Term(e => e.Url, targetUrl);
            return RemoveAllAsync(new ExceptionlessQuery().WithElasticFilter(filter));
        }

        public Task<FindResults<WebHook>> GetByOrganizationIdOrProjectIdAsync(string organizationId, string projectId) {
            var filter = (Filter<WebHook>.Term(e => e.OrganizationId, organizationId) && Filter<WebHook>.Missing(e => e.ProjectId)) || Filter<WebHook>.Term(e => e.ProjectId, projectId);
            return FindAsync(new ExceptionlessQuery()
                .WithElasticFilter(filter)
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

        protected override async Task InvalidateCacheAsync(ICollection<ModifiedDocument<WebHook>> documents) {
            if (!IsCacheEnabled)
                return;

            await Cache.RemoveAllAsync(documents.Select(d => d.Value)
                .Union(documents.Select(d => d.Original).Where(d => d != null))
                .Select(h => String.Concat("org:", h.OrganizationId, "-project:", h.ProjectId))
                .Distinct()).AnyContext();

            await base.InvalidateCacheAsync(documents).AnyContext();
        }
    }
}
