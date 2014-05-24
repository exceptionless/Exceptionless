#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Linq;
using Exceptionless.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using ServiceStack.CacheAccess;

namespace Exceptionless.Core {
    public class OrganizationRepository : MongoRepositoryWithIdentity<Organization>, IOrganizationRepository {
        public OrganizationRepository(MongoDatabase database, ICacheClient cacheClient = null) : base(database, cacheClient) {}

        public const string CollectionName = "organization";

        protected override string GetCollectionName() {
            return CollectionName;
        }

        public new static class FieldNames {
            public const string Id = "_id";
            public const string Name = "Name";
            public const string StripeCustomerId = "StripeCustomerId";
            public const string PlanId = "PlanId";
            public const string CardLast4 = "CardLast4";
            public const string SubscribeDate = "SubscribeDate";
            public const string BillingChangeDate = "BillingChangeDate";
            public const string BillingChangedByUserId = "BillingChangedByUserId";
            public const string BillingStatus = "BillingStatus";
            public const string BillingPrice = "BillingPrice";
            public const string IsSuspended = "IsSuspended";
            public const string SuspensionCode = "SuspensionCode";
            public const string SuspensionNotes = "SuspensionNotes";
            public const string SuspensionDate = "SuspensionDate";
            public const string SuspendedByUserId = "SuspendedByUserId";
            public const string RetentionDays = "RetentionDays";
            public const string HasPremiumFeatures = "HasPremiumFeatures";
            public const string MaxUsers = "MaxUsers";
            public const string MaxErrorsPerMonth = "MaxErrorsPerMonth";
            public const string MaxProjects = "MaxProjects";
            public const string ProjectCount = "ProjectCount";
            public const string StackCount = "StackCount";
            public const string ErrorCount = "ErrorCount";
            public const string TotalErrorCount = "TotalErrorCount";
            public const string LastErrorDate = "LastErrorDate";
            public const string Invites = "Invites";
            public const string Invites_Token = "Invites.Token";
            public const string Invites_EmailAddress = "Invites.EmailAddress";
            public const string Invites_DateAdded = "Invites.DateAdded";
            public const string OverageHours = "OverageHours";
        }

        protected override void InitializeCollection(MongoCollection<Organization> collection) {
            base.InitializeCollection(collection);

            collection.CreateIndex(IndexKeys.Ascending(FieldNames.Invites_Token), IndexOptions.SetBackground(true));
            collection.CreateIndex(IndexKeys.Ascending(FieldNames.Invites_EmailAddress), IndexOptions.SetBackground(true));
            collection.CreateIndex(IndexKeys.Ascending(FieldNames.StripeCustomerId), IndexOptions.SetUnique(true).SetSparse(true).SetBackground(true));
        }

        protected override void ConfigureClassMap(BsonClassMap<Organization> cm) {
            base.ConfigureClassMap(cm);
            cm.GetMemberMap(c => c.StripeCustomerId).SetElementName(FieldNames.StripeCustomerId).SetIgnoreIfNull(true);
            cm.GetMemberMap(c => c.PlanId).SetElementName(FieldNames.PlanId).SetIgnoreIfNull(true);
            cm.GetMemberMap(c => c.CardLast4).SetElementName(FieldNames.CardLast4).SetIgnoreIfNull(true);
            cm.GetMemberMap(c => c.SubscribeDate).SetElementName(FieldNames.SubscribeDate).SetIgnoreIfNull(true);
            cm.GetMemberMap(c => c.BillingChangeDate).SetElementName(FieldNames.BillingChangeDate).SetIgnoreIfNull(true);
            cm.GetMemberMap(c => c.BillingChangedByUserId).SetElementName(FieldNames.BillingChangedByUserId).SetIgnoreIfNull(true);
            cm.GetMemberMap(c => c.OverageHours).SetElementName(FieldNames.OverageHours).SetIgnoreIfNull(true);
        }

        public Organization GetByInviteToken(string token, out Invite invite) {
            invite = null;
            if (String.IsNullOrEmpty(token))
                return null;

            Organization organization = Where(Query.EQ(FieldNames.Invites_Token, new BsonString(token))).FirstOrDefault();
            if (organization != null)
                invite = organization.Invites.FirstOrDefault(i => String.Equals(i.Token, token, StringComparison.OrdinalIgnoreCase));

            return organization;
        }

        public Organization GetByStripeCustomerId(string customerId) {
            return String.IsNullOrEmpty(customerId) ? null : Where(Query.EQ(FieldNames.StripeCustomerId, new BsonString(customerId))).FirstOrDefault();
        }

        public void IncrementStats(string organizationId, long? projectCount = null, long? errorCount = null, long? stackCount = null) {
            if (String.IsNullOrEmpty(organizationId))
                throw new ArgumentNullException("organizationId");

            IMongoQuery query = Query.EQ(FieldNames.Id, new BsonObjectId(new ObjectId(organizationId)));

            var update = new UpdateBuilder();
            if (projectCount.HasValue && projectCount.Value != 0)
                update.Inc(FieldNames.ProjectCount, projectCount.Value);
            if (errorCount.HasValue && errorCount.Value != 0) {
                update.Inc(FieldNames.ErrorCount, errorCount.Value);
                if (errorCount.Value > 0) {
                    update.Inc(FieldNames.TotalErrorCount, errorCount.Value);
                    update.Set(FieldNames.LastErrorDate, new BsonDateTime(DateTime.UtcNow));
                }
            }
            if (stackCount.HasValue && stackCount.Value != 0)
                update.Inc(FieldNames.StackCount, stackCount.Value);

            Collection.Update(query, update);
            InvalidateCache(organizationId);
        }

        public void SetStats(string organizationId, long? projectCount = null, long? errorCount = null, long? stackCount = null) {
            if (String.IsNullOrEmpty(organizationId))
                throw new ArgumentNullException("organizationId");

            IMongoQuery query = Query.EQ(FieldNames.Id, new BsonObjectId(new ObjectId(organizationId)));

            var update = new UpdateBuilder();
            if (projectCount.HasValue)
                update.Set(FieldNames.ProjectCount, projectCount.Value);
            if (errorCount.HasValue)
                update.Set(FieldNames.ErrorCount, errorCount.Value);
            if (stackCount.HasValue)
                update.Set(FieldNames.StackCount, stackCount.Value);

            Collection.Update(query, update);
            InvalidateCache(organizationId);
        }
    }
}