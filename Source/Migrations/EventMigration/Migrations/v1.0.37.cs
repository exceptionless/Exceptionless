using System;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using MongoMigrations;

namespace Exceptionless.EventMigration.Migrations {
    public class EnsureWebhookOrganizationIsSetMigration : CollectionMigration {
        public EnsureWebhookOrganizationIsSetMigration() : base("1.0.37", "webhook") {
            Description = "Ensure the webhook organization id is set to the current organization.";
            IsSafeToRunMultipleTimes = true;
        }

        public override void UpdateDocument(MongoCollection<BsonDocument> collection, BsonDocument document) {
            if (document.Contains("oid")) {
                var organizationId = document.GetValue("oid").AsNullableObjectId;
                if (!organizationId.HasValue || organizationId.Value == ObjectId.Empty) {
                    document.Remove("oid");
                } else {
                    var organizationCollection = Database.GetCollection("organization");
                    var organization = organizationCollection.FindOneById(organizationId);

                    // The organization with this id could not be found.. Remove the webhook.
                    if (organization == null) {
                        collection.Remove(Query.EQ("_id", document.GetDocumentId()));
                        return;
                    }
                }
            }

            var projectCollection = Database.GetCollection("project");
            if (document.Contains("pid")) {
                var projectId = document.GetValue("pid").AsNullableObjectId;
                if (!projectId.HasValue || projectId.Value == ObjectId.Empty) {
                    document.Remove("pid");
                } else {
                    var project = projectCollection.FindOneById(projectId);

                    // The project with this id could not be found.. Remove the webhook.
                    if (project == null) {
                        collection.Remove(Query.EQ("_id", document.GetDocumentId()));
                        return;
                    }

                    if (!document.Contains("oid"))
                        document.Set("oid", project.GetValue("oid").AsObjectId);
                }
            }

            if (!document.Contains("oid") && !document.Contains("pid")) {
                collection.Remove(Query.EQ("_id", document.GetDocumentId()));
                return;
            }

            collection.Save(document);
        }
    }
}