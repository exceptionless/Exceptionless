using System;
using Exceptionless.Core.Models.Admin;
using FluentValidation;
using Foundatio.Caching;
using Foundatio.Messaging;
using MongoDB.Driver;

namespace Exceptionless.Core.Repositories {
    public class ApplicationRepository  : MongoRepositoryOwnedByOrganization<Application>, IApplicationRepository {
        public ApplicationRepository(MongoDatabase database, IValidator<Application> validator = null, ICacheClient cacheClient = null, IMessagePublisher messagePublisher = null)
            : base(database, validator, cacheClient, messagePublisher) {
            _getIdValue = s => s;
        }

        #region Collection Setup

        public const string CollectionName = "application";

        private static class FieldNames {
            public const string Id = CommonFieldNames.Id;
            public const string OrganizationId = CommonFieldNames.OrganizationId;
        }

        protected override string GetCollectionName() {
            return CollectionName;
        }

        #endregion
    }
}