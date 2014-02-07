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
    public class AddNextDailySummaryTicks : CollectionMigration {
        public AddNextDailySummaryTicks() : base("1.0.21", ProjectRepository.CollectionName) {
            Description = "Add Next Daily Summary Ticks.";
        }

        public override void UpdateDocument(MongoCollection<BsonDocument> collection, BsonDocument document) {
            string timeZone = document.GetValue(ProjectRepository.FieldNames.TimeZone).AsString;
            TimeZoneInfo tzi;
            try {
                tzi = TimeZoneInfo.FindSystemTimeZoneById(timeZone);
            } catch {
                tzi = TimeZoneInfo.Local;
            }

            if (!document.Contains(ProjectRepository.FieldNames.NextSummaryEndOfDayTicks))
                document.Add(ProjectRepository.FieldNames.NextSummaryEndOfDayTicks, new BsonInt64(TimeZoneInfo.ConvertTime(DateTime.Today.AddDays(1), tzi).ToUniversalTime().Ticks));
            else
                document.Set(ProjectRepository.FieldNames.NextSummaryEndOfDayTicks, new BsonInt64(TimeZoneInfo.ConvertTime(DateTime.Today.AddDays(1), tzi).ToUniversalTime().Ticks));

            collection.Save(document);
        }
    }
}