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
    public abstract class MongoRepositoryOwnedByOrganizationAndProject<T> : MongoRepositoryOwnedByOrganization<T>, IRepositoryOwnedByOrganizationAndProject<T> where T : class, IOwnedByProject, IIdentity, IOwnedByOrganization, new() {
        public MongoRepositoryOwnedByOrganizationAndProject(MongoDatabase database, IValidator<T> validator = null, ICacheClient cacheClient = null, IMessagePublisher messagePublisher = null)
            : base(database, validator, cacheClient, messagePublisher) {}

        public virtual ICollection<T> GetByProjectId(string projectId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            return Find<T>(new MultiOptions()
                .WithProjectId(projectId)
                .WithPaging(paging)
                .WithCacheKey(useCache ? String.Concat("project:", projectId) : null)
                .WithExpiresIn(expiresIn));
        }

        public async Task RemoveAllByProjectIdsAsync(string[] projectIds) {
            await Task.Run(() => RemoveAll(new QueryOptions().WithProjectIds(projectIds)));
        }

        protected override void ConfigureClassMap(BsonClassMap<T> cm) {
            base.ConfigureClassMap(cm);
            cm.GetMemberMap(c => c.ProjectId).SetElementName(CommonFieldNames.ProjectId).SetRepresentation(BsonType.ObjectId).SetIdGenerator(new StringObjectIdGenerator());
        }

        public override void InvalidateCache(T document) {
            if (Cache == null)
                return;

            Cache.Remove(GetScopedCacheKey(String.Concat("project:", document.ProjectId)));
            base.InvalidateCache(document);
        }
    }
}