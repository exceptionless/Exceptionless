using System;
using System.Threading.Tasks;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Queries;
using FluentValidation;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Foundatio.Repositories.Models;
using Foundatio.Repositories.Queries;

namespace Exceptionless.Core.Repositories {
    public abstract class RepositoryOwnedByOrganizationAndProject<T> : RepositoryOwnedByOrganization<T>, IRepositoryOwnedByProject<T> where T : class, IOwnedByProject, IIdentity, IOwnedByOrganization, new() {
        public RepositoryOwnedByOrganizationAndProject(IIndexType<T> indexType, IValidator<T> validator) : base(indexType, validator) {
            FieldsRequiredForRemove.Add("project_id");
        }

        public virtual Task<FindResults<T>> GetByProjectIdAsync(string projectId, PagingOptions paging = null) {
            return FindAsync(new ExceptionlessQuery()
                .WithProjectId(projectId)
                .WithPaging(paging));
        }

        public virtual Task<long> RemoveAllByProjectIdAsync(string organizationId, string projectId) {
            return RemoveAllAsync(new ExceptionlessQuery().WithOrganizationId(organizationId).WithProjectId(projectId));
        }
    }
}
