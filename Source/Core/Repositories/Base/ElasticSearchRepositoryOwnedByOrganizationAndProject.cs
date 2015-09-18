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

        public virtual FindResults<T> GetByProjectId(string projectId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            return Find(new ElasticSearchOptions<T>()
                .WithProjectId(projectId)
                .WithPaging(paging)
                .WithCacheKey(useCache ? String.Concat("project:", projectId) : null)
                .WithExpiresIn(expiresIn));
        }

        public virtual async Task RemoveAllByProjectIdsAsync(string[]  projectIds) {
            await Task.Run(() => RemoveAll(new QueryOptions().WithProjectIds(projectIds))).AnyContext();
        }

        protected override void InvalidateCache(ICollection<T> documents, ICollection<T> originalDocuments) {
            if (!EnableCache)
                return;

            documents.ForEach(doc => InvalidateCache(String.Concat("project:", doc.ProjectId)));
            if (originalDocuments != null)
                originalDocuments.ForEach(doc => InvalidateCache(String.Concat("project:", doc.ProjectId)));
            base.InvalidateCache(documents, originalDocuments);
        }
    }
}