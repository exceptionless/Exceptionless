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
    public class VerifyEmailAddressForExistingUsers : CollectionMigration {
        public VerifyEmailAddressForExistingUsers() : base("1.0.17", UserRepository.CollectionName) {
            Description = "Verify Email Address for existing users.";
        }

        public override void UpdateDocument(MongoCollection<BsonDocument> collection, BsonDocument document) {
            if (!document.Contains(UserRepository.FieldNames.IsEmailAddressVerified))
                document.Add(UserRepository.FieldNames.IsEmailAddressVerified, new BsonBoolean(true));
            else
                document.Set(UserRepository.FieldNames.IsEmailAddressVerified, new BsonBoolean(true));

            collection.Save(document);
        }
    }
}