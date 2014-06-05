using System;
using Exceptionless.Core.Caching;
using Exceptionless.Core.Messaging;
using Exceptionless.Models.Admin;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace Exceptionless.Core.Repositories {
    public class ApplicationRepository  : MongoRepositoryOwnedByOrganization<Application>, IApplicationRepository {
        public ApplicationRepository(MongoDatabase database, ICacheClient cacheClient = null, IMessagePublisher messagePublisher = null)
            : base(database, cacheClient, messagePublisher) {
            _getIdValue = s => s;
        }

        #region Collection Setup

        public const string CollectionName = "token";

        public static class FieldNames {
            public const string Id = CommonFieldNames.Id;
            public const string OrganizationId = CommonFieldNames.OrganizationId;
            public const string Secret = "scrt";
            public const string Name = "name";
            public const string Url = "url";
            public const string Description = "desc";
            public const string CallbackUrl = "curl";
            public const string ImageUrl = "iurl";
        }

        protected override string GetCollectionName() {
            return CollectionName;
        }

        protected override void ConfigureClassMap(BsonClassMap<Application> cm) {
            //base.ConfigureClassMap(cm);
            cm.AutoMap();
            cm.SetIgnoreExtraElements(true);
            cm.SetIdMember(cm.GetMemberMap(c => c.Id));
            cm.GetMemberMap(c => c.OrganizationId).SetElementName(FieldNames.OrganizationId).SetRepresentation(BsonType.ObjectId).SetIgnoreIfNull(true);
            cm.GetMemberMap(c => c.Secret).SetElementName(FieldNames.Secret).SetIgnoreIfNull(true);
            cm.GetMemberMap(c => c.Name).SetElementName(FieldNames.Name).SetIgnoreIfNull(true);
            cm.GetMemberMap(c => c.Url).SetElementName(FieldNames.Url).SetIgnoreIfNull(true);
            cm.GetMemberMap(c => c.Description).SetElementName(FieldNames.Description).SetIgnoreIfNull(true);
            cm.GetMemberMap(c => c.CallbackUrl).SetElementName(FieldNames.CallbackUrl).SetIgnoreIfNull(true);
            cm.GetMemberMap(c => c.ImageUrl).SetElementName(FieldNames.ImageUrl).SetIgnoreIfNull(true);
        }

        #endregion
    }
}