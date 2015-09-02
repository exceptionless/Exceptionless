using System;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoMigrations;

namespace Exceptionless.Core.Migrations {
    public class CollectionNameConversionMigration : CollectionMigration {
        public CollectionNameConversionMigration() : base("1.0.33", "project.hook") {
            Description = "Rename project.hook collection to webhook.";
            IsSafeToRunMultipleTimes = true;
        }

        public override void Update() {
            if (Database.CollectionExists("webhook"))
                Database.DropCollection("webhook");

            if (Database.CollectionExists("project.hook"))
                Database.RenameCollection("project.hook", "webhook");
            
            base.Update();
        }

        public override void UpdateDocument(MongoCollection<BsonDocument> collection, BsonDocument document) {}
    }
}