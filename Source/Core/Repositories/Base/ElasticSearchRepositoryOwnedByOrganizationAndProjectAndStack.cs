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
    public abstract class ElasticSearchRepositoryOwnedByOrganizationAndProjectAndStack<T> : ElasticSearchRepositoryOwnedByOrganizationAndProject<T>, IRepositoryOwnedByStack<T> where T : class, IOwnedByProject, IIdentity, IOwnedByStack, IOwnedByOrganization, new() {
        public ElasticSearchRepositoryOwnedByOrganizationAndProjectAndStack(IElasticClient elasticClient, IElasticSearchIndex index, IValidator<T> validator = null, ICacheClient cacheClient = null, IMessagePublisher messagePublisher = null) : base(elasticClient, index, validator, cacheClient, messagePublisher) {}

        public virtual Task<FindResults<T>> GetByStackIdAsync(string stackId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            return FindAsync(new ElasticSearchOptions<T>()
                .WithStackId(stackId)
                .WithPaging(paging)
                .WithCacheKey(useCache ? String.Concat("stack:", stackId) : null)
                .WithExpiresIn(expiresIn));
        }

        public async Task RemoveAllByStackIdsAsync(string[] stackIds) {
            await Task.Run(async () => await RemoveAllAsync(new QueryOptions().WithStackIds(stackIds)).AnyContext()).AnyContext();
        }

        protected override async Task InvalidateCacheAsync(ICollection<T> documents, ICollection<T> originalDocuments) {
            if (!EnableCache)
                return;

            documents.ForEach(async d => await InvalidateCacheAsync(String.Concat("stack:", d.StackId)).AnyContext());
            await base.InvalidateCacheAsync(documents, originalDocuments).AnyContext();
        }
    }
}