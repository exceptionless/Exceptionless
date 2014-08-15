using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Caching;
using Exceptionless.Core.Messaging;
using Exceptionless.Core.Repositories;
using Exceptionless.Models;

namespace Exceptionless.Api.Tests.Repositories.InMemory {
    public class RepositoryOwnedByOrganization<T> : Repository<T>, IRepositoryOwnedByOrganization<T> where T : class, IOwnedByOrganization, IIdentity, new() {
        public RepositoryOwnedByOrganization(ICacheClient cacheClient = null, IMessagePublisher messagePublisher = null)
            : base(cacheClient, messagePublisher) {}

        protected override void BeforeAdd(ICollection<T> documents) {
            if (documents.Any(d => String.IsNullOrEmpty(d.OrganizationId)))
                throw new ArgumentException("OrganizationIds must be set.");

            base.BeforeAdd(documents);
        }

        public virtual ICollection<T> GetByOrganizationId(string organizationId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            return Find<T>(FindOptionsExtensions.WithPaging(new MultiOptions()
                    .WithOrganizationId(organizationId), paging)
                .WithCacheKey(useCache ? String.Concat("org:", organizationId) : null)
                .WithExpiresIn(expiresIn));
        }

        public virtual ICollection<T> GetByOrganizationIds(ICollection<string> organizationIds, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            if (organizationIds == null || organizationIds.Count == 0)
                return new List<T>();

            string cacheKey = String.Concat("org:", String.Join("", organizationIds).GetHashCode().ToString());
            return Find<T>(FindOptionsExtensions.WithPaging(new MultiOptions()
                    .WithOrganizationIds(organizationIds), paging)
                .WithCacheKey(useCache ? cacheKey : null)
                .WithExpiresIn(expiresIn));
        }

        public async Task RemoveAllByOrganizationIdAsync(string organizationId) {
            await Task.Run(() => RemoveAll(new QueryOptions().WithOrganizationId(organizationId)));
        }
    }
}
