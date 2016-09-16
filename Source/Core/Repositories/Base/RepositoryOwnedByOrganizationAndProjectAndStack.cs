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
    public abstract class RepositoryOwnedByOrganizationAndProjectAndStack<T> : RepositoryOwnedByOrganizationAndProject<T>, IRepositoryOwnedByStack<T> where T : class, IOwnedByProject, IIdentity, IOwnedByStack, IOwnedByOrganization, new() {
        public RepositoryOwnedByOrganizationAndProjectAndStack(IIndexType<T> indexType, IValidator<T> validator) : base(indexType, validator) {
            FieldsRequiredForRemove.Add("stack_id");
        }

        public virtual Task<IFindResults<T>> GetByStackIdAsync(string stackId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            return FindAsync(new ExceptionlessQuery()
                .WithStackId(stackId)
                .WithPaging(paging)
                .WithCacheKey(useCache ? String.Concat("paged:Stack:", stackId) : null)
                .WithExpiresIn(expiresIn));
        }

        public Task<long> RemoveAllByStackIdAsync(string organizationId, string projectId, string stackId) {
            return RemoveAllAsync(new ExceptionlessQuery().WithOrganizationId(organizationId).WithProjectId(projectId).WithStackId(stackId));
        }

        protected override async Task InvalidateCachedQueriesAsync(IReadOnlyCollection<T> documents) {
            var stacks = documents.Select(d => d.StackId).Distinct().Where(id => !String.IsNullOrEmpty(id));
            foreach (var stackId in stacks)
                await Cache.RemoveByPrefixAsync($"paged:Stack:{stackId}:*").AnyContext();

            await base.InvalidateCachedQueriesAsync(documents).AnyContext();
        }
    }
}
