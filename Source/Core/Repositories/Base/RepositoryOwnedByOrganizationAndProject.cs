using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Queries;
using FluentValidation;
using Foundatio.Caching;
using Foundatio.Logging;
using Foundatio.Messaging;
using Foundatio.Repositories.Elasticsearch.Queries;
using Foundatio.Repositories.Models;
using Foundatio.Repositories.Queries;
using Nest;

namespace Exceptionless.Core.Repositories {
    public abstract class RepositoryOwnedByOrganizationAndProject<T> : RepositoryOwnedByOrganization<T>, IRepositoryOwnedByProject<T> where T : class, IOwnedByProject, IIdentity, IOwnedByOrganization, new() {
        public RepositoryOwnedByOrganizationAndProject(IElasticClient client, IValidator<T> validator, ICacheClient cache, IMessagePublisher messagePublisher, ILogger logger) 
            : base(client, validator, cache, messagePublisher, logger) { }
        

        public virtual Task<IFindResults<T>> GetByProjectIdAsync(string projectId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            return FindAsync(new ExceptionlessQuery()
                .WithProjectId(projectId)
                .WithPaging(paging)
                .WithCacheKey(useCache ? String.Concat("project:", projectId) : null)
                .WithExpiresIn(expiresIn));
        }

        public virtual Task RemoveAllByProjectIdsAsync(string[]  projectIds) {
            return RemoveAllAsync(new ExceptionlessQuery().WithProjectIds(projectIds));
        }

        protected override async Task InvalidateCacheAsync(IReadOnlyCollection<ModifiedDocument<T>> documents) {
            if (!IsCacheEnabled)
                return;

            await Cache.RemoveAllAsync(documents.Select(d => d.Value)
                .Union(documents.Select(d => d.Original))
                .OfType<IOwnedByProject>()
                .Where(d => !String.IsNullOrEmpty(d.ProjectId))
                .Select(d => "project:" + d.ProjectId)
                .Distinct()).AnyContext();

            await base.InvalidateCacheAsync(documents).AnyContext();
        }
    }
}
