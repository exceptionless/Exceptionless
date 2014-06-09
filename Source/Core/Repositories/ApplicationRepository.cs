using System;
using Exceptionless.Core.Caching;
using Exceptionless.Core.Messaging;
using Exceptionless.Models.Admin;
using MongoDB.Driver;

namespace Exceptionless.Core.Repositories {
    public class ApplicationRepository  : MongoRepositoryOwnedByOrganization<Application>, IApplicationRepository {
        public ApplicationRepository(MongoDatabase database, ICacheClient cacheClient = null, IMessagePublisher messagePublisher = null)
            : base(database, cacheClient, messagePublisher) {
            _getIdValue = s => s;
        }

        #region Collection Setup

        public const string CollectionName = "application";

        public static class FieldNames {
            public const string Id = CommonFieldNames.Id;
            public const string OrganizationId = CommonFieldNames.OrganizationId;
        }

        protected override string GetCollectionName() {
            return CollectionName;
        }

        #endregion
    }
}