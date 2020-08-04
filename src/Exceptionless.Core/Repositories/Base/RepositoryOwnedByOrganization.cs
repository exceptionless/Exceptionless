using System;
using System.Threading.Tasks;
using Exceptionless.Core.Models;
using FluentValidation;
using Foundatio.Repositories;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Foundatio.Repositories.Models;
using Foundatio.Repositories.Options;

namespace Exceptionless.Core.Repositories {
    public abstract class RepositoryOwnedByOrganization<T> : RepositoryBase<T>, IRepositoryOwnedByOrganization<T> where T : class, IOwnedByOrganization, IIdentity, new() {
        public RepositoryOwnedByOrganization(IIndex index, IValidator<T> validator, AppOptions options) : base(index, validator, options) {
            AddPropertyRequiredForRemove(o => o.OrganizationId);
        }

        public virtual Task<FindResults<T>> GetByOrganizationIdAsync(string organizationId, CommandOptionsDescriptor<T> options = null) {
            if (String.IsNullOrEmpty(organizationId))
                throw new ArgumentNullException(nameof(organizationId));

            var commandOptions = options.Configure();
            if (commandOptions.ShouldUseCache())
                throw new Exception("Caching of paged queries is not allowed");

            return FindAsync(q => q.Organization(organizationId), o => commandOptions);
        }

        public virtual Task<long> RemoveAllByOrganizationIdAsync(string organizationId) {
            if (String.IsNullOrEmpty(organizationId))
                throw new ArgumentNullException(nameof(organizationId));

            return RemoveAllAsync(q => q.Organization(organizationId));
        }
    }
}