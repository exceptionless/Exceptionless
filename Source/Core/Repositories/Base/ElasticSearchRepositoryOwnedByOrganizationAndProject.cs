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
    public abstract class ElasticSearchRepositoryOwnedByOrganizationAndProject<T> : ElasticSearchRepositoryOwnedByOrganization<T>, IRepositoryOwnedByProject<T> where T : class, IOwnedByProject, IIdentity, IOwnedByOrganization, new() {
        public ElasticSearchRepositoryOwnedByOrganizationAndProject(IElasticClient elasticClient, IElasticSearchIndex index, IValidator<T> validator = null, ICacheClient cacheClient = null, IMessagePublisher messagePublisher = null) : base(elasticClient, index, validator, cacheClient, messagePublisher) { }

        public virtual Task<FindResults<T>> GetByProjectIdAsync(string projectId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            return FindAsync(new ElasticSearchOptions<T>()
                .WithProjectId(projectId)
                .WithPaging(paging)
                .WithCacheKey(useCache ? String.Concat("project:", projectId) : null)
                .WithExpiresIn(expiresIn));
        }

        public virtual Task RemoveAllByProjectIdsAsync(string[]  projectIds) {
            return RemoveAllAsync(new QueryOptions().WithProjectIds(projectIds));
        }

        protected override async Task InvalidateCacheAsync(ICollection<T> documents, ICollection<T> originalDocuments) {
            if (!EnableCache)
                return;

            documents.ForEach(async doc => await InvalidateCacheAsync(String.Concat("project:", doc.ProjectId)).AnyContext());
            originalDocuments?.ForEach(async doc => await InvalidateCacheAsync(String.Concat("project:", doc.ProjectId)).AnyContext());
            await base.InvalidateCacheAsync(documents, originalDocuments).AnyContext();
        }
    }
}