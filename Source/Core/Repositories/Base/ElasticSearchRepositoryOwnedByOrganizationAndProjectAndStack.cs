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

        public virtual FindResults<T> GetByStackId(string stackId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            return Find(new ElasticSearchOptions<T>()
                .WithStackId(stackId)
                .WithPaging(paging)
                .WithCacheKey(useCache ? String.Concat("stack:", stackId) : null)
                .WithExpiresIn(expiresIn));
        }

        public async Task RemoveAllByStackIdsAsync(string[] stackIds) {
            await Task.Run(() => RemoveAll(new QueryOptions().WithStackIds(stackIds))).AnyContext();
        }

        protected override void InvalidateCache(ICollection<T> documents, ICollection<T> originalDocuments) {
            if (!EnableCache)
                return;

            documents.ForEach(d => InvalidateCache(String.Concat("stack:", d.StackId)));
            base.InvalidateCache(documents, originalDocuments);
        }
    }
}