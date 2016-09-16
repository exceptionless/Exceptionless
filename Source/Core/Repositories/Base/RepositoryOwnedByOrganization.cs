using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Queries;
using FluentValidation;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Foundatio.Repositories.Elasticsearch.Queries;
using Foundatio.Repositories.Models;
using Foundatio.Repositories.Queries;

namespace Exceptionless.Core.Repositories {
    public abstract class RepositoryOwnedByOrganization<T> : RepositoryBase<T>, IRepositoryOwnedByOrganization<T> where T : class, IOwnedByOrganization, IIdentity, new() {
        public RepositoryOwnedByOrganization(IIndexType<T> indexType, IValidator<T> validator) : base(indexType, validator) { }

        public virtual Task<IFindResults<T>> GetByOrganizationIdAsync(string organizationId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            if (String.IsNullOrEmpty(organizationId))
                return Task.FromResult<IFindResults<T>>(new FindResults<T>());

            string cacheKey = String.Concat("Organization:", organizationId);
            return FindAsync(new ExceptionlessQuery()
                .WithOrganizationId(organizationId)
                .WithPaging(paging)
                .WithCacheKey(useCache ? cacheKey : null)
                .WithExpiresIn(expiresIn));
        }

        public Task<long> RemoveAllByOrganizationIdAsync(string organizationId) {
            if (String.IsNullOrEmpty(organizationId))
                return Task.FromResult<long>(0);

            return RemoveAllAsync(new ExceptionlessQuery().WithOrganizationId(organizationId));
        }

        protected override async Task InvalidateCacheAsync(IReadOnlyCollection<ModifiedDocument<T>> documents) {
            if (!IsCacheEnabled)
                return;

            var docs = documents.Select(d => d.Value).Union(documents.Select(d => d.Original)).OfType<IOwnedByOrganization>();
            await Cache.RemoveAllAsync(docs
                .Where(d => !String.IsNullOrEmpty(d.OrganizationId))
                .Select(d => "Organization:" + d.OrganizationId)
                .Distinct()).AnyContext();

            await base.InvalidateCacheAsync(documents).AnyContext();
        }
    }
}
