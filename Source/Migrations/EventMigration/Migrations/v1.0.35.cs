using System;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoMigrations;

namespace Exceptionless.Core.Migrations {
    public class UpdateWebhookEventTypesAndVersionMigration : CollectionMigration {
        public UpdateWebhookEventTypesAndVersionMigration() : base("1.0.35", "webhook") {
            Description = "Change EventType names and add a webhook version";
            IsSafeToRunMultipleTimes = true;
        }

        public override void UpdateDocument(MongoCollection<BsonDocument> collection, BsonDocument document) {
            BsonValue value;
            if (document.TryGetValue("EventTypes", out value) && value.IsBsonArray) {
                var types = value.AsBsonArray;
                if (types.Contains(new BsonString("ErrorRegression"))) {
                    types.Remove(new BsonString("ErrorRegression"));
                    types.Add(new BsonString("StackRegression"));
                    document.Set("EventTypes", types);
                }
            }

            if (!document.Contains("Version"))
                document.Set("Version", "1.0");

            collection.Save(document);
        }
    }
}