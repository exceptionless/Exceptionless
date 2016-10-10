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
        public RepositoryOwnedByOrganization(IIndexType<T> indexType, IValidator<T> validator) : base(indexType, validator) {
            FieldsRequiredForRemove.Add("organization_id");
            DocumentsAdded.AddHandler(OnDocumentsAdded);
        }

        public virtual Task<FindResults<T>> GetByOrganizationIdAsync(string organizationId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            if (String.IsNullOrEmpty(organizationId))
                throw new ArgumentNullException(nameof(organizationId));

            string cacheKey = String.Concat("paged:Organization:", organizationId);
            return FindAsync(new ExceptionlessQuery()
                .WithOrganizationId(organizationId)
                .WithPaging(paging)
                .WithCacheKey(useCache ? cacheKey : null)
                .WithExpiresIn(expiresIn));
        }

        public Task<long> RemoveAllByOrganizationIdAsync(string organizationId) {
            if (String.IsNullOrEmpty(organizationId))
                throw new ArgumentNullException(nameof(organizationId));

            return RemoveAllAsync(new ExceptionlessQuery().WithOrganizationId(organizationId));
        }

        protected override async Task InvalidateCacheAsync(IReadOnlyCollection<ModifiedDocument<T>> documents) {
            if (!IsCacheEnabled)
                return;

            await InvalidateCachedQueriesAsync(documents.Select(d => d.Value).ToList());
            await base.InvalidateCacheAsync(documents).AnyContext();
        }

        private Task OnDocumentsAdded(object sender, DocumentsEventArgs<T> documents) {
            if (!IsCacheEnabled)
                return Task.CompletedTask;

            return InvalidateCachedQueriesAsync(documents.Documents);
        }

        protected virtual async Task InvalidateCachedQueriesAsync(IReadOnlyCollection<T> documents) {
            var organizations = documents.Select(d => d.OrganizationId).Distinct().Where(id => !String.IsNullOrEmpty(id));
            foreach (var organizationId in organizations)
                await Cache.RemoveByPrefixAsync($"paged:Organization:{organizationId}:*").AnyContext();
        }
    }
}
