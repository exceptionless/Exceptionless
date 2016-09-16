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
    public abstract class RepositoryOwnedByOrganizationAndProject<T> : RepositoryOwnedByOrganization<T>, IRepositoryOwnedByProject<T> where T : class, IOwnedByProject, IIdentity, IOwnedByOrganization, new() {
        public RepositoryOwnedByOrganizationAndProject(IIndexType<T> indexType, IValidator<T> validator) : base(indexType, validator) {
            FieldsRequiredForRemove.Add("project_id");
        }

        public virtual Task<IFindResults<T>> GetByProjectIdAsync(string projectId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            return FindAsync(new ExceptionlessQuery()
                .WithProjectId(projectId)
                .WithPaging(paging)
                .WithCacheKey(useCache ? String.Concat("paged:Project:", projectId) : null)
                .WithExpiresIn(expiresIn));
        }

        public virtual Task<long> RemoveAllByProjectIdAsync(string organizationId, string projectId) {
            return RemoveAllAsync(new ExceptionlessQuery().WithOrganizationId(organizationId).WithProjectId(projectId));
        }

        protected override async Task InvalidateCachedQueriesAsync(IReadOnlyCollection<T> documents) {
            var projects = documents.Select(d => d.ProjectId).Distinct().Where(id => !String.IsNullOrEmpty(id));
            foreach (var projectId in projects)
                await Cache.RemoveByPrefixAsync($"paged:Project:{projectId}:*").AnyContext();

            await base.InvalidateCachedQueriesAsync(documents).AnyContext();
        }
    }
}
