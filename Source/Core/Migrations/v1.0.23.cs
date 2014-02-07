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
    public class UpgradeOrganizationWithBillingPrice : CollectionMigration {
        public UpgradeOrganizationWithBillingPrice() : base("1.0.23", OrganizationRepository.CollectionName) {
            Description = "Populate the Organization with the proper billing price.";
        }

        public override void UpdateDocument(MongoCollection<BsonDocument> collection, BsonDocument document) {
            decimal price = 0m;
            if (!document.Contains(OrganizationRepository.FieldNames.PlanId))
                return;

            switch (document.GetValue(OrganizationRepository.FieldNames.PlanId).AsString) {
                case "EX_SMALL":
                    price = BillingManager.SmallPlan.Price;
                    break;
                case "EX_MEDIUM":
                    price = BillingManager.MediumPlan.Price;
                    break;
                case "EX_LARGE":
                    price = BillingManager.LargePlan.Price;
                    break;
            }

            if (price == 0m)
                return;

            if (!document.Contains(OrganizationRepository.FieldNames.BillingPrice))
                document.Add(OrganizationRepository.FieldNames.BillingPrice, new BsonString(price.ToString()));
            else
                document.Set(OrganizationRepository.FieldNames.BillingPrice, new BsonString(price.ToString()));

            collection.Save(document);
        }
    }
}