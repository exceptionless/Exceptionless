using System;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoMigrations;

namespace Exceptionless.EventMigration.Migrations {
    public class UpdateWebhookVersionAndFieldNamesMigration : CollectionMigration {
        public UpdateWebhookVersionAndFieldNamesMigration() : base("1.0.36", "webhook") {
            Description = "Change webhook version and field names";
            IsSafeToRunMultipleTimes = true;
        }

        public override void UpdateDocument(MongoCollection<BsonDocument> collection, BsonDocument document) {
            if (document.Contains("OrganizationId"))
                document.ChangeName("OrganizationId", "oid");
            
            if (document.Contains("ProjectId"))
                document.ChangeName("ProjectId", "pid");

            if (document.Contains("Version"))
                document.Remove("Version");

            document.Set("Version", "1.0.0.0");

            collection.Save(document);
        }
    }
}