#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using Exceptionless.Models;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using MongoMigrations;

namespace Exceptionless.Core.Migrations {
    public class CreateOrganizationandProjectDocumentStats : CollectionMigration {
        public CreateOrganizationandProjectDocumentStats() : base("1.0.15", ProjectRepository.CollectionName) {
            Description = "Update Organization and Project Stats";
        }

        public override void UpdateDocument(MongoCollection<BsonDocument> collection, BsonDocument document) {
            BsonValue organizationId = document.GetValue(ProjectRepository.FieldNames.OrganizationId);
            BsonValue projectId = document.GetValue(ProjectRepository.FieldNames.Id);

            var stackCount = Database.GetCollection(StackRepository.CollectionName).FindAs<Stack>(Query.EQ(StackRepository.FieldNames.ProjectId, projectId)).Count();
            long errorCount = Database.GetCollection(EventRepository.CollectionName).FindAs<Event>(Query.EQ(EventRepository.FieldNames.ProjectId, projectId)).Count();

            document.Add(ProjectRepository.FieldNames.StackCount, new BsonInt64(stackCount));
            document.Add(ProjectRepository.FieldNames.EventCount, new BsonInt64(errorCount));
            document.Add(ProjectRepository.FieldNames.TotalEventCount, new BsonInt64(errorCount));
            collection.Save(document);

            // Update the organization.

            BsonDocument doc = Database.GetCollection(OrganizationRepository.CollectionName).FindOneById(organizationId);
            if (!doc.Contains(OrganizationRepository.FieldNames.ProjectCount))
                doc.Add(OrganizationRepository.FieldNames.ProjectCount, new BsonInt32(1));
            else {
                int count = doc.GetValue(OrganizationRepository.FieldNames.ProjectCount).AsInt32;
                doc.Set(OrganizationRepository.FieldNames.ProjectCount, count + 1);
            }

            if (!doc.Contains(OrganizationRepository.FieldNames.StackCount))
                doc.Add(OrganizationRepository.FieldNames.StackCount, new BsonInt64(stackCount));
            else {
                long count = doc.GetValue(OrganizationRepository.FieldNames.StackCount).AsInt64;
                doc.Set(OrganizationRepository.FieldNames.StackCount, count + stackCount);
            }

            if (!doc.Contains(OrganizationRepository.FieldNames.ErrorCount))
                doc.Add(OrganizationRepository.FieldNames.ErrorCount, new BsonInt64(errorCount));
            else {
                long count = doc.GetValue(OrganizationRepository.FieldNames.ErrorCount).AsInt64;
                doc.Set(OrganizationRepository.FieldNames.ErrorCount, count + errorCount);
            }

            if (!doc.Contains(OrganizationRepository.FieldNames.TotalErrorCount))
                doc.Add(OrganizationRepository.FieldNames.TotalErrorCount, new BsonInt64(errorCount));
            else {
                long count = doc.GetValue(OrganizationRepository.FieldNames.TotalErrorCount).AsInt64;
                doc.Set(OrganizationRepository.FieldNames.TotalErrorCount, count + errorCount);
            }

            Database.GetCollection(OrganizationRepository.CollectionName).Save(doc);
        }
    }
}