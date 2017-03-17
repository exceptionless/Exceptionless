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
using Exceptionless.DateTimeExtensions;
using Exceptionless.Extensions;
using FluentValidation;
using Foundatio.Caching;
using Foundatio.Logging;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;
using Foundatio.Utility;
using Nest;

namespace Exceptionless.Core.Repositories {
    public class OrganizationRepository : RepositoryBase<Organization>, IOrganizationRepository {
        public OrganizationRepository(ExceptionlessElasticConfiguration configuration, IValidator<Organization> validator)
            : base(configuration.Organizations.Organization, validator) {}

        public async Task<Organization> GetByInviteTokenAsync(string token) {
            if (String.IsNullOrEmpty(token))
                throw new ArgumentNullException(nameof(token));

            var filter = Query<Organization>.Term(f => f.Field(o => o.Invites.First().Token).Value(token));
            var hit = await FindOneAsync(q => q.ElasticFilter(filter)).AnyContext();
            return hit?.Document;
        }

        public async Task<Organization> GetByStripeCustomerIdAsync(string customerId) {
            if (String.IsNullOrEmpty(customerId))
                throw new ArgumentNullException(nameof(customerId));

            var filter = Query<Organization>.Term(f => f.Field(o => o.StripeCustomerId).Value(customerId));
            var hit = await FindOneAsync(q => q.ElasticFilter(filter)).AnyContext();
            return hit?.Document;
        }

        public Task<FindResults<Organization>> GetByRetentionDaysEnabledAsync(CommandOptionsDescriptor<Organization> options = null) {
            var filter = Query<Organization>.Range(f => f.Field(o => o.RetentionDays).GreaterThan(0));
            return FindAsync(q => q.ElasticFilter(filter).Include(o => o.Id, o => o.Name, o => o.RetentionDays), options);
        }

        public Task<FindResults<Organization>> GetByCriteriaAsync(string criteria, CommandOptionsDescriptor<Organization> options, OrganizationSortBy sortBy, bool? paid = null, bool? suspended = null) {
            var filter = Query<Organization>.MatchAll();
            if (!String.IsNullOrWhiteSpace(criteria))
                filter &= Query<Organization>.Term(o => o.Name, criteria);

            if (paid.HasValue) {
                if (paid.Value)
                    filter &= !Query<Organization>.Term(o => o.PlanId, BillingManager.FreePlan.Id);
                else
                    filter &= Query<Organization>.Term(o => o.PlanId, BillingManager.FreePlan.Id);
            }

            if (suspended.HasValue) {
                if (suspended.Value)
                    filter &= (!Query<Organization>.Term(o => o.BillingStatus, BillingStatus.Active) &&
                            !Query<Organization>.Term(o => o.BillingStatus, BillingStatus.Trialing) &&
                            !Query<Organization>.Term(o => o.BillingStatus, BillingStatus.Canceled)
                        ) || Query<Organization>.Term(o => o.IsSuspended, true);
                else
                    filter &= (
                            Query<Organization>.Term(o => o.BillingStatus, BillingStatus.Active) &&
                            Query<Organization>.Term(o => o.BillingStatus, BillingStatus.Trialing) &&
                            Query<Organization>.Term(o => o.BillingStatus, BillingStatus.Canceled)
                        ) || Query<Organization>.Term(o => o.IsSuspended, false);
            }

            var query = new RepositoryQuery<Organization>().ElasticFilter(filter);
            switch (sortBy) {
                case OrganizationSortBy.Newest:
                    query.SortDescending((Organization o) => o.Id);
                    break;
                case OrganizationSortBy.Subscribed:
                    query.SortDescending((Organization o) => o.SubscribeDate);
                    break;
                // case OrganizationSortBy.MostActive:
                //    query.WithSortDescending((Organization o) => o.TotalEventCount);
                //    break;
                default:
                    query.SortAscending((Organization o) => o.Name.Suffix("keyword"));
                    break;
            }

            return FindAsync(q => query, options);
        }

        public async Task<BillingPlanStats> GetBillingPlanStatsAsync() {
            var query = new RepositoryQuery<Organization>().Include(o => o.PlanId, o => o.IsSuspended, o => o.BillingPrice, o => o.BillingStatus)
                .SortDescending((Organization o) => o.PlanId);

            var results = (await FindAsync(query).AnyContext()).Documents;
            var smallOrganizations = results.Where(o => String.Equals(o.PlanId, BillingManager.SmallPlan.Id) && o.BillingPrice > 0).ToList();
            var mediumOrganizations = results.Where(o => String.Equals(o.PlanId, BillingManager.MediumPlan.Id) && o.BillingPrice > 0).ToList();
            var largeOrganizations = results.Where(o => String.Equals(o.PlanId, BillingManager.LargePlan.Id) && o.BillingPrice > 0).ToList();
            decimal monthlyTotalPaid = smallOrganizations.Where(o => !o.IsSuspended && o.BillingStatus == BillingStatus.Active).Sum(o => o.BillingPrice)
                + mediumOrganizations.Where(o => !o.IsSuspended && o.BillingStatus == BillingStatus.Active).Sum(o => o.BillingPrice)
                + largeOrganizations.Where(o => !o.IsSuspended && o.BillingStatus == BillingStatus.Active).Sum(o => o.BillingPrice);

            var smallYearlyOrganizations = results.Where(o => String.Equals(o.PlanId, BillingManager.SmallYearlyPlan.Id) && o.BillingPrice > 0).ToList();
            var mediumYearlyOrganizations = results.Where(o => String.Equals(o.PlanId, BillingManager.MediumYearlyPlan.Id) && o.BillingPrice > 0).ToList();
            var largeYearlyOrganizations = results.Where(o => String.Equals(o.PlanId, BillingManager.LargeYearlyPlan.Id) && o.BillingPrice > 0).ToList();
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
            return String.Concat("usage-blocked", ":", SystemClock.UtcNow.ToString("MMddHH"), ":", organizationId);
        }

        private string GetHourlyTotalCacheKey(string organizationId) {
            return String.Concat("usage-total", ":", SystemClock.UtcNow.ToString("MMddHH"), ":", organizationId);
        }

        private string GetHourlyTooBigCacheKey(string organizationId) {
            return String.Concat("usage-toobig", ":", SystemClock.UtcNow.ToString("MMddHH"), ":", organizationId);
        }

        private string GetMonthlyBlockedCacheKey(string organizationId) {
            return String.Concat("usage-blocked", ":", SystemClock.UtcNow.Date.ToString("MM"), ":", organizationId);
        }

        private string GetMonthlyTotalCacheKey(string organizationId) {
            return String.Concat("usage-total", ":", SystemClock.UtcNow.Date.ToString("MM"), ":", organizationId);
        }

        private string GetMonthlyTooBigCacheKey(string organizationId) {
            return String.Concat("usage-toobig", ":", SystemClock.UtcNow.Date.ToString("MM"), ":", organizationId);
        }

        private string GetUsageSavedCacheKey(string organizationId) {
            return String.Concat("usage-saved", ":", organizationId);
        }

        public async Task<bool> IncrementUsageAsync(string organizationId, bool tooBig, int count = 1, bool applyHourlyLimit = true) {
            const int USAGE_SAVE_MINUTES = 5;

            if (String.IsNullOrEmpty(organizationId) || count == 0)
                return false;

            var org = await this.GetByIdAsync(organizationId, o => o.Cache()).AnyContext();
            if (org == null || org.MaxEventsPerMonth < 0)
                return false;

            double hourlyTotal = await Cache.IncrementAsync(GetHourlyTotalCacheKey(organizationId), count, TimeSpan.FromMinutes(61), (uint)org.GetCurrentHourlyTotal()).AnyContext();
            double monthlyTotal = await Cache.IncrementAsync(GetMonthlyTotalCacheKey(organizationId), count, TimeSpan.FromDays(32), (uint)org.GetCurrentMonthlyTotal()).AnyContext();
            double hourlyBlocked = await Cache.GetAsync<long>(GetHourlyBlockedCacheKey(organizationId), org.GetCurrentHourlyBlocked()).AnyContext();
            double monthlyBlocked = await Cache.GetAsync<long>(GetMonthlyBlockedCacheKey(organizationId), org.GetCurrentMonthlyBlocked()).AnyContext();
            double totalBlocked = GetTotalBlocked(org, count, monthlyTotal, monthlyBlocked, hourlyTotal, hourlyBlocked, applyHourlyLimit);

            bool overLimit = totalBlocked > 0;
            hourlyBlocked = await Cache.IncrementIfAsync(GetHourlyBlockedCacheKey(organizationId), (int)totalBlocked, TimeSpan.FromMinutes(61), overLimit, (uint)hourlyBlocked).AnyContext();
            monthlyBlocked = await Cache.IncrementIfAsync(GetMonthlyBlockedCacheKey(organizationId), (int)totalBlocked, TimeSpan.FromDays(32), overLimit, (uint)monthlyBlocked).AnyContext();

            double hourlyTooBig = await Cache.IncrementIfAsync(GetHourlyTooBigCacheKey(organizationId), count, TimeSpan.FromMinutes(61), tooBig, (uint)org.GetCurrentHourlyTooBig()).AnyContext();
            double monthlyTooBig = await Cache.IncrementIfAsync(GetMonthlyTooBigCacheKey(organizationId), count, TimeSpan.FromDays(32), tooBig, (uint)org.GetCurrentMonthlyTooBig()).AnyContext();

            bool justWentOverHourly = hourlyTotal > org.GetHourlyEventLimit() && hourlyTotal <= org.GetHourlyEventLimit() + count;
            bool justWentOverMonthly = monthlyTotal > org.GetMaxEventsPerMonthWithBonus() && monthlyTotal <= org.GetMaxEventsPerMonthWithBonus() + count;

            bool shouldSaveUsage = false;
            var lastCounterSavedDate = await Cache.GetAsync<DateTime>(GetUsageSavedCacheKey(organizationId)).AnyContext();

            // don't save on the 1st increment, but set the last saved date so we will save in 5 minutes
            if (!lastCounterSavedDate.HasValue)
                await Cache.SetAsync(GetUsageSavedCacheKey(organizationId), SystemClock.UtcNow, TimeSpan.FromDays(32)).AnyContext();

            // save usages if we just went over one of the limits
            if (justWentOverHourly || justWentOverMonthly)
                shouldSaveUsage = true;

            // save usages if the last time we saved them is more than 5 minutes ago
            if (lastCounterSavedDate.HasValue && SystemClock.UtcNow.Subtract(lastCounterSavedDate.Value).TotalMinutes >= USAGE_SAVE_MINUTES)
                shouldSaveUsage = true;

            if (shouldSaveUsage) {
                try {
                    org = await GetByIdAsync(organizationId).AnyContext();
                    org.SetMonthlyUsage(monthlyTotal, monthlyBlocked, monthlyTooBig);
                    if (hourlyTotal > org.GetHourlyEventLimit())
                        org.SetHourlyOverage(hourlyTotal, hourlyBlocked, hourlyTooBig);

                    await this.SaveAsync(org, o => o.Cache()).AnyContext();
                    await Cache.SetAsync(GetUsageSavedCacheKey(organizationId), SystemClock.UtcNow, TimeSpan.FromDays(32)).AnyContext();
                } catch (Exception ex) {
                    _logger.Error(ex, "Error while saving organization usage data.");

                    // Set the next document save for 5 seconds in the future.
                    await Cache.SetAsync(GetUsageSavedCacheKey(organizationId), SystemClock.UtcNow.SubtractMinutes(4).SubtractSeconds(55), TimeSpan.FromDays(32)).AnyContext();
                }
            }

            if (justWentOverMonthly)
                await PublishMessageAsync(new PlanOverage { OrganizationId = org.Id }).AnyContext();
            else if (justWentOverHourly)
                await PublishMessageAsync(new PlanOverage { OrganizationId = org.Id, IsHourly = true }).AnyContext();

            return overLimit;
        }

        private double GetTotalBlocked(Organization organization, int count, double monthlyTotal, double monthlyBlocked, double hourlyTotal, double hourlyBlocked, bool applyHourlyLimit) {
            if (organization.IsSuspended)
                return count;

            int hourlyEventLimit = organization.GetHourlyEventLimit();
            int monthlyEventLimit = organization.GetMaxEventsPerMonthWithBonus();
            double originalAllowedMonthlyEventTotal = monthlyTotal - monthlyBlocked - count;

            // If the original count is less than the max events per month and original count + hourly limit is greater than the max events per month then use the monthly limit.
            if (originalAllowedMonthlyEventTotal < monthlyEventLimit && (originalAllowedMonthlyEventTotal + hourlyEventLimit) >= monthlyEventLimit)
                return originalAllowedMonthlyEventTotal < monthlyEventLimit ? monthlyTotal - monthlyBlocked - monthlyEventLimit : count;

            double originalAllowedHourlyEventTotal = hourlyTotal - hourlyBlocked - count;
            if (applyHourlyLimit && (hourlyTotal - hourlyBlocked) > hourlyEventLimit)
                return originalAllowedHourlyEventTotal < hourlyEventLimit ? hourlyTotal - hourlyBlocked - hourlyEventLimit : count;

            if ((monthlyTotal - monthlyBlocked) > monthlyEventLimit)
                return originalAllowedMonthlyEventTotal < monthlyEventLimit ? monthlyTotal - monthlyBlocked - monthlyEventLimit : count;

            return 0;
        }

        public async Task<int> GetRemainingEventLimitAsync(string organizationId) {
            var organization = await this.GetByIdAsync(organizationId, o => o.Cache()).AnyContext();
            if (organization == null || organization.MaxEventsPerMonth < 0)
                return Int32.MaxValue;

            string monthlyCacheKey = GetMonthlyTotalCacheKey(organizationId);
            long monthlyEventCount = await Cache.GetAsync<long>(monthlyCacheKey, 0).AnyContext();
            return Math.Max(0, organization.GetMaxEventsPerMonthWithBonus() - (int)monthlyEventCount);
        }
    }

    public enum OrganizationSortBy {
        Newest = 0,
        Subscribed = 1,
        MostActive = 2,
        Alphabetical = 3,
    }
}
