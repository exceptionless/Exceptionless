using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Queries;
using Foundatio.Elasticsearch.Configuration;
using Foundatio.Elasticsearch.Repositories;
using Foundatio.Elasticsearch.Repositories.Queries;
using Foundatio.Repositories.Models;
using Foundatio.Repositories.Queries;

namespace Exceptionless.Core.Repositories {
    public abstract class RepositoryOwnedByOrganizationAndProject<T> : RepositoryOwnedByOrganization<T>, IRepositoryOwnedByProject<T> where T : class, IOwnedByProject, IIdentity, IOwnedByOrganization, new() {
        public RepositoryOwnedByOrganizationAndProject(RepositoryContext<T> context, IElasticsearchIndex index) : base(context, index) { }

        public virtual Task<FindResults<T>> GetByProjectIdAsync(string projectId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            return FindAsync(NewQuery()
                .WithProjectId(projectId)
                .WithPaging(paging)
                .WithCacheKey(useCache ? String.Concat("project:", projectId) : null)
                .WithExpiresIn(expiresIn));
        }

        public virtual Task RemoveAllByProjectIdsAsync(string[]  projectIds) {
            return RemoveAllAsync(NewQuery().WithProjectIds(projectIds));
        }

        protected override async Task InvalidateCacheAsync(ICollection<ModifiedDocument<T>> documents) {
            if (!IsCacheEnabled)
                return;

            await Cache.RemoveAllAsync(documents.Select(d => d.Value)
                .Union(documents.Select(d => d.Original))
                .OfType<IOwnedByProject>()
                .Where(d => !String.IsNullOrEmpty(d.ProjectId))
                .Select(d => "project:" + d.ProjectId)
                .Distinct()).AnyContext();

            await base.InvalidateCacheAsync(documents).AnyContext();
        }
    }
}