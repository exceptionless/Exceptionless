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
using Exceptionless.Core.Caching;
using Exceptionless.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using MongoDB.Driver.Builders;

namespace Exceptionless.Core.Repositories {
    public class OrganizationRepository : MongoRepository<Organization>, IOrganizationRepository {
        public OrganizationRepository(MongoDatabase database, ICacheClient cacheClient = null) : base(database, cacheClient) {}

        public Organization GetByInviteToken(string token, out Invite invite) {
            invite = null;
            if (String.IsNullOrEmpty(token))
                return null;

            var organization = FindOne<Organization>(new FindOptions().WithQuery(Query.EQ(FieldNames.Invites_Token, token)));
            if (organization != null)
                invite = organization.Invites.FirstOrDefault(i => String.Equals(i.Token, token, StringComparison.OrdinalIgnoreCase));

            return organization;
        }

        public Organization GetByStripeCustomerId(string customerId) {
            if (String.IsNullOrEmpty(customerId))
                throw new ArgumentNullException("customerId");

            return FindOne<Organization>(new FindOptions().WithQuery(Query.EQ(FieldNames.StripeCustomerId, customerId)));
        }

        public void IncrementStats(string organizationId, long? projectCount = null, long? eventCount = null, long? stackCount = null) {
            if (String.IsNullOrEmpty(organizationId))
                throw new ArgumentNullException("organizationId");

            var update = new UpdateBuilder();
            if (projectCount.HasValue && projectCount.Value != 0)
                update.Inc(FieldNames.ProjectCount, projectCount.Value);
            if (eventCount.HasValue && eventCount.Value != 0) {
                update.Inc(FieldNames.EventCount, eventCount.Value);
                if (eventCount.Value > 0) {
                    update.Inc(FieldNames.TotalEventCount, eventCount.Value);
                    update.Set(FieldNames.LastEventDate, new BsonDateTime(DateTime.UtcNow));
                }
            }

            if (stackCount.HasValue && stackCount.Value != 0)
                update.Inc(FieldNames.StackCount, stackCount.Value);

            UpdateAll(new QueryOptions().WithId(organizationId), update);
            InvalidateCache(organizationId);
        }

        public void SetStats(string organizationId, long? projectCount = null, long? errorCount = null, long? stackCount = null) {
            if (String.IsNullOrEmpty(organizationId))
                throw new ArgumentNullException("organizationId");

            var update = new UpdateBuilder();
            if (projectCount.HasValue)
                update.Set(FieldNames.ProjectCount, projectCount.Value);
            if (errorCount.HasValue)
                update.Set(FieldNames.EventCount, errorCount.Value);
            if (stackCount.HasValue)
                update.Set(FieldNames.StackCount, stackCount.Value);

            UpdateAll(new QueryOptions().WithId(organizationId), update);
            InvalidateCache(organizationId);
        }

        #region Collection Setup

        public const string CollectionName = "organization";

        protected override string GetCollectionName() {
            return CollectionName;
        }

        public static class FieldNames {
            public const string Id = CommonFieldNames.Id;
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
            public const string MaxEventsPerDay = "MaxEventsPerDay";
            public const string MaxProjects = "MaxProjects";
            public const string ProjectCount = "ProjectCount";
            public const string StackCount = "StackCount";
            public const string EventCount = "EventCount";
            public const string TotalEventCount = "TotalEventCount"; // TODO: Add a migration for TotalErrorCount.
            public const string LastEventDate = "LastEventDate";
            public const string Invites = "Invites";
            public const string Invites_Token = "Invites.Token";
            public const string Invites_EmailAddress = "Invites.EmailAddress";
            public const string Invites_DateAdded = "Invites.DateAdded";
            public const string OverageDays = "OverageDays";
        }

        protected override void InitializeCollection(MongoDatabase database) {
            base.InitializeCollection(database);

            _collection.CreateIndex(IndexKeys.Ascending(FieldNames.Invites_Token), IndexOptions.SetBackground(true));
            _collection.CreateIndex(IndexKeys.Ascending(FieldNames.Invites_EmailAddress), IndexOptions.SetBackground(true));
            _collection.CreateIndex(IndexKeys.Ascending(FieldNames.StripeCustomerId), IndexOptions.SetUnique(true).SetSparse(true).SetBackground(true));
        }

        protected override void ConfigureClassMap(BsonClassMap<Organization> cm) {
            base.ConfigureClassMap(cm);
            cm.GetMemberMap(c => c.StripeCustomerId).SetElementName(FieldNames.StripeCustomerId).SetIgnoreIfNull(true);
            cm.GetMemberMap(c => c.PlanId).SetElementName(FieldNames.PlanId).SetIgnoreIfNull(true);
            cm.GetMemberMap(c => c.CardLast4).SetElementName(FieldNames.CardLast4).SetIgnoreIfNull(true);
            cm.GetMemberMap(c => c.SubscribeDate).SetElementName(FieldNames.SubscribeDate).SetIgnoreIfNull(true);
            cm.GetMemberMap(c => c.BillingChangeDate).SetElementName(FieldNames.BillingChangeDate).SetIgnoreIfNull(true);
            cm.GetMemberMap(c => c.BillingChangedByUserId).SetElementName(FieldNames.BillingChangedByUserId).SetIgnoreIfNull(true);
            cm.GetMemberMap(c => c.OverageDays).SetElementName(FieldNames.OverageDays).SetIgnoreIfNull(true);
        }

        #endregion
    }
}