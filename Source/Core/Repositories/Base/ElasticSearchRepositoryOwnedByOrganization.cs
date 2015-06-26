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

        public long CountByOrganizationId(string organizationId) {
            var options = new ElasticSearchOptions<T>().WithOrganizationId(organizationId);

            return Count(options);
        }

        public virtual FindResults<T> GetByOrganizationId(string organizationId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            return GetByOrganizationIds(new[] { organizationId }, paging, useCache, expiresIn);
        }

        public virtual FindResults<T> GetByOrganizationIds(ICollection<string> organizationIds, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            if (organizationIds == null || organizationIds.Count == 0)
                return new FindResults<T> { Documents = new List<T>(), Total = 0 };

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

        protected override void InvalidateCache(ICollection<T> documents, ICollection<T> originalDocuments)
        {
            if (!EnableCache)
                return;

            if (documents == null)
                throw new ArgumentNullException("documents");

            var combinedDocuments = new List<T>();
            combinedDocuments.AddRange(documents);
            if (originalDocuments != null)
                combinedDocuments.AddRange(originalDocuments);

            combinedDocuments
                .Cast<IOwnedByOrganization>()
                .SelectMany(d => d.OrganizationId)
                .Distinct()
                .ForEach(organizationId => InvalidateCache("org:" + organizationId));

            base.InvalidateCache(documents, originalDocuments);
        }
    }
}
