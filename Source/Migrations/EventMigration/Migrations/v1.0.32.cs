using System;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Models;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoMigrations;

namespace Exceptionless.Core.Migrations {
    public class OrganizationConversionMigration : CollectionMigration {
        public OrganizationConversionMigration(): base("1.0.32", "organization") {
            Description = "Rename various organization fields";
            IsSafeToRunMultipleTimes = true;
        }

        public override void UpdateDocument(MongoCollection<BsonDocument> collection, BsonDocument document) {
            if (document.Contains("ErrorCount"))
                document.ChangeName("ErrorCount", "EventCount");

            if (document.Contains("TotalErrorCount"))
                document.ChangeName("TotalErrorCount", "TotalEventCount");

            if (document.Contains("LastErrorDate"))
                document.ChangeName("LastErrorDate", "LastEventDate");

            if (document.Contains("MaxErrorsPerDay")) {
                int maxErrorsPerDay = -1;
                var maxErrorsPerDayElement = document.GetValue("MaxErrorsPerDay");
                if (maxErrorsPerDayElement.IsInt32)
                    maxErrorsPerDay = maxErrorsPerDayElement.AsInt32;
                else if (maxErrorsPerDayElement.IsInt64)
                    maxErrorsPerDay = (int)maxErrorsPerDayElement.AsInt64;

                document.Set("MaxEventsPerMonth", maxErrorsPerDay > 0 ? maxErrorsPerDay * 30 : -1);
                document.Remove("MaxErrorsPerDay");
            }

            if (document.Contains("MaxErrorsPerMonth"))
                document.ChangeName("MaxErrorsPerMonth", "MaxEventsPerMonth");

            if (document.Contains("SuspensionCode")) {
                var value = document.GetValue("SuspensionCode");
                document.Remove("SuspensionCode");

                SuspensionCode suspensionCode;
                if (value.IsString && Enum.TryParse(value.AsString, true, out suspensionCode))
                    document.Set("SuspensionCode", suspensionCode);
            }

            if (document.Contains("PlanId")) {
                string planId = document.GetValue("PlanId").AsString;
                var currentPlan = BillingManager.GetBillingPlan(planId);

                document.Set("PlanName", currentPlan != null ? currentPlan.Name : planId);
                document.Set("PlanDescription", currentPlan != null ? currentPlan.Description : planId);
            }

            collection.Save(document);
        }
    }
}