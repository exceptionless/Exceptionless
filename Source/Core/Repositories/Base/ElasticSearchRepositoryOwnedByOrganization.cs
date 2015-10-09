using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using FluentValidation;
using Foundatio.Caching;
using Foundatio.Messaging;
using Nest;

namespace Exceptionless.Core.Repositories {
    public abstract class ElasticSearchRepositoryOwnedByOrganization<T> : ElasticSearchRepository<T>, IRepositoryOwnedByOrganization<T> where T : class, IOwnedByOrganization, IIdentity, new() {
        public ElasticSearchRepositoryOwnedByOrganization(IElasticClient elasticClient, IElasticSearchIndex index, IValidator<T> validator = null, ICacheClient cacheClient = null, IMessagePublisher messagePublisher = null) : base(elasticClient, index, validator, cacheClient, messagePublisher) { }

        public Task<long> CountByOrganizationIdAsync(string organizationId) {
            var options = new ElasticSearchOptions<T>().WithOrganizationId(organizationId);
            return CountAsync(options);
        }

        public virtual Task<FindResults<T>> GetByOrganizationIdAsync(string organizationId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            if (String.IsNullOrEmpty(organizationId))
                return Task.FromResult(new FindResults<T> { Documents = new List<T>(), Total = 0 });

            string cacheKey = String.Concat("org:", organizationId);
            return FindAsync(new ElasticSearchOptions<T>()
                .WithOrganizationId(organizationId)
                .WithPaging(paging)
                .WithCacheKey(useCache ? cacheKey : null)
                .WithExpiresIn(expiresIn));
        }

        public virtual Task<FindResults<T>> GetByOrganizationIdsAsync(ICollection<string> organizationIds, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            if (organizationIds == null || organizationIds.Count == 0)
                return Task.FromResult(new FindResults<T> { Documents = new List<T>(), Total = 0 });
            
            // NOTE: There is no way to currently invalidate this.. If you try and cache this result, you should expect it to be dirty.
            string cacheKey = String.Concat("org:", String.Join("", organizationIds).GetHashCode().ToString());
            return FindAsync(new ElasticSearchOptions<T>()
                .WithOrganizationIds(organizationIds)
                .WithPaging(paging)
                .WithCacheKey(useCache ? cacheKey : null)
                .WithExpiresIn(expiresIn));
        }

        public Task RemoveAllByOrganizationIdsAsync(string[] organizationIds) {
            return RemoveAllAsync(new QueryOptions().WithOrganizationIds(organizationIds));
        }

        protected override async Task InvalidateCacheAsync(ICollection<T> documents, ICollection<T> originalDocuments) {
            if (!EnableCache)
                return;

            if (documents == null)
                throw new ArgumentNullException(nameof(documents));

            var combinedDocuments = new List<T>(documents);
            if (originalDocuments != null)
                combinedDocuments.AddRange(originalDocuments);
            
            foreach (var organizationId in combinedDocuments.Cast<IOwnedByOrganization>().SelectMany(d => d.OrganizationId).Distinct())
                await InvalidateCacheAsync("org:" + organizationId).AnyContext();

            await base.InvalidateCacheAsync(documents, originalDocuments).AnyContext();
        }
    }
}
