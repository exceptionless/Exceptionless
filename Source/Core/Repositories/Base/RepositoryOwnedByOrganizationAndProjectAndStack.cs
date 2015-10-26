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
    public abstract class RepositoryOwnedByOrganizationAndProjectAndStack<T> : RepositoryOwnedByOrganizationAndProject<T>, IRepositoryOwnedByStack<T> where T : class, IOwnedByProject, IIdentity, IOwnedByStack, IOwnedByOrganization, new() {
        public RepositoryOwnedByOrganizationAndProjectAndStack(IElasticClient elasticClient, IElasticSearchIndex index, IValidator<T> validator = null, ICacheClient cacheClient = null, IMessagePublisher messagePublisher = null) : base(elasticClient, index, validator, cacheClient, messagePublisher) {}

        public virtual Task<FindResults<T>> GetByStackIdAsync(string stackId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            return FindAsync(new ElasticSearchOptions<T>()
                .WithStackId(stackId)
                .WithPaging(paging)
                .WithCacheKey(useCache ? String.Concat("stack:", stackId) : null)
                .WithExpiresIn(expiresIn));
        }

        public Task RemoveAllByStackIdsAsync(string[] stackIds) {
            return RemoveAllAsync(new QueryOptions().WithStackIds(stackIds));
        }

        protected override async Task InvalidateCacheAsync(ICollection<T> documents, ICollection<T> originalDocuments) {
            if (!EnableCache)
                return;

            foreach (var document in documents)
                await InvalidateCacheAsync(String.Concat("stack:", document.StackId)).AnyContext();

            await base.InvalidateCacheAsync(documents, originalDocuments).AnyContext();
        }
    }
}