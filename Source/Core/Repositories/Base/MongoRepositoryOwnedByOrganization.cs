using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Exceptionless.Core.Caching;
using Exceptionless.Core.Messaging;
using Exceptionless.Models;
using FluentValidation;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Driver;

namespace Exceptionless.Core.Repositories {
    public abstract class MongoRepositoryOwnedByOrganization<T> : MongoRepository<T>, IRepositoryOwnedByOrganization<T> where T : class, IOwnedByOrganization, IIdentity, new() {
        public MongoRepositoryOwnedByOrganization(MongoDatabase database, IValidator<T> validator = null, ICacheClient cacheClient = null, IMessagePublisher messagePublisher = null)
            : base(database, validator, cacheClient, messagePublisher) {}

        public virtual ICollection<T> GetByOrganizationId(string organizationId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            return Find<T>(new MultiOptions()
                .WithOrganizationId(organizationId)
                .WithPaging(paging)
                .WithCacheKey(useCache ? String.Concat("org:", organizationId) : null)
                .WithExpiresIn(expiresIn));
        }

        public virtual ICollection<T> GetByOrganizationIds(ICollection<string> organizationIds, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            if (organizationIds == null || organizationIds.Count == 0)
                return new List<T>();

            string cacheKey = String.Concat("org:", String.Join("", organizationIds).GetHashCode().ToString());
            return Find<T>(new MultiOptions()
                .WithOrganizationIds(organizationIds)
                .WithPaging(paging)
                .WithCacheKey(useCache ? cacheKey : null)
                .WithExpiresIn(expiresIn));
        }

        public async Task RemoveAllByOrganizationIdsAsync(string[] organizationIds) {
            await Task.Run(() => RemoveAll(new QueryOptions().WithOrganizationIds(organizationIds)));
        }

        protected override void ConfigureClassMap(BsonClassMap<T> cm) {
            base.ConfigureClassMap(cm);
            cm.GetMemberMap(c => c.OrganizationId).SetElementName(CommonFieldNames.OrganizationId).SetRepresentation(BsonType.ObjectId).SetIdGenerator(new StringObjectIdGenerator());
        }

        public override void InvalidateCache(T document) {
            if (Cache == null)
                return;

            Cache.Remove(GetScopedCacheKey(String.Concat("org:", document.OrganizationId)));
            base.InvalidateCache(document);
        }
    }
}
