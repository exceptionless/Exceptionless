using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Queries;
using FluentValidation;
using Foundatio.Caching;
using Foundatio.Logging;
using Foundatio.Messaging;
using Foundatio.Repositories.Elasticsearch.Queries;
using Foundatio.Repositories.Elasticsearch.Queries.Builders;
using Foundatio.Repositories.Models;
using Foundatio.Repositories.Queries;
using Nest;

namespace Exceptionless.Core.Repositories {
    public abstract class RepositoryOwnedByOrganization<T> : RepositoryBase<T>, IRepositoryOwnedByOrganization<T> where T : class, IOwnedByOrganization, IIdentity, new() {
        public RepositoryOwnedByOrganization(IElasticClient client, IValidator<T> validator, ICacheClient cache, IMessagePublisher messagePublisher, ILogger logger) 
            : base(client, validator, cache, messagePublisher, logger) { }

        public Task<CountResult> CountByOrganizationIdAsync(string organizationId) {
            var options = new ExceptionlessQuery().WithOrganizationId(organizationId);
            return CountAsync(options);
        }

        public virtual Task<IFindResults<T>> GetByOrganizationIdAsync(string organizationId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            return GetByOrganizationIdsAsync(new[] { organizationId }, paging, useCache, expiresIn);
        }

        public virtual Task<IFindResults<T>> GetByOrganizationIdsAsync(ICollection<string> organizationIds, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            if (organizationIds == null || organizationIds.Count == 0)
                return Task.FromResult<IFindResults<T>>(new FindResults<T>());

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
        
        protected override async Task InvalidateCacheAsync(IReadOnlyCollection<ModifiedDocument<T>> documents) {
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

        protected Task<long> PatchAllAsync<TQuery>(string organizationId, TQuery query, object update, bool sendNotifications = true) where TQuery : IPagableQuery, ISelectedFieldsQuery {
            return PatchAllAsync(new[] { organizationId }, query, update, sendNotifications);
        }

        protected async Task<long> PatchAllAsync<TQuery>(string[] organizationIds, TQuery query, object update, bool sendNotifications = true) where TQuery : IPagableQuery, ISelectedFieldsQuery {
            var recordsAffected = await PatchAllAsync(query, update, false).AnyContext();
            if (recordsAffected <= 0 || !sendNotifications)
                return recordsAffected;

            foreach (var organizationId in organizationIds) {
                await PublishMessageAsync(new ExtendedEntityChanged {
                    ChangeType = ChangeType.Saved,
                    OrganizationId = organizationId,
                    Type = ElasticType.Name
                }, TimeSpan.FromSeconds(1.5)).AnyContext();
            }

            return recordsAffected;
        }
    }
}
