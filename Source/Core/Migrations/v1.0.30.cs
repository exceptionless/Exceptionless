using System;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoMigrations;

namespace Exceptionless.Core.Migrations {
    public class RemoveExistingUsageCountsMigration : CollectionMigration {
        public RemoveExistingUsageCountsMigration() : base("1.0.30", "organization") {
            Description = "Remove existing usage counts from organizations";
            IsSafeToRunMultipleTimes = true;
        }

        public override void UpdateDocument(MongoCollection<BsonDocument> collection, BsonDocument document) {
            if (document.Contains("OverageHours"))
                document.Remove("OverageHours");

            if (document.Contains("Usage"))
                document.Remove("Usage");

            collection.Save(document);
        }
    }
}