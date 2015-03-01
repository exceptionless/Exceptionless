#region Copyright 2015 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

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