#region Copyright 2014 Exceptionless

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
    public class CollectionNameConversionMigration : CollectionMigration {
        public CollectionNameConversionMigration() : base("1.0.33", "token") {
            Description = "Rename various errorstack collection to stack and project.hook collection to webhook.";
        }

        public override void Update() {
            if (Database.CollectionExists("project.hook"))
                Database.RenameCollection("project.hook", "webhook");
            
            base.Update();
        }

        public override void UpdateDocument(MongoCollection<BsonDocument> collection, BsonDocument document) {
            // TODO: We should be able to tell the upgrader to skip this step.
        }
    }
}