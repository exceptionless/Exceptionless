using System;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoMigrations;

namespace Exceptionless.Core.Migrations {
    public class ErrorOccurrenceDateLocalToUtcMigration : CollectionMigration {
        public ErrorOccurrenceDateLocalToUtcMigration() : base("1.0.27", "error") {
            Description = "Change occurrence date ticks to be stored in utc ticks.";
        }

        public override void UpdateDocument(MongoCollection<BsonDocument> collection, BsonDocument document) {
            if (!document.Contains("dt"))
                return;

            var occurrenceDateArray = document.GetValue("dt").AsBsonArray;
            var localTicks = occurrenceDateArray[0].AsInt64;
            var date = new DateTime(localTicks);
            if (date > new DateTime(2015, 3, 14, 12, 30, 0))
                return;

            var offset = TimeSpan.FromMinutes(occurrenceDateArray[1].AsInt32);
            occurrenceDateArray[0] = localTicks + -offset.Ticks;

            document.Set("dt", occurrenceDateArray);

            collection.Save(document);
        }
    }
}