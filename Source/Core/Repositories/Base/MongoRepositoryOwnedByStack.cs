using System;
using System.Collections.Generic;
using System.Linq;
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
    public abstract class MongoRepositoryOwnedByStack<T> : MongoRepository<T>, IRepositoryOwnedByStack<T> where T : class, IOwnedByOrganization, IOwnedByProject, IOwnedByStack, IIdentity, new() {
        public MongoRepositoryOwnedByStack(MongoDatabase database, IValidator<T> validator = null, ICacheClient cacheClient = null, IMessagePublisher messagePublisher = null)
            : base(database, validator, cacheClient, messagePublisher) {}

        public virtual ICollection<T> GetByStackId(string stackId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            return Find<T>(new MultiOptions()
                .WithStackId(stackId)
                .WithPaging(paging)
                .WithCacheKey(useCache ? String.Concat("stack:", stackId) : null)
                .WithExpiresIn(expiresIn));
        }

        public async Task RemoveAllByStackIdAsync(string stackId) {
            await Task.Run(() => RemoveAll(new QueryOptions().WithStackId(stackId)));
        }

        protected override void ConfigureClassMap(BsonClassMap<T> cm) {
            base.ConfigureClassMap(cm);
            cm.GetMemberMap(c => c.StackId).SetElementName(CommonFieldNames.StackId).SetRepresentation(BsonType.ObjectId).SetIdGenerator(new StringObjectIdGenerator());
        }

        public override void InvalidateCache(T document) {
            if (Cache == null)
                return;

            Cache.Remove(GetScopedCacheKey(String.Concat("stack:", document.StackId)));
            base.InvalidateCache(document);
        }
    }
}
