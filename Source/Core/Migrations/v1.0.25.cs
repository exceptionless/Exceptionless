#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using Exceptionless.Core.Billing;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoMigrations;

namespace Exceptionless.Core.Migrations {
    public class UpgradeOrganizationsToFreePlanWithOriginalRetentionLimits : CollectionMigration {
        public UpgradeOrganizationsToFreePlanWithOriginalRetentionLimits() : base("1.0.25", OrganizationRepository.CollectionName) {
            Description = "Upgrade Organizations To Free Plan With Original Retention Limits";
        }

        public override void UpdateDocument(MongoCollection<BsonDocument> collection, BsonDocument document) {
            if (!document.Contains(OrganizationRepository.FieldNames.PlanId))
                return;

            string planId = document.GetValue(OrganizationRepository.FieldNames.PlanId).AsString;
            if (String.IsNullOrEmpty(planId) || !String.Equals(planId, BillingManager.FreePlan.Id))
                return;

            if (!document.Contains(OrganizationRepository.FieldNames.RetentionDays))
                document.Add(OrganizationRepository.FieldNames.RetentionDays, new BsonInt64(BillingManager.FreePlan.RetentionDays));
            else
                document.Set(OrganizationRepository.FieldNames.RetentionDays, new BsonInt64(BillingManager.FreePlan.RetentionDays));

            collection.Save(document);
        }
    }
}