using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Queries;
using Foundatio.Elasticsearch.Configuration;
using Foundatio.Elasticsearch.Repositories;
using Foundatio.Elasticsearch.Repositories.Queries;
using Foundatio.Repositories.Models;
using Foundatio.Repositories.Queries;

namespace Exceptionless.Core.Repositories {
    public abstract class RepositoryOwnedByOrganization<T> : RepositoryBase<T>, IRepositoryOwnedByOrganization<T> where T : class, IOwnedByOrganization, IIdentity, new() {
        public RepositoryOwnedByOrganization(ElasticRepositoryContext<T> context, IElasticIndex index) : base(context, index) { }

        public Task<long> CountByOrganizationIdAsync(string organizationId) {
            var options = new ExceptionlessQuery().WithOrganizationId(organizationId);
            return CountAsync(options);
        }

        public virtual Task<FindResults<T>> GetByOrganizationIdAsync(string organizationId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            return GetByOrganizationIdsAsync(new[] { organizationId }, paging, useCache, expiresIn);
        }

        public virtual Task<FindResults<T>> GetByOrganizationIdsAsync(ICollection<string> organizationIds, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            if (organizationIds == null || organizationIds.Count == 0)
                return Task.FromResult(new FindResults<T> { Documents = new List<T>(), Total = 0 });

            // NOTE: There is no way to currently invalidate this.. If you try and cache this result, you should expect it to be dirty.
            string cacheKey = String.Concat("org:", String.Join("", organizationIds).GetHashCode().ToString());
            return FindAsync(new ExceptionlessQuery()
                .WithOrganizationIds(organizationIds)
                .WithPaging(paging)
                .WithCacheKey(useCache ? cacheKey : null)
                .WithExpiresIn(expiresIn));
        }

        public Task RemoveAllByOrganizationIdsAsync(string[] organizationIds) {
            return RemoveAllAsync(new ExceptionlessQuery().WithOrganizationIds(organizationIds));
        }

        protected override async Task InvalidateCacheAsync(ICollection<ModifiedDocument<T>> documents) {
            if (!IsCacheEnabled)
                return;

            await Cache.RemoveAllAsync(documents.Select(d => d.Value)
                .Union(documents.Select(d => d.Original))
                .OfType<IOwnedByOrganization>()
                .Where(d => !String.IsNullOrEmpty(d.OrganizationId))
                .Select(d => "org:" + d.OrganizationId)
                .Distinct()).AnyContext();

            await base.InvalidateCacheAsync(documents).AnyContext();
        }

        protected Task<long> UpdateAllAsync(string organizationId, object query, object update, bool sendNotifications = true) {
            return UpdateAllAsync(new[] { organizationId }, query, update, sendNotifications);
        }

        protected async Task<long> UpdateAllAsync(string[] organizationIds, object query, object update, bool sendNotifications = true) {
            var recordsAffected = await UpdateAllAsync(query, update, false).AnyContext();
            if (sendNotifications) {
                foreach (var organizationId in organizationIds) {
                    await PublishMessageAsync(new ExtendedEntityChanged {
                        ChangeType = ChangeType.Saved,
                        OrganizationId = organizationId,
                        Type = EntityType
                    }, TimeSpan.FromSeconds(1.5)).AnyContext();
                }
            }

            return recordsAffected;
        }
    }
}
