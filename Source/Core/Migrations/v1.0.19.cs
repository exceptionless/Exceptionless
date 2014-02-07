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
    public class RemoveDeletedOrganizationFields : CollectionMigration {
        public RemoveDeletedOrganizationFields() : base("1.0.19", OrganizationRepository.CollectionName) {
            Description = "Remove Deleted Organization Fields";
        }

        public override void UpdateDocument(MongoCollection<BsonDocument> collection, BsonDocument document) {
            if (document.Contains("MaxErrors"))
                document.Remove("MaxErrors");

            if (document.Contains("MaxErrorsPerStack"))
                document.Remove("MaxErrorsPerStack");

            collection.Save(document);
        }
    }
}