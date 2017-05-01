using System;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Billing;
using Exceptionless.Core.Repositories.Configuration;
using FluentValidation;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;
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
    }

    public enum OrganizationSortBy {
        Newest = 0,
        Subscribed = 1,
        MostActive = 2,
        Alphabetical = 3,
    }
}
