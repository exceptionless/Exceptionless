using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Exceptionless.Core.Caching;
using Exceptionless.Core.Messaging;
using Exceptionless.Models;
using FluentValidation;
using Nest;

namespace Exceptionless.Core.Repositories {
    public abstract class ElasticSearchRepositoryOwnedByOrganization<T> : ElasticSearchRepository<T>, IRepositoryOwnedByOrganization<T> where T : class, IOwnedByOrganization, IIdentity, new() {
        public ElasticSearchRepositoryOwnedByOrganization(IElasticClient elasticClient, IValidator<T> validator = null, ICacheClient cacheClient = null, IMessagePublisher messagePublisher = null) : base(elasticClient, validator, cacheClient, messagePublisher) { }

        public virtual ICollection<T> GetByOrganizationId(string organizationId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            return GetByOrganizationIds(new[] { organizationId }, paging, useCache, expiresIn);
        }

        public virtual ICollection<T> GetByOrganizationIds(ICollection<string> organizationIds, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            if (organizationIds == null || organizationIds.Count == 0)
                return new List<T>();

            string cacheKey = String.Concat("org:", String.Join("", organizationIds).GetHashCode().ToString());
            return Find(new ElasticSearchOptions<T>()
                .WithOrganizationIds(organizationIds)
                .WithPaging(paging)
                .WithCacheKey(useCache ? cacheKey : null)
                .WithExpiresIn(expiresIn));
        }

        public async Task RemoveAllByOrganizationIdsAsync(string[] organizationIds) {
            await Task.Run(() => RemoveAll(new QueryOptions().WithOrganizationIds(organizationIds)));
        }

        public override void InvalidateCache(T document) {
            if (Cache == null)
                return;

            Cache.Remove(GetScopedCacheKey(String.Concat("org:", document.OrganizationId)));
            base.InvalidateCache(document);
        }
    }
}
