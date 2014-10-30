using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Exceptionless.Core.Caching;
using Exceptionless.Core.Messaging;
using Exceptionless.Models;
using FluentValidation;
using Nest;

namespace Exceptionless.Core.Repositories {
    public abstract class ElasticSearchRepositoryOwnedByOrganizationAndProject<T> : ElasticSearchRepositoryOwnedByOrganization<T>, IRepositoryOwnedByProject<T> where T : class, IOwnedByProject, IIdentity, IOwnedByOrganization, new() {
        public ElasticSearchRepositoryOwnedByOrganizationAndProject(IElasticClient elasticClient, IValidator<T> validator = null, ICacheClient cacheClient = null, IMessagePublisher messagePublisher = null) : base(elasticClient, validator, cacheClient, messagePublisher) { }

        public virtual ICollection<T> GetByProjectId(string projectId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            return Find(new ElasticSearchOptions<T>()
                .WithProjectId(projectId)
                .WithPaging(paging)
                .WithCacheKey(useCache ? String.Concat("project:", projectId) : null)
                .WithExpiresIn(expiresIn));
        }

        public async Task RemoveAllByProjectIdsAsync(string[]  projectIds) {
            await Task.Run(() => RemoveAll(new QueryOptions().WithProjectIds(projectIds)));
        }

        public override void InvalidateCache(T document) {
            if (Cache == null)
                return;

            Cache.Remove(GetScopedCacheKey(String.Concat("project:", document.ProjectId)));
            base.InvalidateCache(document);
        }
    }
}