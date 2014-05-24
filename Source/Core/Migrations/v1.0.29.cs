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
    public class UpdateOveragesImplementationMigration : CollectionMigration {
        public UpdateOveragesImplementationMigration()
            : base("1.0.29", OrganizationRepository.CollectionName) {
            Description = "Update property names and new plan limit implementation for orgs docs.";
        }

        public override void UpdateDocument(MongoCollection<BsonDocument> collection, BsonDocument document) {
            if (document.ChangeName("OverageDays", OrganizationRepository.FieldNames.OverageHours)) {
                var overageHours = document.GetElement(OrganizationRepository.FieldNames.OverageHours).Value;
                foreach (var overage in overageHours.AsBsonArray)
                    overage.AsBsonDocument.ChangeName("Day", "Date");
            }

            document.ChangeName("MaxErrorsPerDay", OrganizationRepository.FieldNames.MaxErrorsPerMonth);
            var maxErrorsPerMonth = document.GetValue(OrganizationRepository.FieldNames.MaxErrorsPerMonth).AsInt32;
            if (maxErrorsPerMonth > 0)
                document.Set(OrganizationRepository.FieldNames.MaxErrorsPerMonth, maxErrorsPerMonth * 30);
        }
    }
}