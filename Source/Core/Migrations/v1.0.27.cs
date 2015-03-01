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