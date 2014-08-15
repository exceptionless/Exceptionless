#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using CodeSmith.Core.Extensions;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Caching;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Messaging;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models.Billing;
using Exceptionless.Extensions;
using Exceptionless.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using MongoDB.Driver.Builders;

namespace Exceptionless.Api.Tests.Repositories.InMemory {
    public class OrganizationRepository : MongoRepository<Organization>, IOrganizationRepository {
        public OrganizationRepository(MongoDatabase database, ICacheClient cacheClient = null, IMessagePublisher messagePublisher = null) : base(database, cacheClient, messagePublisher) { }

        public Organization GetByInviteToken(string token, out Invite invite) {
            invite = null;
            if (String.IsNullOrEmpty(token))
                return null;

            var organization = FindOne<Organization>(new OneOptions().WithQuery(Query.EQ(FieldNames.Invites_Token, token)));
            if (organization != null)
                invite = organization.Invites.FirstOrDefault(i => String.Equals(i.Token, token, StringComparison.OrdinalIgnoreCase));

            return organization;
        }

        public Organization GetByStripeCustomerId(string customerId) {
            if (String.IsNullOrEmpty(customerId))
                throw new ArgumentNullException("customerId");

            return FindOne<Organization>(new OneOptions().WithQuery(Query.EQ(FieldNames.StripeCustomerId, customerId)));
        }

        public ICollection<Organization> GetByRetentionDaysEnabled(PagingOptions paging) {
            return Find<Organization>(new MultiOptions()
                .WithQuery(Query.GT(FieldNames.RetentionDays, 0))
                .WithFields(FieldNames.Id, FieldNames.Name, FieldNames.RetentionDays)
                .WithPaging(paging));
        }

        public ICollection<Organization> GetAbandoned(int? limit = 20) {
            var query = Query.And(
                Query.EQ(FieldNames.PlanId, BillingManager.FreePlan.Id),
                Query.LTE(FieldNames.TotalEventCount, new BsonInt64(0)),
                Query.GTE(FieldNames.Id, new BsonObjectId(ObjectId.GenerateNewId(DateTime.Now.SubtractDays(90)))),
                Query.GTE(FieldNames.LastEventDate, DateTime.Now.SubtractDays(90)),
                Query.NotExists(FieldNames.StripeCustomerId));

            return Find<Organization>(new MultiOptions().WithQuery(query).WithFields(FieldNames.Id, FieldNames.Name).WithLimit(limit));
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

        public ICollection<Organization> GetByCriteria(string criteria, PagingOptions paging, OrganizationSortBy sortBy, bool? paid = null, bool? suspended = null) {
            var options = new MultiOptions().WithPaging(paging);
            if (!String.IsNullOrWhiteSpace(criteria))
                options.Query = options.Query.And(Query.Matches(FieldNames.Name, new BsonRegularExpression(String.Format("/{0}/i", criteria))));
            
            if (paid.HasValue) {
                if (paid.Value)
                    options.Query = options.Query.And(Query.NE(FieldNames.PlanId, new BsonString(BillingManager.FreePlan.Id)));
                else
                    options.Query = options.Query.And(Query.EQ(FieldNames.PlanId, new BsonString(BillingManager.FreePlan.Id)));
            }

            if (suspended.HasValue) {
                if (suspended.Value)
                    options.Query = options.Query.And(
                        Query.Or(
                            Query.And(
                                Query.NE(FieldNames.BillingStatus, new BsonInt32((int)BillingStatus.Active)), 
                                Query.NE(FieldNames.BillingStatus, new BsonInt32((int)BillingStatus.Trialing)), 
                                Query.NE(FieldNames.BillingStatus, new BsonInt32((int)BillingStatus.Canceled))
                            ), 
                            Query.EQ(FieldNames.IsSuspended, new BsonBoolean(true))));
                else
                    options.Query = options.Query.And(
                        Query.Or(
                            Query.EQ(FieldNames.BillingStatus, new BsonInt32((int)BillingStatus.Active)), 
                            Query.EQ(FieldNames.BillingStatus, new BsonInt32((int)BillingStatus.Trialing)), 
                            Query.EQ(FieldNames.BillingStatus, new BsonInt32((int)BillingStatus.Canceled))
                        ), 
                        Query.EQ(FieldNames.IsSuspended, new BsonBoolean(false)));
            }

            switch (sortBy) {
                case OrganizationSortBy.Newest:
                    options.SortBy = SortBy.Descending(FieldNames.Id);
                    break;
                case OrganizationSortBy.Subscribed:
                    options.SortBy = SortBy.Descending(FieldNames.SubscribeDate);
                    break;
                case OrganizationSortBy.MostActive:
                    options.SortBy = SortBy.Descending(FieldNames.TotalEventCount);
                    break;
                default:
                    options.SortBy = SortBy.Ascending(FieldNames.Name);
                    break;
            }

            return Find<Organization>(options);
        }

        public BillingPlanStats GetBillingPlanStats() {
            var results = Find<Organization>(new MultiOptions()
                .WithFields(FieldNames.PlanId, FieldNames.IsSuspended, FieldNames.BillingPrice, FieldNames.BillingStatus)
                .WithSort(SortBy.Descending(FieldNames.PlanId)));

            List<Organization> smallOrganizations = results.Where(o => String.Equals(o.PlanId, BillingManager.SmallPlan.Id) && o.BillingPrice > 0).ToList();
            List<Organization> mediumOrganizations = results.Where(o => String.Equals(o.PlanId, BillingManager.MediumPlan.Id) && o.BillingPrice > 0).ToList();
            List<Organization> largeOrganizations = results.Where(o => String.Equals(o.PlanId, BillingManager.LargePlan.Id) && o.BillingPrice > 0).ToList();
            decimal monthlyTotalPaid = smallOrganizations.Where(o => !o.IsSuspended && o.BillingStatus == BillingStatus.Active).Sum(o => o.BillingPrice)
                + mediumOrganizations.Where(o => !o.IsSuspended && o.BillingStatus == BillingStatus.Active).Sum(o => o.BillingPrice)
                + largeOrganizations.Where(o => !o.IsSuspended && o.BillingStatus == BillingStatus.Active).Sum(o => o.BillingPrice);

            List<Organization> smallYearlyOrganizations = results.Where(o => String.Equals(o.PlanId, BillingManager.SmallYearlyPlan.Id) && o.BillingPrice > 0).ToList();
            List<Organization> mediumYearlyOrganizations = results.Where(o => String.Equals(o.PlanId, BillingManager.MediumYearlyPlan.Id) && o.BillingPrice > 0).ToList();
            List<Organization> largeYearlyOrganizations = results.Where(o => String.Equals(o.PlanId, BillingManager.LargeYearlyPlan.Id) && o.BillingPrice > 0).ToList();
            decimal yearlyTotalPaid = smallYearlyOrganizations.Where(o => !o.IsSuspended && o.BillingStatus == BillingStatus.Active).Sum(o => o.BillingPrice)
                + mediumYearlyOrganizations.Where(o => !o.IsSuspended && o.BillingStatus == BillingStatus.Active).Sum(o => o.BillingPrice)
                + largeYearlyOrganizations.Where(o => !o.IsSuspended && o.BillingStatus == BillingStatus.Active).Sum(o => o.BillingPrice);

            return new BillingPlanStats {
                SmallTotal = smallOrganizations.Count,
                SmallYearlyTotal = smallYearlyOrganizations.Count,
                MediumTotal = mediumOrganizations.Count,
                MediumYearlyTotal = mediumYearlyOrganizations.Count,
                LargeTotal = largeOrganizations.Count,
                LargeYearlyTotal = largeYearlyOrganizations.Count,
                MonthlyTotal = monthlyTotalPaid + (yearlyTotalPaid / 12),
                YearlyTotal = (monthlyTotalPaid * 12) + yearlyTotalPaid,
                MonthlyTotalAccounts = smallOrganizations.Count + mediumOrganizations.Count + largeOrganizations.Count,
                YearlyTotalAccounts = smallYearlyOrganizations.Count + mediumYearlyOrganizations.Count + largeYearlyOrganizations.Count,
                FreeAccounts = results.Count(o => String.Equals(o.PlanId, BillingManager.FreePlan.Id)),
                PaidAccounts = results.Count(o => !String.Equals(o.PlanId, BillingManager.FreePlan.Id) && o.BillingPrice > 0),
                FreeloaderAccounts = results.Count(o => !String.Equals(o.PlanId, BillingManager.FreePlan.Id) && o.BillingPrice <= 0),
                SuspendedAccounts = results.Count(o => o.IsSuspended),
            };
        }

        private string GetHourlyUsageCacheKey(string organizationId) {
            return String.Concat("usage", ":hr-", organizationId, ":", DateTime.UtcNow.Date.ToString("MMddHH"));
        }

        private string GetMonthlyUsageCacheKey(string organizationId) {
            return String.Concat("usage", ":month-", organizationId, ":", DateTime.UtcNow.Date.ToString("MM"));
        }

        private string GetUsageSavedCacheKey(string organizationId) {
            return String.Concat("usage-saved", ":", organizationId);
        }

        public bool IncrementUsage(string organizationId, int count = 1) {
            const int USAGE_SAVE_MINUTES = 5;

            var org = GetById(organizationId, true);
            if (org.MaxEventsPerMonth < 0)
                return false;

            string hourlyCacheKey = GetHourlyUsageCacheKey(org.Id);
            long hourlyErrorCount = Cache.Increment(hourlyCacheKey, (uint)count, TimeSpan.FromMinutes(61));
            string monthlyCacheKey = GetMonthlyUsageCacheKey(organizationId);
            long monthlyErrorCount = Cache.Increment(monthlyCacheKey, (uint)count, TimeSpan.FromDays(32), (uint)org.GetCurrentMonthlyUsage());
            bool justWentOverHourly = hourlyErrorCount > org.GetHourlyErrorLimit() && hourlyErrorCount <= org.GetHourlyErrorLimit() + count;
            bool justWentOverMonthly = monthlyErrorCount > org.MaxEventsPerMonth && monthlyErrorCount <= org.MaxEventsPerMonth + count;
            bool overLimit = hourlyErrorCount > org.GetHourlyErrorLimit() || monthlyErrorCount > org.MaxEventsPerMonth;

            if (justWentOverHourly)
                PublishMessageAsync(new PlanOverage { OrganizationId = org.Id, IsHourly = true });

            if (justWentOverMonthly)
                PublishMessageAsync(new PlanOverage { OrganizationId = org.Id });

            var lastCounterSavedDate = Cache.Get<DateTime?>(GetUsageSavedCacheKey(organizationId));
            if (!justWentOverHourly && !justWentOverMonthly && lastCounterSavedDate.HasValue && DateTime.UtcNow.Subtract(lastCounterSavedDate.Value).TotalMinutes < USAGE_SAVE_MINUTES)
                return overLimit;

            org = GetById(organizationId, false);
            org.SetMonthlyUsage(monthlyErrorCount);
            if (hourlyErrorCount > org.GetHourlyErrorLimit())
                org.SetHourlyOverage(hourlyErrorCount);

            Save(org);
            Cache.Set(GetUsageSavedCacheKey(organizationId), DateTime.UtcNow, TimeSpan.FromDays(32));

            return overLimit;
        }

        public int GetRemainingEventLimit(string organizationId) {
            var org = GetById(organizationId, true);
            if (org.MaxEventsPerMonth < 0)
                return Int32.MaxValue;

            string monthlyCacheKey = GetMonthlyUsageCacheKey(organizationId);
            var monthlyErrorCount = Cache.Get<long?>(monthlyCacheKey);
            if (!monthlyErrorCount.HasValue)
                monthlyErrorCount = 0;

            return Math.Max(0, org.MaxEventsPerMonth - (int)monthlyErrorCount.Value);
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
            public const string Usage = "Usage";
            public const string OverageHours = "OverageHours";
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
            cm.GetMemberMap(c => c.Usage).SetElementName(FieldNames.Usage).SetIgnoreIfNull(true).SetShouldSerializeMethod(obj => ((Organization)obj).Usage.Any());
            cm.GetMemberMap(c => c.OverageHours).SetElementName(FieldNames.OverageHours).SetIgnoreIfNull(true).SetShouldSerializeMethod(obj => ((Organization)obj).OverageHours.Any());
        }

        #endregion
    }

    public enum OrganizationSortBy {
        Newest = 0,
        Subscribed = 1,
        MostActive = 2,
        Alphabetical = 3,
    }
}