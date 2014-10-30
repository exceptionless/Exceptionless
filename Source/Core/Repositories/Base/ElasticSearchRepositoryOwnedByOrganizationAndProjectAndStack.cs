using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Exceptionless.Core.Caching;
using Exceptionless.Core.Messaging;
using Exceptionless.Models;
using FluentValidation;
using Nest;

namespace Exceptionless.Core.Repositories {
    public abstract class ElasticSearchRepositoryOwnedByOrganizationAndProjectAndStack<T> : ElasticSearchRepositoryOwnedByOrganizationAndProject<T>, IRepositoryOwnedByStack<T> where T : class, IOwnedByProject, IIdentity, IOwnedByStack, IOwnedByOrganization, new() {
        public ElasticSearchRepositoryOwnedByOrganizationAndProjectAndStack(IElasticClient elasticClient, IValidator<T> validator = null, ICacheClient cacheClient = null, IMessagePublisher messagePublisher = null)
            : base(elasticClient, validator, cacheClient, messagePublisher) {}

        public virtual ICollection<T> GetByStackId(string stackId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            return Find(new ElasticSearchOptions<T>()
                .WithStackId(stackId)
                .WithPaging(paging)
                .WithCacheKey(useCache ? String.Concat("stack:", stackId) : null)
                .WithExpiresIn(expiresIn));
        }

        public async Task RemoveAllByStackIdsAsync(string[] stackIds) {
            await Task.Run(() => RemoveAll(new QueryOptions().WithStackIds(stackIds)));
        }

        public override void InvalidateCache(T document) {
            if (Cache == null)
                return;

            Cache.Remove(GetScopedCacheKey(String.Concat("stack:", document.StackId)));
            base.InvalidateCache(document);
        }
    }
}