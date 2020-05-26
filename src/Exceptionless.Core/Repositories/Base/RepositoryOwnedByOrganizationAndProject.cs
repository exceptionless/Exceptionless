using System;
using System.Threading.Tasks;
using Exceptionless.Core.Models;
using FluentValidation;
using Foundatio.Repositories;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Repositories {
    public abstract class RepositoryOwnedByOrganizationAndProject<T> : RepositoryOwnedByOrganization<T>, IRepositoryOwnedByProject<T> where T : class, IOwnedByProject, IIdentity, IOwnedByOrganization, new() {
        public RepositoryOwnedByOrganizationAndProject(IIndex index, IValidator<T> validator, AppOptions options) : base(index, validator, options) {
            AddPropertyRequiredForRemove(o => o.ProjectId);
        }

        public virtual Task<QueryResults<T>> GetByProjectIdAsync(string projectId, CommandOptionsDescriptor<T> options = null) {
            if (String.IsNullOrEmpty(projectId))
                throw new ArgumentNullException(nameof(projectId));
            
            return QueryAsync(q => q.Project(projectId), options);
        }

        public virtual Task<long> RemoveAllByProjectIdAsync(string organizationId, string projectId) {
            if (String.IsNullOrEmpty(organizationId))
                throw new ArgumentNullException(nameof(organizationId));

            if (String.IsNullOrEmpty(projectId))
                throw new ArgumentNullException(nameof(projectId));
            
            return RemoveByQueryAsync(q => q.Organization(organizationId).Project(projectId));
        }
    }
}
