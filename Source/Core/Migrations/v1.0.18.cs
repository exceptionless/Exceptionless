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
using Exceptionless.Models;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoMigrations;

namespace Exceptionless.Core.Migrations {
    public class UpgradeOrganizationsToFreePlanWithHighRetentionLimits : CollectionMigration {
        public UpgradeOrganizationsToFreePlanWithHighRetentionLimits() : base("1.0.18", OrganizationRepository.CollectionName) {
            Description = "Upgrade Organizations To Free Plan With High Retention Limits";
        }

        public override void UpdateDocument(MongoCollection<BsonDocument> collection, BsonDocument document) {
            if (!document.Contains(OrganizationRepository.FieldNames.PlanId))
                document.Add(OrganizationRepository.FieldNames.PlanId, new BsonString(BillingManager.FreePlan.Id));
            else
                document.Set(OrganizationRepository.FieldNames.PlanId, new BsonString(BillingManager.FreePlan.Id));

            if (!document.Contains(OrganizationRepository.FieldNames.BillingStatus))
                document.Add(OrganizationRepository.FieldNames.BillingStatus, new BsonInt32((int)BillingStatus.Active));
            else
                document.Set(OrganizationRepository.FieldNames.BillingStatus, new BsonInt32((int)BillingStatus.Active));

            if (!document.Contains(OrganizationRepository.FieldNames.MaxUsers))
                document.Add(OrganizationRepository.FieldNames.MaxUsers, new BsonInt32(BillingManager.FreePlan.MaxUsers));
            else
                document.Set(OrganizationRepository.FieldNames.MaxUsers, new BsonInt32(BillingManager.FreePlan.MaxUsers));

            if (!document.Contains(OrganizationRepository.FieldNames.MaxProjects))
                document.Add(OrganizationRepository.FieldNames.MaxProjects, new BsonInt32(BillingManager.FreePlan.MaxProjects));
            else
                document.Set(OrganizationRepository.FieldNames.MaxProjects, new BsonInt32(BillingManager.FreePlan.MaxProjects));

            if (!document.Contains(OrganizationRepository.FieldNames.RetentionDays))
                document.Add(OrganizationRepository.FieldNames.RetentionDays, new BsonInt64(BillingManager.MediumPlan.RetentionDays));
            else
                document.Set(OrganizationRepository.FieldNames.RetentionDays, new BsonInt64(BillingManager.MediumPlan.RetentionDays));

            if (!document.Contains(OrganizationRepository.FieldNames.MaxErrorsPerDay))
                document.Add(OrganizationRepository.FieldNames.MaxErrorsPerDay, new BsonInt64(BillingManager.FreePlan.MaxErrorsPerDay));
            else
                document.Set(OrganizationRepository.FieldNames.MaxErrorsPerDay, new BsonInt64(BillingManager.FreePlan.MaxErrorsPerDay));

            if (!document.Contains(OrganizationRepository.FieldNames.HasPremiumFeatures))
                document.Add(OrganizationRepository.FieldNames.HasPremiumFeatures, new BsonBoolean(BillingManager.FreePlan.HasPremiumFeatures));
            else
                document.Set(OrganizationRepository.FieldNames.HasPremiumFeatures, new BsonBoolean(BillingManager.FreePlan.HasPremiumFeatures));

            if (!document.Contains(OrganizationRepository.FieldNames.OverageDays))
                document.Add(OrganizationRepository.FieldNames.OverageDays, new BsonArray());
            else
                document.Set(OrganizationRepository.FieldNames.OverageDays, new BsonArray());

            collection.Save(document);
        }
    }
}