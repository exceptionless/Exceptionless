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
    public class ConvertEmailAddressesToLowerCaseMigration : CollectionMigration {
        public ConvertEmailAddressesToLowerCaseMigration() : base("1.0.34", "user") {
            Description = "Convert email addresses to lower case and remove any duplicate properties.";
            IsSafeToRunMultipleTimes = true;
        }

        public override void UpdateDocument(MongoCollection<BsonDocument> collection, BsonDocument document) {
            if (document.Contains("EmailNotificationsEnabled")) {
                bool emailNotificationsEnabled = document.GetValue("EmailNotificationsEnabled").AsBoolean;
                while (document.Contains("EmailNotificationsEnabled"))
                    document.Remove("EmailNotificationsEnabled");

                document.Set("EmailNotificationsEnabled", emailNotificationsEnabled);
            }

            if (document.Contains("EmailAddress")) {
                string emailAddress = document.GetValue("EmailAddress").AsString;
                if (!String.IsNullOrWhiteSpace(emailAddress))
                    document.Set("EmailAddress", emailAddress.ToLowerInvariant().Trim());
            }

            collection.Save(document);
        }
    }
}