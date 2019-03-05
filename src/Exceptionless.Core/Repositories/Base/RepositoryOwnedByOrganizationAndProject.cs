using System;
using System.Threading.Tasks;
using Exceptionless.Core.Models;
using FluentValidation;
using Foundatio.Repositories;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Foundatio.Repositories.Models;
using Microsoft.Extensions.Options;

namespace Exceptionless.Core.Repositories {
    public abstract class RepositoryOwnedByOrganizationAndProject<T> : RepositoryOwnedByOrganization<T>, IRepositoryOwnedByProject<T> where T : class, IOwnedByProject, IIdentity, IOwnedByOrganization, new() {
        public RepositoryOwnedByOrganizationAndProject(IIndexType<T> indexType, IValidator<T> validator, IOptions<AppOptions> options) : base(indexType, validator, options) {
            AddPropertyRequiredForRemove(o => o.ProjectId);
        }

        public virtual Task<FindResults<T>> GetByProjectIdAsync(string projectId, CommandOptionsDescriptor<T> options = null) {
            return FindAsync(q => q.Project(projectId), options);
        }

        public virtual Task<long> RemoveAllByProjectIdAsync(string organizationId, string projectId) {
            return RemoveAllAsync(q => q.Organization(organizationId).Project(projectId));
        }
    }
}
