using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Billing;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Repositories.Queries;
using Exceptionless.Extensions;
using Foundatio.Caching;
using Foundatio.Elasticsearch.Repositories;
using Foundatio.Elasticsearch.Repositories.Queries;
using Foundatio.Logging;
using Foundatio.Repositories.Models;
using Foundatio.Repositories.Queries;
using Nest;
using SortOrder = Foundatio.Repositories.Models.SortOrder;

namespace Exceptionless.Core.Repositories {
    public class OrganizationRepository : RepositoryBase<Organization>, IOrganizationRepository {
        public OrganizationRepository(ElasticRepositoryContext<Organization> context, OrganizationIndex index, ILoggerFactory loggerFactory = null) : base(context, index, loggerFactory) {}

        public Task<Organization> GetByInviteTokenAsync(string token) {
            if (String.IsNullOrEmpty(token))
                throw new ArgumentNullException(nameof(token));

            return FindOneAsync(new ExceptionlessQuery().WithFieldEquals(OrganizationIndex.Fields.Organization.InviteToken, token));
        }

        public Task<Organization> GetByStripeCustomerIdAsync(string customerId) {
            if (String.IsNullOrEmpty(customerId))
                throw new ArgumentNullException(nameof(customerId));

            var filter = Filter<Organization>.Term(o => o.StripeCustomerId, customerId);
            return FindOneAsync(new ExceptionlessQuery().WithElasticFilter(filter));
        }

        public Task<FindResults<Organization>> GetByRetentionDaysEnabledAsync(PagingOptions paging) {
            var filter = Filter<Organization>.Range(r => r.OnField(o => o.RetentionDays).Greater(0));
            return FindAsync(new ExceptionlessQuery()
                .WithElasticFilter(filter)
                .WithSelectedFields("id", "name", "retention_days")
                .WithPaging(paging));
        }

        public Task<FindResults<Organization>> GetByCriteriaAsync(string criteria, PagingOptions paging, OrganizationSortBy sortBy, bool? paid = null, bool? suspended = null) {
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

            var query = new ExceptionlessQuery().WithPaging(paging).WithElasticFilter(filter);
            switch (sortBy) {
                case OrganizationSortBy.Newest:
                    query.WithSort(OrganizationIndex.Fields.Organization.Id, SortOrder.Descending);
                    break;
                case OrganizationSortBy.Subscribed:
                    query.WithSort(OrganizationIndex.Fields.Organization.SubscribeDate, SortOrder.Descending);
                    break;
                // case OrganizationSortBy.MostActive:
                //    query.WithSort(OrganizationIndex.Fields.Organization.TotalEventCount, SortOrder.Descending);
                //    break;
                default:
                    query.WithSort(OrganizationIndex.Fields.Organization.Name, SortOrder.Ascending);
                    break;
            }

            return FindAsync(query);
        }

        public async Task<BillingPlanStats> GetBillingPlanStatsAsync() {
            var query = new ExceptionlessQuery()
                .WithSelectedFields("plan_id", "is_suspended", "billing_price", "billing_status")
                .WithSort(OrganizationIndex.Fields.Organization.PlanId, SortOrder.Descending);

            var results = (await FindAsync(query).AnyContext()).Documents;

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

        public async Task<bool> IncrementUsageAsync(string organizationId, bool tooBig, int count = 1) {
            const int USAGE_SAVE_MINUTES = 5;

            if (String.IsNullOrEmpty(organizationId))
                return false;

            var org = await GetByIdAsync(organizationId, true).AnyContext();
            if (org == null || org.MaxEventsPerMonth < 0)
                return false;

            double hourlyTotal = await Cache.IncrementAsync(GetHourlyTotalCacheKey(organizationId), count, TimeSpan.FromMinutes(61), (uint)org.GetCurrentHourlyTotal()).AnyContext();
            double monthlyTotal = await Cache.IncrementAsync(GetMonthlyTotalCacheKey(organizationId), count, TimeSpan.FromDays(32), (uint)org.GetCurrentMonthlyTotal()).AnyContext();
            double monthlyBlocked = await Cache.GetAsync<long>(GetMonthlyBlockedCacheKey(organizationId), org.GetCurrentMonthlyBlocked()).AnyContext();
            bool overLimit = hourlyTotal > org.GetHourlyEventLimit() || (monthlyTotal - monthlyBlocked) > org.GetMaxEventsPerMonthWithBonus();

            double monthlyTooBig = await Cache.IncrementIfAsync(GetHourlyTooBigCacheKey(organizationId), 1, TimeSpan.FromMinutes(61), tooBig, (uint)org.GetCurrentHourlyTooBig()).AnyContext();
            double hourlyTooBig = await Cache.IncrementIfAsync(GetMonthlyTooBigCacheKey(organizationId), 1, TimeSpan.FromDays(32), tooBig, (uint)org.GetCurrentMonthlyTooBig()).AnyContext();

            double totalBlocked = count;

            // If the original count is less than the max events per month and original count + hourly limit is greater than the max events per month then use the monthly limit.
            if ((monthlyTotal - monthlyBlocked - count) < org.GetMaxEventsPerMonthWithBonus() && (monthlyTotal - monthlyBlocked - count + org.GetHourlyEventLimit()) >= org.GetMaxEventsPerMonthWithBonus())
                totalBlocked = (monthlyTotal - monthlyBlocked - count) < org.GetMaxEventsPerMonthWithBonus() ? monthlyTotal - monthlyBlocked - org.GetMaxEventsPerMonthWithBonus() : count;
            else if (hourlyTotal > org.GetHourlyEventLimit())
                totalBlocked = (hourlyTotal - count) < org.GetHourlyEventLimit() ? hourlyTotal - org.GetHourlyEventLimit() : count;
            else if ((monthlyTotal - monthlyBlocked) > org.GetMaxEventsPerMonthWithBonus())
                totalBlocked = (monthlyTotal - monthlyBlocked - count) < org.GetMaxEventsPerMonthWithBonus() ? monthlyTotal - monthlyBlocked - org.GetMaxEventsPerMonthWithBonus() : count;

            double hourlyBlocked = await Cache.IncrementIfAsync(GetHourlyBlockedCacheKey(organizationId), (int)totalBlocked, TimeSpan.FromMinutes(61), overLimit, (uint)org.GetCurrentHourlyBlocked()).AnyContext();
            monthlyBlocked = await Cache.IncrementIfAsync(GetMonthlyBlockedCacheKey(organizationId), (int)totalBlocked, TimeSpan.FromDays(32), overLimit, (uint)monthlyBlocked).AnyContext();

            bool justWentOverHourly = hourlyTotal > org.GetHourlyEventLimit() && hourlyTotal <= org.GetHourlyEventLimit() + count;
            bool justWentOverMonthly = monthlyTotal > org.GetMaxEventsPerMonthWithBonus() && monthlyTotal <= org.GetMaxEventsPerMonthWithBonus() + count;

            if (justWentOverMonthly)
                await PublishMessageAsync(new PlanOverage { OrganizationId = org.Id }).AnyContext();
            else if (justWentOverHourly)
                await PublishMessageAsync(new PlanOverage { OrganizationId = org.Id, IsHourly = true }).AnyContext();

            bool shouldSaveUsage = false;
            var lastCounterSavedDate = await Cache.GetAsync<DateTime>(GetUsageSavedCacheKey(organizationId)).AnyContext();

            // don't save on the 1st increment, but set the last saved date so we will save in 5 minutes
            if (!lastCounterSavedDate.HasValue)
                await Cache.SetAsync(GetUsageSavedCacheKey(organizationId), DateTime.UtcNow, TimeSpan.FromDays(32)).AnyContext();

            // save usages if we just went over one of the limits
            if (justWentOverHourly || justWentOverMonthly)
                shouldSaveUsage = true;

            // save usages if the last time we saved them is more than 5 minutes ago
            if (lastCounterSavedDate.HasValue && DateTime.UtcNow.Subtract(lastCounterSavedDate.Value).TotalMinutes >= USAGE_SAVE_MINUTES)
                shouldSaveUsage = true;

            if (shouldSaveUsage) {
                org = await GetByIdAsync(organizationId, false).AnyContext();
                org.SetMonthlyUsage(monthlyTotal, monthlyBlocked, monthlyTooBig);
                if (hourlyTotal > org.GetHourlyEventLimit())
                    org.SetHourlyOverage(hourlyTotal, hourlyBlocked, hourlyTooBig);

                await SaveAsync(org, true).AnyContext();
                await Cache.SetAsync(GetUsageSavedCacheKey(organizationId), DateTime.UtcNow, TimeSpan.FromDays(32)).AnyContext();
            }

            return overLimit;
        }

        public async Task<int> GetRemainingEventLimitAsync(string organizationId) {
            var org = await GetByIdAsync(organizationId, true).AnyContext();
            if (org == null || org.MaxEventsPerMonth < 0)
                return Int32.MaxValue;

            string monthlyCacheKey = GetMonthlyTotalCacheKey(organizationId);
            var monthlyErrorCount = await Cache.GetAsync<long>(monthlyCacheKey, 0).AnyContext();
            return Math.Max(0, org.GetMaxEventsPerMonthWithBonus() - (int)monthlyErrorCount);
        }
    }

    public enum OrganizationSortBy {
        Newest = 0,
        Subscribed = 1,
        MostActive = 2,
        Alphabetical = 3,
    }
}
