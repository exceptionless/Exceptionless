using System;
using System.Collections.Generic;
using Exceptionless.Core.Models.Admin;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoMigrations;

namespace Exceptionless.Core.Migrations {
    public class ProjectConversionMigration : CollectionMigration {
        public ProjectConversionMigration() : base("1.0.31", "project") {
            Description = "Migrate ApiKeys to the token repository and rename various project fields.";
            IsSafeToRunMultipleTimes = true;
        }

        public override void Update() {
            if (Database.CollectionExists("token"))
                Database.DropCollection("token");

            var projectCollection = GetCollection();
            if (projectCollection.IndexExists("ApiKeys"))
                projectCollection.DropIndex("ApiKeys");

            base.Update();
        }

        public override void UpdateDocument(MongoCollection<BsonDocument> collection, BsonDocument document) {
            if (document.Contains("ErrorCount"))
                document.ChangeName("ErrorCount", "EventCount");

            if (document.Contains("TotalErrorCount"))
                document.ChangeName("TotalErrorCount", "TotalEventCount");

            if (document.Contains("LastErrorDate"))
                document.ChangeName("LastErrorDate", "LastEventDate");

            BsonValue value;
            if (document.TryGetValue("ApiKeys", out value) && value.IsBsonArray) {
                if (!collection.Database.CollectionExists("token"))
                    collection.Database.CreateCollection("token");

                var projectId = document.GetValue("_id").AsObjectId;

                var tokenCollection = Database.GetCollection("token");
                var tokens = new List<BsonDocument>();
                foreach (var key in value.AsBsonArray) {
                    var token = new BsonDocument();
                    token.Set("_id", key);
                    token.Set("oid", new BsonObjectId(document.GetValue("oid").AsObjectId));
                    token.Set("pid", new BsonObjectId(projectId));
                    token.Set("typ", TokenType.Access);
                    token.Set("scp", new BsonArray(new[] { "client" }));
                    token.Set("exp", DateTime.UtcNow.AddYears(100));
                    token.Set("not", "Client api key");
                    token.Set("dt", projectId.CreationTime.ToUniversalTime());
                    token.Set("mdt", DateTime.UtcNow);

                    tokens.Add(token);
                }

                if (tokens.Count > 0)
                    tokenCollection.InsertBatch(tokens);
            }

            document.Remove("ApiKeys");

            if (document.TryGetValue("NotificationSettings", out value) && value.IsBsonDocument) {
                var settings = new BsonDocument();
                foreach (BsonElement element in value.AsBsonDocument) {
                    if (String.IsNullOrEmpty(element.Name) || !element.Value.IsBsonDocument)
                        continue;

                    var userSettings = element.Value.AsBsonDocument;

                    bool isNew = false;
                    if (userSettings.Contains("Mode")) {
                        isNew = userSettings.GetValue("Mode").AsInt32 == 1;
                        userSettings.Remove("Mode");
                    }

                    userSettings.Set("ReportNewErrors", new BsonBoolean(isNew));

                    if (userSettings.Contains("ReportRegressions"))
                        userSettings.ChangeName("ReportRegressions", "ReportEventRegressions");

                    if (userSettings.Contains("Report404Errors"))
                        userSettings.Remove("Report404Errors");

                    if (userSettings.Contains("ReportKnownBotErrors"))
                        userSettings.Remove("ReportKnownBotErrors");

                    settings.Set(element.Name, userSettings);
                }

                document.Set("NotificationSettings", settings);
            }

            collection.Save(document);
        }
    }
}