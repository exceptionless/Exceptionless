using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using FluentValidation;
using Foundatio.Repositories;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Foundatio.Repositories.Models;
using Foundatio.Repositories.Options;

namespace Exceptionless.Core.Repositories {
    public abstract class RepositoryOwnedByOrganization<T> : RepositoryBase<T>, IRepositoryOwnedByOrganization<T> where T : class, IOwnedByOrganization, IIdentity, new() {
        public RepositoryOwnedByOrganization(IIndexType<T> indexType, IValidator<T> validator) : base(indexType, validator) {
            FieldsRequiredForRemove.Add("organization_id");
            DocumentsAdded.AddHandler(OnDocumentsAdded);
        }

        public virtual Task<FindResults<T>> GetByOrganizationIdAsync(string organizationId, CommandOptionsDescriptor<T> options = null) {
            if (String.IsNullOrEmpty(organizationId))
                throw new ArgumentNullException(nameof(organizationId));

            var commandOptions = options.Configure();
            if (commandOptions.ShouldUseCache())
                commandOptions.CacheKey(String.Concat("paged:Organization:", organizationId));

            return FindAsync(new RepositoryQuery<T>().Organization(organizationId), commandOptions);
        }

        public virtual Task<long> RemoveAllByOrganizationIdAsync(string organizationId) {
            if (String.IsNullOrEmpty(organizationId))
                throw new ArgumentNullException(nameof(organizationId));

            return RemoveAllAsync(q => q.Organization(organizationId));
        }

        protected override async Task InvalidateCacheAsync(IReadOnlyCollection<ModifiedDocument<T>> documents, ICommandOptions options = null) {
            if (!IsCacheEnabled)
                return;

            await InvalidateCachedQueriesAsync(documents.Select(d => d.Value).ToList(), options);
            await base.InvalidateCacheAsync(documents, options).AnyContext();
        }

        private Task OnDocumentsAdded(object sender, DocumentsEventArgs<T> documents) {
            if (!IsCacheEnabled)
                return Task.CompletedTask;

            return InvalidateCachedQueriesAsync(documents.Documents, documents.Options);
        }

        protected virtual async Task InvalidateCachedQueriesAsync(IReadOnlyCollection<T> documents, ICommandOptions options = null) {
            var organizations = documents.Select(d => d.OrganizationId).Distinct().Where(id => !String.IsNullOrEmpty(id));
            foreach (var organizationId in organizations)
                await Cache.RemoveByPrefixAsync($"paged:Organization:{organizationId}:*").AnyContext();
        }
    }
}