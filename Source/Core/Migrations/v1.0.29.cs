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
    public class EventConversionMigration : CollectionMigration {
        public EventConversionMigration()
            : base("1.0.29", StackRepository.CollectionName) {
            Description = "Update the system to use more generic events instead of errors.";
        }

        public override void Update() {
            // TODO: rename errorstack collection to stack
            // TODO: update field names in org and project for counts
            // TODO: rename error collection to event
            // TODO: migrate errors to events on demand as documents are requested

            base.Update();
        }

        public override void UpdateDocument(MongoCollection<BsonDocument> collection, BsonDocument document) {
        }
    }
}