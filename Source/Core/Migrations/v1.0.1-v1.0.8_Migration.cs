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
using Exceptionless.Core.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using MongoMigrations;

namespace Exceptionless.Core.Migrations {
    public class AddProjectAndStackIdToDayStackStatsMigration : CollectionMigration {
        public AddProjectAndStackIdToDayStackStatsMigration()
            : base("1.0.5", DayStackStatsRepository.CollectionName) {
            Description = "Add ProjectId and ErrorStackId to day stack stats.";
        }

        public override void UpdateDocument(MongoCollection<BsonDocument> collection, BsonDocument document) {
            string id = document.GetElement(DayStackStatsRepository.FieldNames.Id).Value.AsString;

            string[] parts = id.Split('/');
            if (parts.Length != 2)
                return;

            string errorStackId = parts[0];
            var stackCollection = Database.GetCollection(ErrorStackRepository.CollectionName);
            var errorStack = stackCollection.FindOne(Query.EQ(ErrorStackRepository.FieldNames.Id, new BsonObjectId(new ObjectId(errorStackId))));

            if (errorStack != null) {
                var projectId = errorStack.GetElement(ErrorStackRepository.FieldNames.ProjectId).Value;
                document.Set(DayStackStatsRepository.FieldNames.ProjectId, projectId);
            }

            document.Set(DayStackStatsRepository.FieldNames.ErrorStackId, new BsonObjectId(new ObjectId(errorStackId)));

            var emptyBlocks = new List<BsonElement>();
            BsonDocument minuteBlocks = document.GetValue(DayStackStatsRepository.FieldNames.MinuteStats).AsBsonDocument;
            foreach (BsonElement b in minuteBlocks.Elements) {
                if (b.Value.AsInt32 == 0)
                    emptyBlocks.Add(b);
            }

            foreach (BsonElement b in emptyBlocks)
                minuteBlocks.RemoveElement(b);

            collection.Save(document);
        }
    }

    public class AddProjectAndStackIdToMonthStackStatsMigration : CollectionMigration {
        public AddProjectAndStackIdToMonthStackStatsMigration()
            : base("1.0.6", MonthStackStatsRepository.CollectionName) {
            Description = "Add ProjectId and ErrorStackId to month stack stats.";
        }

        public override void UpdateDocument(MongoCollection<BsonDocument> collection, BsonDocument document) {
            string id = document.GetElement(MonthStackStatsRepository.FieldNames.Id).Value.AsString;

            string[] parts = id.Split('/');
            if (parts.Length > 0)
                return;

            string errorStackId = parts[0];
            var stackCollection = Database.GetCollection(ErrorStackRepository.CollectionName);
            var errorStack = stackCollection.FindOne(Query.EQ(ErrorStackRepository.FieldNames.Id, new BsonObjectId(new ObjectId(errorStackId))));

            if (errorStack != null) {
                var projectId = errorStack.GetElement(ErrorStackRepository.FieldNames.ProjectId).Value;
                document.Set(MonthStackStatsRepository.FieldNames.ProjectId, projectId);
            }

            document.Set(MonthStackStatsRepository.FieldNames.ErrorStackId, new BsonObjectId(new ObjectId(errorStackId)));

            var emptyBlocks = new List<BsonElement>();
            BsonDocument dayBlocks = document.GetValue(MonthStackStatsRepository.FieldNames.DayStats).AsBsonDocument;
            foreach (BsonElement b in dayBlocks.Elements) {
                if (b.Value.AsInt32 == 0)
                    emptyBlocks.Add(b);
            }

            foreach (BsonElement b in emptyBlocks)
                dayBlocks.RemoveElement(b);

            collection.Save(document);
        }
    }

    public class AddProjectToDayProjectStatsMigration : CollectionMigration {
        public AddProjectToDayProjectStatsMigration()
            : base("1.0.7", DayProjectStatsRepository.CollectionName) {
            Description = "Add ProjectId to day project stats.";
        }

        public override void UpdateDocument(MongoCollection<BsonDocument> collection, BsonDocument document) {
            string id = document.GetElement(DayProjectStatsRepository.FieldNames.Id).Value.AsString;

            string[] parts = id.Split('/');
            if (parts.Length > 0)
                return;

            string projectId = parts[0];

            document.Set(DayProjectStatsRepository.FieldNames.ProjectId, new BsonObjectId(new ObjectId(projectId)));

            var emptyBlocks = new List<BsonElement>();
            BsonDocument minuteBlocks = document.GetValue(DayProjectStatsRepository.FieldNames.MinuteStats).AsBsonDocument;
            foreach (BsonElement b in minuteBlocks.Elements) {
                BsonDocument stats = b.Value.AsBsonDocument;
                if (stats.GetElement(DayProjectStatsRepository.FieldNames.MinuteStats_Total).Value.AsInt32 == 0)
                    emptyBlocks.Add(b);
            }

            foreach (BsonElement b in emptyBlocks)
                minuteBlocks.RemoveElement(b);

            collection.Save(document);
        }
    }

    public class AddProjectToMonthProjectStatsMigration : CollectionMigration {
        public AddProjectToMonthProjectStatsMigration()
            : base("1.0.8", MonthProjectStatsRepository.CollectionName) {
            Description = "Add ProjectId to month project stats.";
        }

        public override void UpdateDocument(MongoCollection<BsonDocument> collection, BsonDocument document) {
            string id = document.GetElement(MonthProjectStatsRepository.FieldNames.Id).Value.AsString;

            string[] parts = id.Split('/');
            if (parts.Length != 3)
                return;

            string projectId = parts[0];

            document.Set(MonthProjectStatsRepository.FieldNames.ProjectId, new BsonObjectId(new ObjectId(projectId)));

            var emptyBlocks = new List<BsonElement>();
            BsonDocument dayBlocks = document.GetValue(MonthProjectStatsRepository.FieldNames.DayStats).AsBsonDocument;
            foreach (BsonElement b in dayBlocks.Elements) {
                BsonDocument stats = b.Value.AsBsonDocument;
                if (stats.GetElement(MonthProjectStatsRepository.FieldNames.DayStats_Total).Value.AsInt32 == 0)
                    emptyBlocks.Add(b);
            }

            foreach (BsonElement b in emptyBlocks)
                dayBlocks.RemoveElement(b);

            collection.Save(document);
        }
    }
}