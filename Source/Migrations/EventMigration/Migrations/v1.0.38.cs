using System;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using MongoMigrations;

namespace Exceptionless.Core.Migrations {
    public class RemoveInvalidTokensMigration : CollectionMigration {
        public RemoveInvalidTokensMigration() : base("1.0.38", "token") {
            Description = "Ensure all current tokens are valid.";
            IsSafeToRunMultipleTimes = true;
        }

        public override void UpdateDocument(MongoCollection<BsonDocument> collection, BsonDocument document) {
            if (document.Contains("oid")) {
                var organizationId = document.GetValue("oid").AsNullableObjectId;
                if (!organizationId.HasValue || organizationId.Value == ObjectId.Empty) {
                    // The organization id is invalid. Remove the token.
                    collection.Remove(Query.EQ("_id", document.GetDocumentId()));
                    return;
                }

                var organizationCollection = Database.GetCollection("organization");
                var organization = organizationCollection.FindOneById(organizationId.Value);

                // The organization with this id could not be found.. Remove the token.
                if (organization == null) {
                    collection.Remove(Query.EQ("_id", document.GetDocumentId()));
                    return;
                }
            }

            if (document.Contains("pid")) {
                var projectId = document.GetValue("pid").AsNullableObjectId;
                if (!projectId.HasValue || projectId.Value == ObjectId.Empty) {
                    // The project id is invalid. Remove the token.
                    collection.Remove(Query.EQ("_id", document.GetDocumentId()));
                    return;
                }

                var projectCollection = Database.GetCollection("project");
                var project = projectCollection.FindOneById(projectId.Value);

                // The project with this id could not be found.. Remove the token.
                if (project == null) {
                    collection.Remove(Query.EQ("_id", document.GetDocumentId()));
                    return;
                }
            }

            // Remove the token if it's not associated to an organization, project or user.
            if (!document.Contains("oid") && !document.Contains("pid") && !document.Contains("uid")) {
                collection.Remove(Query.EQ("_id", document.GetDocumentId()));
            }
        }
    }
}