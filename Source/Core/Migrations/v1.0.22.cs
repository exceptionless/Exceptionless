#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using MongoMigrations;

namespace Exceptionless.Core.Migrations {
    public class EnableDailyNotificationsForEveryUser : CollectionMigration {
        public EnableDailyNotificationsForEveryUser() : base("1.0.22", ProjectRepository.CollectionName) {
            Description = "Enable Daily Notifications For Every User";
        }

        public override void UpdateDocument(MongoCollection<BsonDocument> collection, BsonDocument document) {
            ObjectId organizationId = document.GetValue(ProjectRepository.FieldNames.OrganizationId).AsObjectId;
            var users = Database.GetCollection(UserRepository.CollectionName)
                .Find(Query.In(UserRepository.FieldNames.OrganizationIds, new List<BsonValue> {
                    organizationId
                }))
                .SetFields(ProjectRepository.FieldNames.Id).ToList();

            if (!document.Contains(ProjectRepository.FieldNames.NotificationSettings))
                document.Add(ProjectRepository.FieldNames.NotificationSettings, new BsonDocument());

            BsonDocument settings = document.GetValue(ProjectRepository.FieldNames.NotificationSettings).AsBsonDocument;
            foreach (var user in users) {
                var userId = user.GetValue(ProjectRepository.FieldNames.Id).AsObjectId.ToString();
                if (!settings.Contains(userId))
                    settings.Add(userId, new BsonDocument());

                var userSettings = settings.GetValue(userId).AsBsonDocument;
                if (!userSettings.Contains("SendDailySummary"))
                    userSettings.Add("SendDailySummary", new BsonBoolean(true));
                else
                    userSettings.Set("SendDailySummary", new BsonBoolean(true));
            }

            collection.Save(document);
        }
    }
}