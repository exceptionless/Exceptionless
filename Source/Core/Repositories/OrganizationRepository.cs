using System;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Billing;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Extensions;
using FluentValidation;
using Foundatio.Caching;
using Foundatio.Messaging;
using Nest;

namespace Exceptionless.Core.Repositories {
    public class OrganizationRepository : ElasticSearchRepository<Organization>, IOrganizationRepository {
        public OrganizationRepository(IElasticClient elasticClient, OrganizationIndex index, IValidator<Organization> validator = null, ICacheClient cacheClient = null, IMessagePublisher messagePublisher = null) : base(elasticClient, index, validator, cacheClient, messagePublisher) { }

        public Organization GetByInviteToken(string token, out Invite invite) {
            invite = null;
            if (String.IsNullOrEmpty(token))
                return null;

            var filter = Filter<Organization>.Term(OrganizationIndex.Fields.Organization.InviteToken, token);
            var organization = FindOne(new ElasticSearchOptions<Organization>().WithFilter(filter));
            if (organization != null)
                invite = organization.Invites.FirstOrDefault(i => String.Equals(i.Token, token, StringComparison.OrdinalIgnoreCase));

            return organization;
        }

        public Organization GetByStripeCustomerId(string customerId) {
            if (String.IsNullOrEmpty(customerId))
                throw new ArgumentNullException("customerId");

            var filter = Filter<Organization>.Term(o => o.StripeCustomerId, customerId);
            return FindOne(new ElasticSearchOptions<Organization>().WithFilter(filter));
        }

        public FindResults<Organization> GetByRetentionDaysEnabled(PagingOptions paging) {
            var filter = Filter<Organization>.Range(r => r.OnField(o => o.RetentionDays).Greater(0));
            return Find(new ElasticSearchOptions<Organization>()
                .WithFilter(filter)
                .WithFields("id", "name", "retention_days")
                .WithPaging(paging));
        }
        
        public FindResults<Organization> GetByCriteria(string criteria, PagingOptions paging, OrganizationSortBy sortBy, bool? paid = null, bool? suspended = null) {
            var filter = Filter<Organization>.MatchAll();
            if (!String.IsNullOrWhiteSpace(criteria))
                filter &= Filter<Organization>.Term(o => o.Name, criteria);

            if (paid.HasValue) {
                if (paid.Value)
                    filter &= !Filter<Organization>.Term(o => o.PlanId, BillingManager.FreePlan.Id);
                else
                    filter &= Filter<Organization>.Term(o => o.PlanId, BillingManager.FreePlan.Id);
            }

            if (suspended.HasValue) {
                if (suspended.Value)
                    filter &= Filter<Organization>.And(and => ((
                            !Filter<Organization>.Term(o => o.BillingStatus, BillingStatus.Active) && 
                            !Filter<Organization>.Term(o => o.BillingStatus, BillingStatus.Trialing) && 
                            !Filter<Organization>.Term(o => o.BillingStatus, BillingStatus.Canceled)
                        ) || Filter<Organization>.Term(o => o.IsSuspended, true)));
                else
                    filter &= Filter<Organization>.And(and => ((
                            Filter<Organization>.Term(o => o.BillingStatus, BillingStatus.Active) &&
                            Filter<Organization>.Term(o => o.BillingStatus, BillingStatus.Trialing) &&
                            Filter<Organization>.Term(o => o.BillingStatus, BillingStatus.Canceled)
                        ) || Filter<Organization>.Term(o => o.IsSuspended, false)));
            }

            Func<SortFieldDescriptor<Organization>, IFieldSort> sort = descriptor => descriptor.OnField(o => o.Name).Ascending();
            switch (sortBy) {
                case OrganizationSortBy.Newest:
                    sort = descriptor => descriptor.OnField(o => o.Id).Descending();
                    break;
                case OrganizationSortBy.Subscribed:
                    sort = descriptor => descriptor.OnField(o => o.SubscribeDate).Descending();
                    break;
                // case OrganizationSortBy.MostActive:
                //    sort = descriptor => descriptor.OnField(o => o.TotalEventCount).Descending();
                //    break;
            }
            
            return Find(new ElasticSearchOptions<Organization>().WithPaging(paging).WithFilter(filter).WithSort(sort));
        }

        public BillingPlanStats GetBillingPlanStats() {
            var results = Find(new ElasticSearchOptions<Organization>()
                .WithFields("plan_id", "is_suspended", "billing_price", "billing_status")
                .WithSort(s => s.OnField(o => o.PlanId).Order(Nest.SortOrder.Descending))).Documents;

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

        private string GetHourlyBlockedCacheKey(string organizationId) {
            return String.Concat("usage-blocked", ":", DateTime.UtcNow.ToString("MMddHH"), ":", organizationId);
        }

        private string GetHourlyTotalCacheKey(string organizationId) {
            return String.Concat("usage-total", ":", DateTime.UtcNow.ToString("MMddHH"), ":", organizationId);
        }

        private string GetHourlyTooBigCacheKey(string organizationId) {
            return String.Concat("usage-toobig", ":", DateTime.UtcNow.ToString("MMddHH"), ":", organizationId);
        }

        private string GetMonthlyBlockedCacheKey(string organizationId) {
            return String.Concat("usage-blocked", ":", DateTime.UtcNow.Date.ToString("MM"), ":", organizationId);
        }

        private string GetMonthlyTotalCacheKey(string organizationId) {
            return String.Concat("usage-total", ":", DateTime.UtcNow.Date.ToString("MM"), ":", organizationId);
        }

        private string GetMonthlyTooBigCacheKey(string organizationId) {
            return String.Concat("usage-toobig", ":", DateTime.UtcNow.Date.ToString("MM"), ":", organizationId);
        }

        private string GetUsageSavedCacheKey(string organizationId) {
            return String.Concat("usage-saved", ":", organizationId);
        }

        public bool IncrementUsage(string organizationId, bool tooBig, int count = 1) {
            const int USAGE_SAVE_MINUTES = 5;

            if (String.IsNullOrEmpty(organizationId))
                return false;

            var org = GetById(organizationId, true);
            if (org == null || org.MaxEventsPerMonth < 0)
                return false;

            long hourlyTotal = Cache.Increment(GetHourlyTotalCacheKey(organizationId), (uint)count, TimeSpan.FromMinutes(61), (uint)org.GetCurrentHourlyTotal());
            long monthlyTotal = Cache.Increment(GetMonthlyTotalCacheKey(organizationId), (uint)count, TimeSpan.FromDays(32), (uint)org.GetCurrentMonthlyTotal());
            long monthlyBlocked = Cache.Get<long?>(GetMonthlyBlockedCacheKey(organizationId)) ?? org.GetCurrentMonthlyBlocked();
            bool overLimit = hourlyTotal > org.GetHourlyEventLimit() || (monthlyTotal - monthlyBlocked) > org.GetMaxEventsPerMonthWithBonus();

            long monthlyTooBig = Cache.IncrementIf(GetHourlyTooBigCacheKey(organizationId), 1, TimeSpan.FromMinutes(61), tooBig, (uint)org.GetCurrentHourlyTooBig());
            long hourlyTooBig = Cache.IncrementIf(GetMonthlyTooBigCacheKey(organizationId), 1, TimeSpan.FromDays(32), tooBig, (uint)org.GetCurrentMonthlyTooBig());

            long totalBlocked = count;

            // If the original count is less than the max events per month and original count + hourly limit is greater than the max events per month then use the monthly limit.
            if ((monthlyTotal - monthlyBlocked - count) < org.GetMaxEventsPerMonthWithBonus() && (monthlyTotal - monthlyBlocked - count + org.GetHourlyEventLimit()) >= org.GetMaxEventsPerMonthWithBonus())
                totalBlocked = (monthlyTotal - monthlyBlocked - count) < org.GetMaxEventsPerMonthWithBonus() ? monthlyTotal - monthlyBlocked - org.GetMaxEventsPerMonthWithBonus() : count;
            else if (hourlyTotal > org.GetHourlyEventLimit())
                totalBlocked = (hourlyTotal - count) < org.GetHourlyEventLimit() ? hourlyTotal - org.GetHourlyEventLimit() : count;
            else if ((monthlyTotal - monthlyBlocked) > org.GetMaxEventsPerMonthWithBonus())
                totalBlocked = (monthlyTotal - monthlyBlocked - count) < org.GetMaxEventsPerMonthWithBonus() ? monthlyTotal - monthlyBlocked - org.GetMaxEventsPerMonthWithBonus() : count;
            
            long hourlyBlocked = Cache.IncrementIf(GetHourlyBlockedCacheKey(organizationId), (uint)totalBlocked, TimeSpan.FromMinutes(61), overLimit, (uint)org.GetCurrentHourlyBlocked());
            monthlyBlocked = Cache.IncrementIf(GetMonthlyBlockedCacheKey(organizationId), (uint)totalBlocked, TimeSpan.FromDays(32), overLimit, (uint)monthlyBlocked);

            bool justWentOverHourly = hourlyTotal > org.GetHourlyEventLimit() && hourlyTotal <= org.GetHourlyEventLimit() + count;
            bool justWentOverMonthly = monthlyTotal > org.GetMaxEventsPerMonthWithBonus() && monthlyTotal <= org.GetMaxEventsPerMonthWithBonus() + count;

            if (justWentOverMonthly)
                PublishMessage(new PlanOverage { OrganizationId = org.Id });
            else if (justWentOverHourly)
                PublishMessage(new PlanOverage { OrganizationId = org.Id, IsHourly = true });

            bool shouldSaveUsage = false;
            var lastCounterSavedDate = Cache.Get<DateTime?>(GetUsageSavedCacheKey(organizationId));

            // don't save on the 1st increment, but set the last saved date so we will save in 5 minutes
            if (!lastCounterSavedDate.HasValue)
                Cache.Set(GetUsageSavedCacheKey(organizationId), DateTime.UtcNow, TimeSpan.FromDays(32));

            // save usages if we just went over one of the limits
            if (justWentOverHourly || justWentOverMonthly)
                shouldSaveUsage = true;

            // save usages if the last time we saved them is more than 5 minutes ago
            if (lastCounterSavedDate.HasValue && DateTime.UtcNow.Subtract(lastCounterSavedDate.Value).TotalMinutes >= USAGE_SAVE_MINUTES)
                shouldSaveUsage = true;

            if (shouldSaveUsage) {
                org = GetById(organizationId, false);
                org.SetMonthlyUsage(monthlyTotal, monthlyBlocked, monthlyTooBig);
                if (hourlyTotal > org.GetHourlyEventLimit())
                    org.SetHourlyOverage(hourlyTotal, hourlyBlocked, hourlyTooBig);

                Save(org);
                Cache.Set(GetUsageSavedCacheKey(organizationId), DateTime.UtcNow, TimeSpan.FromDays(32));
            }

            return overLimit;
        }

        public int GetRemainingEventLimit(string organizationId) {
            var org = GetById(organizationId, true);
            if (org == null || org.MaxEventsPerMonth < 0)
                return Int32.MaxValue;

            string monthlyCacheKey = GetMonthlyTotalCacheKey(organizationId);
            var monthlyErrorCount = Cache.Get<long?>(monthlyCacheKey);
            if (!monthlyErrorCount.HasValue)
                monthlyErrorCount = 0;

            return Math.Max(0, org.GetMaxEventsPerMonthWithBonus() - (int)monthlyErrorCount.Value);
        }
    }

    public enum OrganizationSortBy {
        Newest = 0,
        Subscribed = 1,
        MostActive = 2,
        Alphabetical = 3,
    }
}