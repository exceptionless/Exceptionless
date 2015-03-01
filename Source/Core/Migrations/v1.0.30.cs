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