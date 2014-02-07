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
    public class DropUserNameColumnAndIndex : CollectionMigration {
        public DropUserNameColumnAndIndex() : base("1.0.16", UserRepository.CollectionName) {
            Description = "Drop username column from user repository.";
        }

        public override void UpdateDocument(MongoCollection<BsonDocument> collection, BsonDocument document) {
            if (collection.IndexExistsByName("Username_1"))
                collection.DropIndexByName("Username_1");

            if (document.Contains("Username"))
                document.Remove("Username");

            collection.Save(document);
        }
    }
}