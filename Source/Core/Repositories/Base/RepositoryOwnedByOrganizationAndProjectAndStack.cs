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
    public abstract class RepositoryOwnedByOrganizationAndProjectAndStack<T> : RepositoryOwnedByOrganizationAndProject<T>, IRepositoryOwnedByStack<T> where T : class, IOwnedByProject, IIdentity, IOwnedByStack, IOwnedByOrganization, new() {
        public RepositoryOwnedByOrganizationAndProjectAndStack(RepositoryContext<T> context, IElasticsearchIndex index) : base(context, index) { }

        public virtual Task<FindResults<T>> GetByStackIdAsync(string stackId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            return FindAsync(NewQuery()
                .WithStackId(stackId)
                .WithPaging(paging)
                .WithCacheKey(useCache ? String.Concat("stack:", stackId) : null)
                .WithExpiresIn(expiresIn));
        }

        public Task RemoveAllByStackIdsAsync(string[] stackIds) {
            return RemoveAllAsync(NewQuery().WithStackIds(stackIds));
        }

        protected override async Task InvalidateCacheAsync(ICollection<ModifiedDocument<T>> documents) {
            if (!IsCacheEnabled)
                return;
            
            await Cache.RemoveAllAsync(documents.Select(d => d.Value)
                .Union(documents.Select(d => d.Original))
                .Cast<IOwnedByStack>()
                .Where(d => !String.IsNullOrEmpty(d.StackId))
                .Distinct()
                .Select(d => "stack:" + d.StackId)).AnyContext();

            await base.InvalidateCacheAsync(documents).AnyContext();
        }
    }
}